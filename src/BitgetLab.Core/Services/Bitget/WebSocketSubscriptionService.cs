using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bitget.Net.Interfaces.Clients;
using Bitget.Net.Enums;
using Bitget.Net.Objects.Models.V2;
using CryptoExchange.Net.Objects.Sockets;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Represents a websocket subscription for candle updates
/// </summary>
public class CandleSubscription
{
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime SubscribedAt { get; set; }
    public CandleDto? LatestCandle { get; set; }
    public CandleRingBuffer RingBuffer { get; set; } = null!;
    public DateTime? LastOpenTime { get; set; }
    /// <summary>
    /// Last time a backfill was started for this subscription (to prevent backfill storms)
    /// </summary>
    public DateTime? LastBackfillTime { get; set; }
}

/// <summary>
/// Work item types for background queue processing
/// </summary>
internal enum BackgroundWorkType
{
    PersistCandles,
    BackfillGap
}

/// <summary>
/// Background work item for processing
/// </summary>
internal class BackgroundWorkItem
{
    public BackgroundWorkType Type { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public List<CandleDto>? Candles { get; set; }
    public DateTime? GapStart { get; set; }
    public DateTime? GapEnd { get; set; }
}

/// <summary>
/// Service for managing websocket subscriptions to candle data
/// </summary>
public interface IWebSocketSubscriptionService
{
    /// <summary>
    /// Subscribe to candle updates for a symbol/interval pair
    /// </summary>
    Task<bool> SubscribeAsync(string symbol, string interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from candle updates for a symbol/interval pair
    /// </summary>
    Task<bool> UnsubscribeAsync(string symbol, string interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active subscriptions
    /// </summary>
    IEnumerable<CandleSubscription> GetActiveSubscriptions();

    /// <summary>
    /// Get latest candle for a subscription
    /// </summary>
    CandleDto? GetLatestCandle(string symbol, string interval);

    /// <summary>
    /// Get candle buffer for a subscription
    /// </summary>
    List<CandleDto> GetCandleBuffer(string symbol, string interval, int limit);
}

public class WebSocketSubscriptionService : BackgroundService, IWebSocketSubscriptionService
{
    private readonly ConcurrentDictionary<string, CandleSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, UpdateSubscription> _socketSubscriptions = new();
    private readonly IBitgetSocketClientFactory _socketClientFactory;
    private readonly ICandleService _candleService;
    private readonly ICandleRepository? _candleRepository;
    private readonly ChartingOptions _chartingOptions;
    private readonly ILogger<WebSocketSubscriptionService> _logger;
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private volatile IBitgetSocketClient? _socketClient;
    private readonly ConcurrentDictionary<string, byte> _activeBackfills = new(); // Track active backfills
    
    // Separate channels for different work types
    private readonly Channel<BackgroundWorkItem> _persistQueue;
    private readonly Channel<BackgroundWorkItem> _backfillQueue;
    
    // Global backfill rate limiting (max 6 per minute)
    private readonly Queue<DateTime> _globalBackfillTimestamps = new();
    private readonly object _rateLimitLock = new();
    private const int MaxBackfillsPerMinute = 6;

    public WebSocketSubscriptionService(
        IBitgetSocketClientFactory socketClientFactory,
        ICandleService candleService,
        IOptions<ChartingOptions> chartingOptions,
        ILogger<WebSocketSubscriptionService> logger,
        ICandleRepository? candleRepository = null)
    {
        _socketClientFactory = socketClientFactory;
        _candleService = candleService;
        _candleRepository = candleRepository;
        _chartingOptions = chartingOptions.Value;
        _logger = logger;
        
        // Create separate bounded channels for different work types
        // Persist queue: can drop oldest items if full (data will be re-persisted)
        _persistQueue = Channel.CreateBounded<BackgroundWorkItem>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        
        // Backfill queue: never drop backfill work items
        _backfillQueue = Channel.CreateBounded<BackgroundWorkItem>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait // Block if full, ensuring backfills are not lost
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebSocket Subscription Service starting...");
        
        try
        {
            // Initialize socket client
            _socketClient = _socketClientFactory.CreateSocketClient();
            _logger.LogInformation("WebSocket client initialized");

            // Start background worker tasks for both queues
            var persistWorkerTask = Task.Run(() => ProcessPersistQueueAsync(stoppingToken), stoppingToken);
            var backfillWorkerTask = Task.Run(() => ProcessBackfillQueueAsync(stoppingToken), stoppingToken);

            // Keep the service running and log metrics periodically
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                
                // Log active subscriptions and queue depths
                var activeCount = _subscriptions.Count;
                var persistQueueCount = _persistQueue.Reader.Count;
                var backfillQueueCount = _backfillQueue.Reader.Count;
                
                if (activeCount > 0 || persistQueueCount > 0 || backfillQueueCount > 0)
                {
                    _logger.LogDebug("Active WebSocket subscriptions: {Count}, Persist queue: {PersistDepth}, Backfill queue: {BackfillDepth}", 
                        activeCount, persistQueueCount, backfillQueueCount);
                }
            }
            
            await Task.WhenAll(persistWorkerTask, backfillWorkerTask);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WebSocket Subscription Service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket Subscription Service");
        }
    }

    private async Task ProcessPersistQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Persist queue processor starting...");
        
        await foreach (var workItem in _persistQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (_chartingOptions.EnablePersistence && _candleRepository != null && workItem.Candles != null)
                {
                    await _candleRepository.UpsertCandlesAsync(
                        workItem.Symbol, 
                        workItem.Interval, 
                        workItem.Candles, 
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting candles for {Symbol} {Interval}", 
                    workItem.Symbol, workItem.Interval);
            }
        }
        
        _logger.LogInformation("Persist queue processor stopped");
    }

