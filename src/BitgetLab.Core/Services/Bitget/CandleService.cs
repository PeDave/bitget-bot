using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Globalization;

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
    private readonly BitgetFuturesOptions _futuresOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CandleService> _logger;

    // Pagination safety constants
    private const int MAX_PAGINATION_ITERATIONS = 200; // Maximum iterations to prevent infinite loops
    private const int MIN_PAGINATION_ITERATIONS = 10;  // Minimum iterations regardless of expected candles
    private const int SAFETY_MULTIPLIER = 2;           // Multiply expected iterations by this for buffer
    private const int DEFAULT_EXPECTED_CANDLES = 1000; // Default when interval calculation fails

    public CandleService(
        IBitgetClientFactory clientFactory,
        IOptions<ChartingOptions> chartingOptions,
        IOptions<BitgetFuturesOptions> futuresOptions,
        HttpClient httpClient,
        ILogger<CandleService> logger,
        ICandleRepository? candleRepository = null)
    {
        _clientFactory = clientFactory;
        _candleRepository = candleRepository;
        _chartingOptions = chartingOptions.Value;
        _futuresOptions = futuresOptions.Value;
        _httpClient = httpClient;
        _logger = logger;
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
        // Route to appropriate API based on market type
        if (market == MarketType.Futures)
        {
            // For futures, always use public history endpoint (no client needed)
            return await FetchFromPublicHistoryApiAsync(symbol, interval, startTime, endTime, limit, cancellationToken);
        }
        else
        {
            using var client = _clientFactory.CreateRestClient();
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
        // Use public history-candles endpoint for futures
        // This endpoint supports correct granularity strings and millisecond timestamps
        return await FetchFromPublicHistoryApiAsync(symbol, interval, startTime, endTime, limit, cancellationToken);
    }

    private async Task<List<CandleDto>> FetchFromPublicHistoryApiAsync(
        string symbol,
        string interval,
        DateTime? startTime,
        DateTime? endTime,
        int limit,
        CancellationToken cancellationToken)
    {
        // Validate time range if both are provided
        if (startTime.HasValue && endTime.HasValue)
        {
            if (startTime.Value >= endTime.Value)
            {
                _logger.LogWarning(
                    "Invalid time range for {Symbol} {Interval}: startTime={StartTime} >= endTime={EndTime}. " +
                    "Returning empty result instead of calling API.",
                    symbol, interval, startTime.Value, endTime.Value);
                return new List<CandleDto>();
            }
        }

        // Map interval to Bitget granularity string (case-sensitive: 1H not 1h)
        var granularity = MapIntervalToGranularity(interval);
        
        // Build URL for Bitget public REST API
        var url = $"{_futuresOptions.PublicRestBaseUrl}/api/v2/mix/market/history-candles";
        url += $"?symbol={symbol}";
        url += $"&granularity={granularity}";
        url += $"&productType={_futuresOptions.ProductType}";
        url += $"&limit={limit}";
        
        // Convert DateTime to Unix milliseconds if provided
        long? startMs = null;
        long? endMs = null;
        if (startTime.HasValue)
        {
            startMs = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds();
            url += $"&startTime={startMs}";
        }
        if (endTime.HasValue)
        {
            endMs = new DateTimeOffset(endTime.Value).ToUnixTimeMilliseconds();
            url += $"&endTime={endMs}";
        }
        
        // Make HTTP GET request
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            // Limit error message to prevent excessive memory usage
            var truncatedError = errorContent.Length > 1000 ? errorContent.Substring(0, 1000) + "..." : errorContent;
            
            // Log detailed info for debugging
            _logger.LogError(
                "Bitget API failed for {Symbol} {Interval}: HTTP {StatusCode}. " +
                "Request params - startTime: {StartTime} ({StartMs}ms), endTime: {EndTime} ({EndMs}ms), " +
                "limit: {Limit}, productType: {ProductType}, granularity: {Granularity}. Error: {Error}",
                symbol, interval, response.StatusCode,
                startTime?.ToString("o"), startMs,
                endTime?.ToString("o"), endMs,
                limit, _futuresOptions.ProductType, granularity, truncatedError);
            
            throw new BitgetApiException($"Failed to get futures candles for {symbol}: HTTP {response.StatusCode} - {truncatedError}");
        }
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Parse JSON response
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        
        // Check for API error
        if (root.TryGetProperty("code", out var codeElement) && codeElement.GetString() != "00000")
        {
            var msg = root.TryGetProperty("msg", out var msgElement) ? msgElement.GetString() : "Unknown error";
            throw new BitgetApiException($"Failed to get futures candles for {symbol}: {msg}");
        }
        
        // Parse candles from data array
        if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
        {
            return new List<CandleDto>();
        }
        
        var candles = new List<CandleDto>();
        foreach (var candleArray in dataElement.EnumerateArray())
        {
            if (candleArray.ValueKind != JsonValueKind.Array || candleArray.GetArrayLength() < 7)
            {
                continue;
            }
            
            try
            {
                // Bitget format: [timestamp, open, high, low, close, volume, quoteVolume]
                var timestampMs = ReadInt64(candleArray[0]);
                var open = ReadDecimal(candleArray[1]);
                var high = ReadDecimal(candleArray[2]);
                var low = ReadDecimal(candleArray[3]);
                var close = ReadDecimal(candleArray[4]);
                var volume = ReadDecimal(candleArray[5]);
                var quoteVolume = ReadDecimal(candleArray[6]);
                
                candles.Add(new CandleDto
                {
                    OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume,
                    QuoteVolume = quoteVolume
                });
            }
            catch (Exception ex)
            {
                // Log raw values for diagnostics
                var arrayLength = candleArray.GetArrayLength();
                var rawValues = string.Join(", ", Enumerable.Range(0, arrayLength)
                    .Select(i => candleArray[i].ToString()));
                throw new InvalidOperationException(
                    $"Failed to parse candle data. Raw values: [{rawValues}]", ex);
            }
        }
        
        return candles;
    }

    private string MapIntervalToGranularity(string interval)
    {
        // Map API interval strings to Bitget granularity strings
        // Note: Hours and days are uppercase in Bitget API
        return interval.ToLowerInvariant() switch
        {
            "1m" => "1m",
            "5m" => "5m",
            "15m" => "15m",
            "30m" => "30m",
            "1h" => "1H",  // Case-sensitive: uppercase H
            "4h" => "4H",  // Case-sensitive: uppercase H
            "6h" => "6H",  // Case-sensitive: uppercase H
            "12h" => "12H", // Case-sensitive: uppercase H
            "1d" => "1D",  // Case-sensitive: uppercase D
            "3d" => "3D",  // Case-sensitive: uppercase D
            "1w" => "1W",  // Case-sensitive: uppercase W
            "1mo" or "1month" => "1M", // Case-sensitive: uppercase M
            _ => throw new ArgumentException($"Invalid interval: {interval}. Supported values (case-insensitive): 1m, 5m, 15m, 30m, 1h, 4h, 6h, 12h, 1d, 3d, 1w, 1mo, 1month")
        };
    }

    /// <summary>
    /// Reads a long integer from a JsonElement, supporting both Number and String value kinds.
    /// </summary>
    private static long ReadInt64(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt64(),
            JsonValueKind.String => long.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Cannot parse Int64 from JSON type {element.ValueKind}")
        };
    }

    /// <summary>
    /// Reads a decimal from a JsonElement, supporting both Number and String value kinds.
    /// </summary>
    private static decimal ReadDecimal(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.String => decimal.Parse(element.GetString() ?? "0", CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Cannot parse decimal from JSON type {element.ValueKind}")
        };
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
        // For futures, use backward-stepping pagination
        if (market == MarketType.Futures)
        {
            return await FetchFuturesRangeWithBackwardPaginationAsync(
                symbol, interval, startTime, endTime, perRequestLimit, cancellationToken);
        }
        
        // For spot, use forward-stepping pagination (existing logic)
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
            
            // Fetch next page
            var pageCandles = await FetchFromSpotApiAsync(client, symbol, interval, currentStartTime, endTime, perRequestLimit, cancellationToken);
            
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

    private async Task<List<CandleDto>> FetchFuturesRangeWithBackwardPaginationAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        int perRequestLimit,
        CancellationToken cancellationToken)
    {
        // Validate time range before pagination
        if (startTime >= endTime)
        {
            _logger.LogWarning(
                "Invalid time range for {Symbol} {Interval} pagination: startTime={StartTime} >= endTime={EndTime}. " +
                "Returning empty result.",
                symbol, interval, startTime, endTime);
            return new List<CandleDto>();
        }

        var allCandles = new Dictionary<DateTime, CandleDto>(); // Use dictionary for deduplication
        var currentEndTime = endTime;
        var intervalTimeSpan = IntervalHelper.ParseIntervalToTimeSpan(interval);
        
        // Calculate expected number of candles for safety check
        var expectedCandles = CalculateExpectedCandles(startTime, endTime, intervalTimeSpan);
        
        // Safety limit: max iterations to prevent infinite loops
        var maxIterations = Math.Min(MAX_PAGINATION_ITERATIONS, 
            Math.Max(MIN_PAGINATION_ITERATIONS, (expectedCandles / perRequestLimit + 1) * SAFETY_MULTIPLIER));
        var iteration = 0;
        
        while (currentEndTime > startTime && iteration < maxIterations)
        {
            iteration++;
            cancellationToken.ThrowIfCancellationRequested();
            
            // Validate time range before each API call
            if (startTime >= currentEndTime)
            {
                _logger.LogDebug(
                    "Stopping pagination for {Symbol} {Interval}: computed startTime >= currentEndTime " +
                    "(startTime={StartTime}, currentEndTime={CurrentEndTime})",
                    symbol, interval, startTime, currentEndTime);
                break;
            }
            
            // Fetch next page stepping backwards
            var pageCandles = await FetchFromPublicHistoryApiAsync(
                symbol, interval, startTime, currentEndTime, perRequestLimit, cancellationToken);
            
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
            
            // Move to next page: set endTime to (minOpenTime - 1ms) from previous batch
            var minCandleTime = pageCandles.Min(k => k.OpenTime);
            var nextEndTime = minCandleTime.AddMilliseconds(-1);
            
            // If we're not making progress, break to avoid infinite loop
            if (nextEndTime >= currentEndTime)
            {
                break;
            }
            
            currentEndTime = nextEndTime;
            
            // If we got fewer candles than requested, we've likely reached the end
            if (pageCandles.Count < perRequestLimit)
            {
                break;
            }
        }
        
        // Return candles sorted by OpenTime ascending
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
}
