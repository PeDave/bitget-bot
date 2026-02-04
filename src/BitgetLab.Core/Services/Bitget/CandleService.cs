using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Microsoft.Extensions.Options;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving historical candle data using Bitget.Net SDK
/// </summary>
public interface ICandleService
{
    /// <summary>
    /// Gets historical candle data for a symbol
    /// </summary>
    /// <param name="symbol">Trading symbol (e.g., BTCUSDT)</param>
    /// <param name="interval">Candle interval (e.g., 1m, 5m, 15m, 1h, 4h, 1d)</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="limit">Maximum number of candles per request (default 100, max 1000). When both startTime and endTime are provided, this acts as page size for pagination and the method returns all candles in the range.</param>
    /// <param name="market">Market type (spot or futures), defaults to spot</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of candles. When startTime and endTime are provided, returns all candles in the date range (potentially more than limit). Otherwise, returns up to limit candles.</returns>
    Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);
}

public class CandleService : ICandleService
{
    private readonly IBitgetClientFactory _clientFactory;
    private readonly ICandleRepository? _candleRepository;
    private readonly ChartingOptions _chartingOptions;

    // Pagination safety constants
    private const int MAX_PAGINATION_ITERATIONS = 200; // Maximum iterations to prevent infinite loops
    private const int MIN_PAGINATION_ITERATIONS = 10;  // Minimum iterations regardless of expected candles
    private const int SAFETY_MULTIPLIER = 2;           // Multiply expected iterations by this for buffer
    private const int DEFAULT_EXPECTED_CANDLES = 1000; // Default when interval calculation fails

    public CandleService(
        IBitgetClientFactory clientFactory,
        IOptions<ChartingOptions> chartingOptions,
        ICandleRepository? candleRepository = null)
    {
        _clientFactory = clientFactory;
        _candleRepository = candleRepository;
        _chartingOptions = chartingOptions.Value;
    }

