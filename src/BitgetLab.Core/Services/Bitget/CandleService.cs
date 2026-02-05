using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
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
    
    // Bitget API limit constraints
    private const int MAX_FUTURES_HISTORY_LIMIT = 200; // Maximum limit for /api/v2/mix/market/history-candles endpoint
    
    // Chunk sizes for futures REST backfill (to avoid Bitget 40017 errors on large time ranges)
    // These are maximum time ranges per single API request for each interval
    private static readonly Dictionary<string, TimeSpan> FuturesChunkSizes = new()
    {
        { "1h", TimeSpan.FromDays(7) },    // 1 hour candles: 7 day chunks
        { "4h", TimeSpan.FromDays(30) },   // 4 hour candles: 30 day chunks
        { "1d", TimeSpan.FromDays(180) },  // 1 day candles: 180 day chunks
        // For other intervals, use conservative defaults
        { "1m", TimeSpan.FromHours(6) },
        { "5m", TimeSpan.FromHours(12) },
        { "15m", TimeSpan.FromDays(3) },
        { "30m", TimeSpan.FromDays(5) },
        { "6h", TimeSpan.FromDays(40) },
        { "12h", TimeSpan.FromDays(60) },
        { "3d", TimeSpan.FromDays(270) },
        { "1w", TimeSpan.FromDays(365) },
        { "1mo", TimeSpan.FromDays(730) },
        { "1month", TimeSpan.FromDays(730) }
    };

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
        // Cap limit to MAX_FUTURES_HISTORY_LIMIT (200) for Bitget futures API constraint
        var effectiveLimit = Math.Min(limit, MAX_FUTURES_HISTORY_LIMIT);
        
        // Map interval to Bitget granularity string (case-sensitive: 1H not 1h)
        var granularity = MapIntervalToGranularity(interval);
        
        // Build URL for Bitget public REST API
        var url = $"{_futuresOptions.PublicRestBaseUrl}/api/v2/mix/market/history-candles";
        url += $"?symbol={symbol}";
        url += $"&granularity={granularity}";
        url += $"&productType={_futuresOptions.ProductType}";
        url += $"&limit={effectiveLimit}";
        
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
            
            // Enhanced logging with time range details
            _logger.LogError(
                "Bitget API error for {Symbol} {Interval}: HTTP {StatusCode}. " +
                "Limit: {EffectiveLimit}, StartTime: {StartTime} ({StartMs}ms), EndTime: {EndTime} ({EndMs}ms). Error: {Error}",
                symbol, interval, response.StatusCode, effectiveLimit,
                startTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null", startMs,
                endTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null", endMs,
                truncatedError);
            
            throw new BitgetApiException($"Failed to get futures candles for {symbol}: HTTP {response.StatusCode} - {truncatedError}");
        }
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Parse JSON response
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        
        // Check for API error
        if (root.TryGetProperty("code", out var codeElement) && codeElement.GetString() != "00000")
        {
            var code = codeElement.GetString();
            var msg = root.TryGetProperty("msg", out var msgElement) ? msgElement.GetString() : "Unknown error";
            
            // Enhanced logging with time range details
            _logger.LogError(
                "Bitget API error code {Code} for {Symbol} {Interval}: {Message}. " +
                "Limit: {EffectiveLimit}, StartTime: {StartTime} ({StartMs}ms), EndTime: {EndTime} ({EndMs}ms)",
                code, symbol, interval, msg, effectiveLimit,
                startTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null", startMs,
                endTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null", endMs);
            
            throw new BitgetApiException($"Failed to get futures candles for {symbol}: {code} - {msg}");
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
        // Guard: validate time range before making any API calls
        if (startTime >= endTime)
        {
            _logger.LogWarning(
                "Invalid time range for {Symbol} {Interval}: startTime ({StartTime}) >= endTime ({EndTime}). Returning empty result.",
                symbol, interval, startTime.ToString("yyyy-MM-dd HH:mm:ss"), endTime.ToString("yyyy-MM-dd HH:mm:ss"));
            return new List<CandleDto>();
        }

        // Cap per-request limit to MAX_FUTURES_HISTORY_LIMIT for Bitget futures API constraint
        var effectivePerRequestLimit = Math.Min(perRequestLimit, MAX_FUTURES_HISTORY_LIMIT);

        // Get chunk size for this interval
        var chunkSize = GetChunkSizeForInterval(interval);
        var totalRange = endTime - startTime;
        
        // If the range is larger than chunk size, split into chunks
        if (totalRange > chunkSize)
        {
            _logger.LogInformation(
                "Large time range detected for {Symbol} {Interval}: {TotalDays:F1} days. " +
                "Splitting into chunks of {ChunkDays:F1} days to avoid API errors.",
                symbol, interval, totalRange.TotalDays, chunkSize.TotalDays);
            
            return await FetchFuturesRangeInChunksAsync(
                symbol, interval, startTime, endTime, effectivePerRequestLimit, chunkSize, cancellationToken);
        }

        // For small ranges, use existing backward pagination logic
        var allCandles = new Dictionary<DateTime, CandleDto>(); // Use dictionary for deduplication
        var currentEndTime = endTime;
        var intervalTimeSpan = IntervalHelper.ParseIntervalToTimeSpan(interval);
        
        // Calculate expected number of candles for safety check
        var expectedCandles = CalculateExpectedCandles(startTime, endTime, intervalTimeSpan);
        
        // Safety limit: max iterations to prevent infinite loops
        var maxIterations = Math.Min(MAX_PAGINATION_ITERATIONS, 
            Math.Max(MIN_PAGINATION_ITERATIONS, (expectedCandles / effectivePerRequestLimit + 1) * SAFETY_MULTIPLIER));
        var iteration = 0;
        
        while (currentEndTime > startTime && iteration < maxIterations)
        {
            iteration++;
            cancellationToken.ThrowIfCancellationRequested();
            
            // Fetch next page stepping backwards
            var pageCandles = await FetchFromPublicHistoryApiAsync(
                symbol, interval, startTime, currentEndTime, effectivePerRequestLimit, cancellationToken);
            
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
            if (pageCandles.Count < effectivePerRequestLimit)
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
    
    /// <summary>
    /// Get the chunk size for a given interval to avoid Bitget 40017 errors
    /// </summary>
    private TimeSpan GetChunkSizeForInterval(string interval)
    {
        var normalizedInterval = interval.ToLowerInvariant();
        if (FuturesChunkSizes.TryGetValue(normalizedInterval, out var chunkSize))
        {
            return chunkSize;
        }
        
        // Default to 7 days for unknown intervals
        _logger.LogWarning("No chunk size configured for interval {Interval}, using default 7 days", interval);
        return TimeSpan.FromDays(7);
    }
    
    /// <summary>
    /// Fetch futures candles by splitting the time range into chunks to avoid API errors
    /// </summary>
    private async Task<List<CandleDto>> FetchFuturesRangeInChunksAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        int effectivePerRequestLimit,
        TimeSpan chunkSize,
        CancellationToken cancellationToken)
    {
        var allCandles = new Dictionary<DateTime, CandleDto>(); // Use dictionary for deduplication
        var intervalTimeSpan = IntervalHelper.ParseIntervalToTimeSpan(interval);
        
        // Align startTime and endTime to interval boundaries to avoid gaps
        var alignedStartTime = AlignToIntervalBoundary(startTime, intervalTimeSpan, roundUp: false);
        var alignedEndTime = AlignToIntervalBoundary(endTime, intervalTimeSpan, roundUp: true);
        
        // Generate chunks: [chunkStart, chunkEnd) with half-open intervals
        var chunks = GenerateChunks(alignedStartTime, alignedEndTime, chunkSize);
        
        _logger.LogInformation(
            "Fetching {Symbol} {Interval} in {ChunkCount} chunks from {StartTime} to {EndTime}",
            symbol, interval, chunks.Count, 
            alignedStartTime.ToString("yyyy-MM-dd HH:mm:ss"), 
            alignedEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
        
        var chunkIndex = 0;
        foreach (var (chunkStart, chunkEnd) in chunks)
        {
            chunkIndex++;
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug(
                "Fetching chunk {ChunkIndex}/{ChunkCount} for {Symbol} {Interval}: {ChunkStart} to {ChunkEnd}",
                chunkIndex, chunks.Count, symbol, interval,
                chunkStart.ToString("yyyy-MM-dd HH:mm:ss"),
                chunkEnd.ToString("yyyy-MM-dd HH:mm:ss"));
            
            // Fetch this chunk using backward pagination
            var chunkCandles = await FetchChunkWithBackwardPaginationAsync(
                symbol, interval, chunkStart, chunkEnd, effectivePerRequestLimit, intervalTimeSpan, cancellationToken);
            
            // Add candles to dictionary (automatic deduplication by OpenTime)
            foreach (var candle in chunkCandles)
            {
                allCandles[candle.OpenTime] = candle;
            }
            
            _logger.LogDebug(
                "Fetched {CandleCount} candles for chunk {ChunkIndex}/{ChunkCount} ({Symbol} {Interval})",
                chunkCandles.Count, chunkIndex, chunks.Count, symbol, interval);
        }
        
        _logger.LogInformation(
            "Completed chunked fetch for {Symbol} {Interval}: {TotalCandles} total candles from {ChunkCount} chunks",
            symbol, interval, allCandles.Count, chunks.Count);
        
        // Return candles sorted by OpenTime ascending
        return allCandles.Values.OrderBy(c => c.OpenTime).ToList();
    }
    
    /// <summary>
    /// Align a DateTime to interval boundaries (round down by default)
    /// </summary>
    private DateTime AlignToIntervalBoundary(DateTime dateTime, TimeSpan intervalTimeSpan, bool roundUp)
    {
        var ticks = dateTime.Ticks;
        var intervalTicks = intervalTimeSpan.Ticks;
        
        if (intervalTicks <= 0)
        {
            return dateTime;
        }
        
        var remainder = ticks % intervalTicks;
        
        if (remainder == 0)
        {
            // Already aligned
            return dateTime;
        }
        
        if (roundUp)
        {
            // Round up to next interval boundary
            return new DateTime(ticks - remainder + intervalTicks, dateTime.Kind);
        }
        else
        {
            // Round down to previous interval boundary
            return new DateTime(ticks - remainder, dateTime.Kind);
        }
    }
    
    /// <summary>
    /// Generate time chunks for fetching: [start, end) split into smaller ranges
    /// </summary>
    private List<(DateTime chunkStart, DateTime chunkEnd)> GenerateChunks(
        DateTime startTime,
        DateTime endTime,
        TimeSpan chunkSize)
    {
        var chunks = new List<(DateTime, DateTime)>();
        var currentStart = startTime;
        
        while (currentStart < endTime)
        {
            var currentEnd = currentStart.Add(chunkSize);
            
            // Don't exceed the overall endTime
            if (currentEnd > endTime)
            {
                currentEnd = endTime;
            }
            
            // Guard: ensure valid chunk (start < end)
            if (currentStart >= currentEnd)
            {
                _logger.LogWarning(
                    "Invalid chunk detected: start ({Start}) >= end ({End}). Stopping chunk generation.",
                    currentStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    currentEnd.ToString("yyyy-MM-dd HH:mm:ss"));
                break;
            }
            
            chunks.Add((currentStart, currentEnd));
            
            // Next chunk starts where this one ends (half-open interval: [start, end))
            currentStart = currentEnd;
        }
        
        return chunks;
    }
    
    /// <summary>
    /// Fetch a single chunk using backward pagination
    /// </summary>
    private async Task<List<CandleDto>> FetchChunkWithBackwardPaginationAsync(
        string symbol,
        string interval,
        DateTime chunkStart,
        DateTime chunkEnd,
        int effectivePerRequestLimit,
        TimeSpan intervalTimeSpan,
        CancellationToken cancellationToken)
    {
        var chunkCandles = new Dictionary<DateTime, CandleDto>();
        var currentEndTime = chunkEnd;
        
        // Calculate expected candles for this chunk
        var expectedCandles = CalculateExpectedCandles(chunkStart, chunkEnd, intervalTimeSpan);
        
        // Safety limit for this chunk
        var maxIterations = Math.Min(MAX_PAGINATION_ITERATIONS, 
            Math.Max(MIN_PAGINATION_ITERATIONS, (expectedCandles / effectivePerRequestLimit + 1) * SAFETY_MULTIPLIER));
        var iteration = 0;
        
        while (currentEndTime > chunkStart && iteration < maxIterations)
        {
            iteration++;
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                // Fetch next page stepping backwards
                var pageCandles = await FetchFromPublicHistoryApiAsync(
                    symbol, interval, chunkStart, currentEndTime, effectivePerRequestLimit, cancellationToken);
                
                // If no data returned, we've reached the end
                if (pageCandles.Count == 0)
                {
                    break;
                }
                
                // Add candles to dictionary (automatic deduplication by OpenTime)
                foreach (var candle in pageCandles)
                {
                    chunkCandles[candle.OpenTime] = candle;
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
                if (pageCandles.Count < effectivePerRequestLimit)
                {
                    break;
                }
            }
            catch (BitgetApiException ex)
            {
                // Enhanced error logging with chunk parameters
                _logger.LogError(
                    "Bitget API error for chunk {Symbol} {Interval}: {Message}. " +
                    "ChunkStart: {ChunkStart}, ChunkEnd: {ChunkEnd}, CurrentEndTime: {CurrentEndTime}",
                    symbol, interval, ex.Message,
                    chunkStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    chunkEnd.ToString("yyyy-MM-dd HH:mm:ss"),
                    currentEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
                throw;
            }
        }
        
        return chunkCandles.Values.OrderBy(c => c.OpenTime).ToList();
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