    private async Task ProcessBackfillQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Backfill queue processor starting...");
        
        await foreach (var workItem in _backfillQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessBackfillAsync(workItem, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing backfill for {Symbol} {Interval}", 
                    workItem.Symbol, workItem.Interval);
            }
        }
        
        _logger.LogInformation("Backfill queue processor stopped");
    }

    private async Task ProcessBackfillAsync(BackgroundWorkItem workItem, CancellationToken cancellationToken)
    {
        var backfillKey = GetSubscriptionKey(workItem.Symbol, workItem.Interval);
        
        // Check if already backfilling this subscription (prevents concurrent processing)
        if (!_activeBackfills.TryAdd(backfillKey, 0))
        {
            _logger.LogTrace("Backfill already in progress for {Symbol} {Interval}, skipping", 
                workItem.Symbol, workItem.Interval);
            return;
        }

        try
        {
            if (workItem.GapStart.HasValue && workItem.GapEnd.HasValue)
            {
                var originalStart = workItem.GapStart.Value;
                var originalEnd = workItem.GapEnd.Value;
                var originalRange = originalEnd - originalStart;
                
                // Clamp backfill range to prevent excessive API calls
                var maxRange = IntervalHelper.GetMaxBackfillRange(workItem.Interval);
                var clampedStart = originalStart;
                var clampedEnd = originalEnd;
                
                if (originalRange > maxRange)
                {
                    // Clamp to the most recent data (keep end time, adjust start time)
                    clampedStart = originalEnd - maxRange;
                    
                    _logger.LogWarning("Backfill range clamped for {Symbol} {Interval}: " +
                        "original range {OriginalRange} (from {OriginalStart} to {OriginalEnd}) " +
                        "exceeds max {MaxRange}, clamping to {ClampedRange} (from {ClampedStart} to {ClampedEnd})",
                        workItem.Symbol, workItem.Interval,
                        originalRange, originalStart, originalEnd,
                        maxRange, clampedEnd - clampedStart, clampedStart, clampedEnd);
                }
                
                _logger.LogInformation("Backfilling gap for {Symbol} {Interval} from {Start} to {End}", 
                    workItem.Symbol, workItem.Interval, clampedStart, clampedEnd);

                var backfillCandles = await _candleService.GetCandlesAsync(
                    workItem.Symbol,
                    workItem.Interval,
                    startTime: clampedStart,
                    endTime: clampedEnd,
                    limit: 1000,
                    cancellationToken: cancellationToken);

                var backfillList = backfillCandles.ToList();
                if (backfillList.Count > 0)
                {
                    // Add to ring buffer if subscription still exists
                    if (_subscriptions.TryGetValue(backfillKey, out var subscription))
                    {
                        subscription.RingBuffer.AddRange(backfillList);
                        _logger.LogInformation("Backfilled {Count} candles for {Symbol} {Interval}", 
                            backfillList.Count, workItem.Symbol, workItem.Interval);
                    }

                    // Persist if enabled (also enqueue to avoid blocking)
                    if (_chartingOptions.EnablePersistence && _candleRepository != null)
                    {
                        EnqueueWork(new BackgroundWorkItem
                        {
                            Type = BackgroundWorkType.PersistCandles,
                            Symbol = workItem.Symbol,
                            Interval = workItem.Interval,
                            Candles = backfillList
                        });
                    }
                }
                else
                {
                    _logger.LogDebug("No candles returned from backfill for {Symbol} {Interval}", 
                        workItem.Symbol, workItem.Interval);
                }
            }
        }
        finally
        {
            // Remove backfill lock
            _activeBackfills.TryRemove(backfillKey, out _);
        }
    }

    private void EnqueueWork(BackgroundWorkItem workItem)
    {
        bool enqueued = false;
        
        if (workItem.Type == BackgroundWorkType.BackfillGap)
        {
            // Check global rate limit for backfills
            if (!CheckGlobalBackfillRateLimit())
            {
                _logger.LogWarning("Global backfill rate limit exceeded for {Symbol} {Interval} - skipping enqueue. " +
                    "Max {MaxRate} backfills per minute allowed.", 
                    workItem.Symbol, workItem.Interval, MaxBackfillsPerMinute);
                return;
            }
            
            // Try to enqueue backfill work (this will wait if queue is full due to BoundedChannelFullMode.Wait)
            enqueued = _backfillQueue.Writer.TryWrite(workItem);
            
            if (!enqueued)
            {
                _logger.LogError("Failed to enqueue backfill work for {Symbol} {Interval} - this should not happen with Wait mode", 
                    workItem.Symbol, workItem.Interval);
            }
        }
        else if (workItem.Type == BackgroundWorkType.PersistCandles)
        {
            // Try to enqueue persist work (oldest items will be dropped if queue is full)
            enqueued = _persistQueue.Writer.TryWrite(workItem);
            
            if (!enqueued)
            {
                _logger.LogDebug("Persist queue full, oldest persist work dropped for {Symbol} {Interval}", 
                    workItem.Symbol, workItem.Interval);
            }
        }
    }

    private bool CheckGlobalBackfillRateLimit()
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            
            // Remove timestamps older than 1 minute
            while (_globalBackfillTimestamps.Count > 0 && _globalBackfillTimestamps.Peek() < oneMinuteAgo)
            {
                _globalBackfillTimestamps.Dequeue();
            }
            
            // Check if we've exceeded the rate limit
            if (_globalBackfillTimestamps.Count >= MaxBackfillsPerMinute)
            {
                return false;
            }
            
            // Add current timestamp and allow the backfill
            _globalBackfillTimestamps.Enqueue(now);
            return true;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WebSocket Subscription Service stopping...");
        
        // Unsubscribe from all active subscriptions
        foreach (var kvp in _socketSubscriptions)
        {
            try
            {
                await kvp.Value.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing subscription {Key}", kvp.Key);
            }
        }
        
        _socketSubscriptions.Clear();
        _subscriptions.Clear();
        
        await base.StopAsync(cancellationToken);
    }

    public async Task<bool> SubscribeAsync(string symbol, string interval, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            throw new ArgumentException("Interval is required", nameof(interval));
        }

        var key = GetSubscriptionKey(symbol, interval);

        // Check if already subscribed
        if (_subscriptions.ContainsKey(key))
        {
            _logger.LogDebug("Already subscribed to {Symbol} {Interval}", symbol, interval);
            return false;
        }

        await _subscriptionLock.WaitAsync(cancellationToken);
        try
        {
            if (_socketClient == null)
            {
                _logger.LogError("Socket client not initialized");
                return false;
            }

            // Parse interval to BitgetStreamKlineIntervalV2
            var streamInterval = ParseToStreamInterval(interval);

            // Subscribe to kline updates
            var result = await _socketClient.SpotApiV2.SubscribeToKlineUpdatesAsync(
                symbol,
                streamInterval,
                data =>
                {
                    try
                    {
                        // Handle incoming candle updates
                        if (data.Data != null && data.Data.Length > 0)
                        {
                            var klineUpdate = data.Data[0];
                            var candle = new CandleDto
                            {
                                OpenTime = klineUpdate.OpenTime,
                                Open = klineUpdate.OpenPrice,
                                High = klineUpdate.HighPrice,
                                Low = klineUpdate.LowPrice,
                                Close = klineUpdate.ClosePrice,
                                Volume = klineUpdate.Volume,
                                QuoteVolume = klineUpdate.QuoteVolume
                            };

                            // Update subscription with latest candle (thread-safe)
                            if (_subscriptions.TryGetValue(key, out var subscription))
                            {
                                subscription.LatestCandle = candle;
                                
                                // Update ring buffer
                                subscription.RingBuffer.AddOrUpdate(candle);
                                
                                _logger.LogTrace("Updated candle for {Symbol} {Interval}: OpenTime={OpenTime}, Close={Close}", 
                                    symbol, interval, candle.OpenTime, candle.Close);

                                // Gap detection: only check if incoming candle is strictly newer than last processed
                                if (_chartingOptions.EnableGapDetection && subscription.LastOpenTime.HasValue)
                                {
                                    // Only detect gaps when new candle OpenTime is strictly greater than last
                                    if (candle.OpenTime > subscription.LastOpenTime.Value)
                                    {
                                        var expectedDuration = ParseIntervalToTimeSpan(interval);
                                        var timeSinceLastCandle = candle.OpenTime - subscription.LastOpenTime.Value;
                                        
                                        // Gap threshold: require at least 2x the expected interval to reduce false positives
                                        // This accounts for irregular update timing and minor network delays
                                        var gapThreshold = expectedDuration + expectedDuration;
                                        
                                        if (timeSinceLastCandle >= gapThreshold)
                                        {
                                            var backfillKey = GetSubscriptionKey(symbol, interval);
                                            
                                            // Check if backfill is already active to prevent re-queueing
                                            if (_activeBackfills.ContainsKey(backfillKey))
                                            {
                                                _logger.LogTrace("Backfill already active for {Symbol} {Interval}, skipping gap enqueue", 
                                                    symbol, interval);
                                            }
                                            else
                                            {
                                                // Cooldown: don't backfill more than once per 60 seconds per subscription
                                                var now = DateTime.UtcNow;
                                                var cooldownPeriod = TimeSpan.FromSeconds(60);
                                                
                                                if (!subscription.LastBackfillTime.HasValue || 
                                                    (now - subscription.LastBackfillTime.Value) >= cooldownPeriod)
                                                {
                                                    _logger.LogDebug("Gap detected for {Symbol} {Interval}: {Gap} between {Last} and {Current}", 
                                                        symbol, interval, timeSinceLastCandle, subscription.LastOpenTime.Value, candle.OpenTime);
                                                    
                                                    subscription.LastBackfillTime = now;
                                                    
                                                    EnqueueWork(new BackgroundWorkItem
                                                    {
                                                        Type = BackgroundWorkType.BackfillGap,
                                                        Symbol = symbol,
                                                        Interval = interval,
                                                        GapStart = subscription.LastOpenTime.Value + expectedDuration,
                                                        GapEnd = candle.OpenTime
                                                    });
                                                }
                                                else
                                                {
                                                    _logger.LogTrace("Skipping gap backfill for {Symbol} {Interval} due to cooldown", symbol, interval);
                                                }
                                            }
                                        }
                                        
                                        // Update last open time for next gap detection
                                        subscription.LastOpenTime = candle.OpenTime;
                                    }
                                    else if (candle.OpenTime == subscription.LastOpenTime.Value)
                                    {
                                        // Same candle update - this is normal, just updating Close/Volume etc.
                                        _logger.LogTrace("Received update for same candle at {OpenTime} for {Symbol} {Interval}", 
                                            candle.OpenTime, symbol, interval);
                                    }
                                    else
                                    {
                                        // Out-of-order candle (older than last) - skip gap detection but allow ring buffer update
                                        _logger.LogTrace("Received out-of-order candle at {OpenTime} (last was {LastOpenTime}) for {Symbol} {Interval}", 
                                            candle.OpenTime, subscription.LastOpenTime.Value, symbol, interval);
                                    }
                                }
                                else if (_chartingOptions.EnableGapDetection && !subscription.LastOpenTime.HasValue)
                                {
                                    // First candle after subscription - initialize LastOpenTime
                                    subscription.LastOpenTime = candle.OpenTime;
                                    _logger.LogDebug("Initialized LastOpenTime to {OpenTime} for {Symbol} {Interval}", 
                                        candle.OpenTime, symbol, interval);
                                }
                                else
                                {
                                    // Gap detection disabled - still update LastOpenTime for consistency
                                    subscription.LastOpenTime = candle.OpenTime;
                                }

                                // Enqueue persistence work
                                if (_chartingOptions.EnablePersistence && _candleRepository != null)
                                {
                                    EnqueueWork(new BackgroundWorkItem
                                    {
                                        Type = BackgroundWorkType.PersistCandles,
                                        Symbol = symbol,
                                        Interval = interval,
                                        Candles = new List<CandleDto> { candle }
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing candle update for {Symbol} {Interval}", 
                            symbol, interval);
                    }
                },
                cancellationToken);

            if (result.Success)
            {
                var subscription = new CandleSubscription
                {
                    Symbol = symbol,
                    Interval = interval,
                    SubscribedAt = DateTime.UtcNow,
                    RingBuffer = new CandleRingBuffer(_chartingOptions.BufferSize)
                };

                _subscriptions.TryAdd(key, subscription);
                _socketSubscriptions.TryAdd(key, result.Data);
                
                _logger.LogInformation("Successfully subscribed to {Symbol} {Interval}", symbol, interval);
                
                // Initialize buffer with recent candles (async, don't block)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await InitializeBufferAsync(symbol, interval, subscription);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error initializing buffer for {Symbol} {Interval}", symbol, interval);
                    }
                }, CancellationToken.None).ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        _logger.LogError(t.Exception, "Unhandled error in buffer initialization task for {Symbol} {Interval}", symbol, interval);
                    }
                }, TaskScheduler.Default);
                
                return true;
            }
            else
            {
                _logger.LogError("Failed to subscribe to {Symbol} {Interval}: {Error}", 
                    symbol, interval, result.Error?.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to {Symbol} {Interval}", symbol, interval);
            return false;
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    public async Task<bool> UnsubscribeAsync(string symbol, string interval, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(interval))
        {
            throw new ArgumentException("Interval is required", nameof(interval));
        }

        var key = GetSubscriptionKey(symbol, interval);

        await _subscriptionLock.WaitAsync(cancellationToken);
        try
        {
            // Remove from subscriptions
            var removed = _subscriptions.TryRemove(key, out _);

            // Close socket subscription if exists
            if (_socketSubscriptions.TryRemove(key, out var socketSubscription))
            {
                try
                {
                    await socketSubscription.CloseAsync();
                    _logger.LogInformation("Unsubscribed from {Symbol} {Interval}", symbol, interval);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing subscription for {Symbol} {Interval}", symbol, interval);
                }
            }

            return removed;
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    public IEnumerable<CandleSubscription> GetActiveSubscriptions()
    {
        return _subscriptions.Values.ToList();
    }

    public CandleDto? GetLatestCandle(string symbol, string interval)
    {
        var key = GetSubscriptionKey(symbol, interval);
        return _subscriptions.TryGetValue(key, out var subscription) 
            ? subscription.LatestCandle 
            : null;
    }

    public List<CandleDto> GetCandleBuffer(string symbol, string interval, int limit)
    {
        var key = GetSubscriptionKey(symbol, interval);
        if (_subscriptions.TryGetValue(key, out var subscription))
        {
            return subscription.RingBuffer.GetLatest(limit);
        }
        return new List<CandleDto>();
    }

    private async Task InitializeBufferAsync(string symbol, string interval, CandleSubscription subscription)
    {
        try
        {
            _logger.LogInformation("Initializing buffer for {Symbol} {Interval}", symbol, interval);

            var candleList = new List<CandleDto>();

            // Try to load from database first if persistence is enabled
            if (_chartingOptions.EnablePersistence && _candleRepository != null)
            {
                var dbCandles = await _candleRepository.GetCandlesAsync(
                    symbol, 
                    interval, 
                    limit: _chartingOptions.BufferSize,
                    cancellationToken: CancellationToken.None);
                
                candleList = dbCandles.ToList();
                if (candleList.Count > 0)
                {
                    subscription.RingBuffer.AddRange(candleList);
                    _logger.LogInformation("Loaded {Count} candles from database for {Symbol} {Interval}", 
                        candleList.Count, symbol, interval);
                }
            }

            // If no candles from DB, fetch from Bitget REST API
            if (candleList.Count == 0)
            {
                var candles = await _candleService.GetCandlesAsync(
                    symbol,
                    interval,
                    limit: _chartingOptions.BufferSize,
                    cancellationToken: CancellationToken.None);

                candleList = candles.ToList();
                if (candleList.Count > 0)
                {
                    subscription.RingBuffer.AddRange(candleList);
                    _logger.LogInformation("Initialized buffer with {Count} candles for {Symbol} {Interval}", 
                        candleList.Count, symbol, interval);

                    // Persist to database if enabled
                    if (_chartingOptions.EnablePersistence && _candleRepository != null)
                    {
                        await _candleRepository.UpsertCandlesAsync(symbol, interval, candleList, CancellationToken.None);
                    }
                }
            }

            // Initialize LastOpenTime to the latest candle in buffer for gap detection
            if (candleList.Count > 0 && _chartingOptions.EnableGapDetection)
            {
                var latestCandle = candleList.MaxBy(c => c.OpenTime);
                // MaxBy returns null only if source is empty, which is already checked above
                // However, to be defensive against concurrent modifications, check again
                if (latestCandle != null)
                {
                    subscription.LastOpenTime = latestCandle.OpenTime;
                    _logger.LogInformation("Initialized LastOpenTime to {OpenTime} for {Symbol} {Interval}", 
                        latestCandle.OpenTime, symbol, interval);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize buffer for {Symbol} {Interval}", symbol, interval);
        }
    }

    private string GetSubscriptionKey(string symbol, string interval)
    {
        return $"{symbol.ToUpperInvariant()}:{interval.ToLowerInvariant()}";
    }

    private BitgetStreamKlineIntervalV2 ParseToStreamInterval(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            "1m" => BitgetStreamKlineIntervalV2.OneMinute,
            "5m" => BitgetStreamKlineIntervalV2.FiveMinutes,
            "15m" => BitgetStreamKlineIntervalV2.FifteenMinutes,
            "30m" => BitgetStreamKlineIntervalV2.ThirtyMinutes,
            "1h" => BitgetStreamKlineIntervalV2.OneHour,
            "4h" => BitgetStreamKlineIntervalV2.FourHours,
            "6h" => BitgetStreamKlineIntervalV2.SixHours,
            "12h" => BitgetStreamKlineIntervalV2.TwelveHours,
            "1d" => BitgetStreamKlineIntervalV2.OneDay,
            "3d" => BitgetStreamKlineIntervalV2.ThreeDays,
            "1w" => BitgetStreamKlineIntervalV2.OneWeek,
            "1mo" or "1month" => BitgetStreamKlineIntervalV2.OneMonth,
            _ => throw new ArgumentException($"Invalid interval: {interval}. Valid values: 1m, 5m, 15m, 30m, 1h, 4h, 6h, 12h, 1d, 3d, 1w, 1mo")
        };
    }

    private TimeSpan ParseIntervalToTimeSpan(string interval)
    {
        return IntervalHelper.ParseIntervalToTimeSpan(interval);
    }
}