    public async Task<IEnumerable<CandleDto>> GetCandlesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            throw new ArgumentException("Interval is required", nameof(interval));
        }

        // When both startTime and endTime are provided, calculate expected candles
        // Otherwise, limit to max 1000 candles per request
        int perRequestLimit = Math.Min(limit, 1000);

        // Try to get from database first if persistence is enabled
        if (_chartingOptions.EnablePersistence && _candleRepository != null)
        {
            var dbCandles = await _candleRepository.GetCandlesAsync(
                symbol, interval, startTime, endTime, limit, market, cancellationToken);
            
            var dbCandleList = dbCandles.ToList();
            
            // Check if DB results are sufficient
            bool isSufficient = false;
            
            // If we have time constraints, check range coverage
            if (startTime.HasValue && endTime.HasValue && dbCandleList.Count > 0)
            {
                var minDbTime = dbCandleList.Min(c => c.OpenTime);
                var maxDbTime = dbCandleList.Max(c => c.OpenTime);
                
                // Check if DB covers the requested range
                isSufficient = minDbTime <= startTime.Value && maxDbTime >= endTime.Value;
            }
            else
            {
                // For limit-only queries, check if we have enough candles
                isSufficient = dbCandleList.Count >= limit;
            }
            
            // If DB data is sufficient, return it
            if (isSufficient && dbCandleList.Count > 0)
            {
                return dbCandleList;
            }
            
            // DB results are insufficient - fetch from REST and merge
            if (dbCandleList.Count > 0)
            {
                // Fetch from REST API with pagination for full range
                var restCandles = await FetchFromRestApiAsync(symbol, interval, startTime, endTime, perRequestLimit, market, cancellationToken);
                
                // Merge DB and REST candles, dedupe by OpenTime
                var mergedCandles = dbCandleList
                    .Concat(restCandles)
                    .GroupBy(c => c.OpenTime)
                    .Select(g => g.First()) // Take first occurrence (prefer DB data)
                    .OrderBy(c => c.OpenTime)
                    .ToList();
                
                // Apply limit only for non-range queries
                if (!startTime.HasValue || !endTime.HasValue)
                {
                    mergedCandles = mergedCandles.Take(limit).ToList();
                }
                
                // Persist REST-fetched candles to DB
                var newCandles = restCandles
                    .Where(rc => !dbCandleList.Any(dc => dc.OpenTime == rc.OpenTime))
                    .ToList();
                
                if (newCandles.Count > 0)
                {
                    // Fire-and-forget persistence for REST API calls (acceptable here as these are
                    // one-off operations, not real-time streams like WebSocket updates)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _candleRepository.UpsertCandlesAsync(symbol, interval, newCandles, market, CancellationToken.None);
                        }
                        catch
                        {
                            // Fire-and-forget, errors logged by repository
                        }
                    }, CancellationToken.None);
                }
                
                return mergedCandles;
            }
        }

        // Otherwise fetch from Bitget REST API with pagination for full range
        var candles = await FetchFromRestApiAsync(symbol, interval, startTime, endTime, perRequestLimit, market, cancellationToken);
        
        // Persist to database if enabled
        if (_chartingOptions.EnablePersistence && _candleRepository != null && candles.Count > 0)
        {
            // Fire-and-forget persistence for REST API calls (acceptable here as these are
            // one-off operations, not real-time streams like WebSocket updates)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _candleRepository.UpsertCandlesAsync(symbol, interval, candles, market, CancellationToken.None);
                }
                catch
                {
                    // Fire-and-forget, errors logged by repository
                }
            }, CancellationToken.None);
        }
        
        return candles;
    }

    private async Task<List<CandleDto>> FetchFromRestApiAsync(
        string symbol,
        string interval,
        DateTime? startTime,
        DateTime? endTime,
        int limit,
        MarketType market,
        CancellationToken cancellationToken)
    {
        // If both startTime and endTime are provided, fetch all candles in range with pagination
        if (startTime.HasValue && endTime.HasValue)
        {
            return await FetchRangeWithPaginationAsync(symbol, interval, startTime.Value, endTime.Value, limit, market, cancellationToken);
        }
        
        // Otherwise, single request with limit
        using var client = _clientFactory.CreateRestClient();
        
        // Route to appropriate API based on market type
        if (market == MarketType.Futures)
        {
            return await FetchFromFuturesApiAsync(client, symbol, interval, startTime, endTime, limit, cancellationToken);
        }
        else
        {
            return await FetchFromSpotApiAsync(client, symbol, interval, startTime, endTime, limit, cancellationToken);
        }
    }

    private async Task<List<CandleDto>> FetchFromSpotApiAsync(
        global::Bitget.Net.Interfaces.Clients.IBitgetRestClient client,
        string symbol,
        string interval,
        DateTime? startTime,
        DateTime? endTime,
        int limit,
        CancellationToken cancellationToken)
    {
        // Get klines from Bitget Spot API
        var result = await client.SpotApiV2.ExchangeData.GetKlinesAsync(
            symbol: symbol,
            interval: ParseInterval(interval),
            startTime: startTime,
            endTime: endTime,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get spot candles for {symbol}: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(k => new CandleDto
        {
            OpenTime = k.OpenTime,
            Open = k.OpenPrice,
            High = k.HighPrice,
            Low = k.LowPrice,
            Close = k.ClosePrice,
            Volume = k.Volume,
            QuoteVolume = k.QuoteVolume
        }).ToList();
    }

    private async Task<List<CandleDto>> FetchFromFuturesApiAsync(
        global::Bitget.Net.Interfaces.Clients.IBitgetRestClient client,
        string symbol,
        string interval,
        DateTime? startTime,
        DateTime? endTime,
        int limit,
        CancellationToken cancellationToken)
    {
        // Get klines from Bitget Futures API (USDT-margined)
        var result = await client.FuturesApiV2.ExchangeData.GetKlinesAsync(
            productType: global::Bitget.Net.Enums.BitgetProductTypeV2.UsdtFutures,
            symbol: symbol,
            interval: ParseFuturesInterval(interval),
            startTime: startTime,
            endTime: endTime,
            limit: limit,
            ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get futures candles for {symbol}: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(k => new CandleDto
        {
            OpenTime = k.OpenTime,
            Open = k.OpenPrice,
            High = k.HighPrice,
            Low = k.LowPrice,
            Close = k.ClosePrice,
            Volume = k.Volume,
            QuoteVolume = k.QuoteVolume
        }).ToList();
    }

    private async Task<List<CandleDto>> FetchRangeWithPaginationAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        int perRequestLimit,
        MarketType market,
        CancellationToken cancellationToken)
    {
        var allCandles = new Dictionary<DateTime, CandleDto>(); // Use dictionary for deduplication
        var currentStartTime = startTime;
        var intervalTimeSpan = IntervalHelper.ParseIntervalToTimeSpan(interval);
        
        // Calculate expected number of candles for safety check
        var expectedCandles = CalculateExpectedCandles(startTime, endTime, intervalTimeSpan);
        
        // Safety limit: max iterations to prevent infinite loops
        // Allow up to 10x the expected candles with a safety buffer, capped at max iterations
        var maxIterations = Math.Min(MAX_PAGINATION_ITERATIONS, 
            Math.Max(MIN_PAGINATION_ITERATIONS, (expectedCandles / perRequestLimit + 1) * SAFETY_MULTIPLIER));
        var iteration = 0;
        
        using var client = _clientFactory.CreateRestClient();
        
        while (currentStartTime < endTime && iteration < maxIterations)
        {
            iteration++;
            cancellationToken.ThrowIfCancellationRequested();
            
            // Fetch next page based on market type
            List<CandleDto> pageCandles;
            if (market == MarketType.Futures)
            {
                pageCandles = await FetchFromFuturesApiAsync(client, symbol, interval, currentStartTime, endTime, perRequestLimit, cancellationToken);
            }
            else
            {
                pageCandles = await FetchFromSpotApiAsync(client, symbol, interval, currentStartTime, endTime, perRequestLimit, cancellationToken);
            }
            
            // If no data returned, we've reached the end
            if (pageCandles.Count == 0)
            {
                break;
            }
            
            // Add candles to dictionary (automatic deduplication by OpenTime)
            foreach (var candle in pageCandles)
            {
                allCandles[candle.OpenTime] = candle;
            }
            
            // Move to next page: start from the last candle's time + interval
            var lastCandleTime = pageCandles.Max(k => k.OpenTime);
            var nextStartTime = lastCandleTime.Add(intervalTimeSpan);
            
            // If we're not making progress, break to avoid infinite loop
            if (nextStartTime <= currentStartTime)
            {
                break;
            }
            
            currentStartTime = nextStartTime;
            
            // If we got fewer candles than requested, we've likely reached the end
            if (pageCandles.Count < perRequestLimit)
            {
                break;
            }
        }
        
        // Return candles sorted by OpenTime
        return allCandles.Values.OrderBy(c => c.OpenTime).ToList();
    }

    private int CalculateExpectedCandles(DateTime startTime, DateTime endTime, TimeSpan intervalTimeSpan)
    {
        if (intervalTimeSpan.TotalSeconds <= 0)
        {
            return DEFAULT_EXPECTED_CANDLES;
        }
        
        var timeRange = endTime - startTime;
        var expectedCount = (int)(timeRange.TotalSeconds / intervalTimeSpan.TotalSeconds);
        
        // Return at least 1, and add some buffer for rounding
        return Math.Max(1, expectedCount + 10);
    }

    private global::Bitget.Net.Enums.V2.KlineInterval ParseInterval(string interval)
    {
        // Map string intervals to Bitget.Net enum
        return interval.ToLowerInvariant() switch
        {
            "1m" => global::Bitget.Net.Enums.V2.KlineInterval.OneMinute,
            "5m" => global::Bitget.Net.Enums.V2.KlineInterval.FiveMinutes,
            "15m" => global::Bitget.Net.Enums.V2.KlineInterval.FifteenMinutes,
            "30m" => global::Bitget.Net.Enums.V2.KlineInterval.ThirtyMinutes,
            "1h" => global::Bitget.Net.Enums.V2.KlineInterval.OneHour,
            "4h" => global::Bitget.Net.Enums.V2.KlineInterval.FourHours,
            "6h" => global::Bitget.Net.Enums.V2.KlineInterval.SixHours,
            "12h" => global::Bitget.Net.Enums.V2.KlineInterval.TwelveHours,
            "1d" => global::Bitget.Net.Enums.V2.KlineInterval.OneDay,
            "3d" => global::Bitget.Net.Enums.V2.KlineInterval.ThreeDays,
            "1w" => global::Bitget.Net.Enums.V2.KlineInterval.OneWeek,
            "1mo" or "1month" => global::Bitget.Net.Enums.V2.KlineInterval.OneMonth,
            _ => throw new ArgumentException($"Invalid interval: {interval}. Valid values: 1m, 5m, 15m, 30m, 1h, 4h, 6h, 12h, 1d, 3d, 1w, 1mo, 1month")
        };
    }

    private global::Bitget.Net.Enums.BitgetFuturesKlineInterval ParseFuturesInterval(string interval)
    {
        // Map string intervals to Bitget.Net futures enum
        return interval.ToLowerInvariant() switch
        {
            "1m" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.OneMinute,
            "5m" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.FiveMinutes,
            "15m" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.FifteenMinutes,
            "30m" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.ThirtyMinutes,
            "1h" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.OneHour,
            "4h" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.FourHours,
            "6h" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.SixHours,
            "12h" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.TwelveHours,
            "1d" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.OneDay,
            "3d" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.ThreeDays,
            "1w" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.OneWeek,
            "1mo" or "1month" => global::Bitget.Net.Enums.BitgetFuturesKlineInterval.OneMonth,
            _ => throw new ArgumentException($"Invalid interval: {interval}. Valid values: 1m, 5m, 15m, 30m, 1h, 4h, 6h, 12h, 1d, 3d, 1w, 1mo, 1month")
        };
    }
}
