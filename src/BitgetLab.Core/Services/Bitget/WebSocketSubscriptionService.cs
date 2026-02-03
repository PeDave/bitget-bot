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
    private readonly Channel<BackgroundWorkItem> _workQueue;

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
        
        // Create bounded channel with capacity of 10000 work items
        _workQueue = Channel.CreateBounded<BackgroundWorkItem>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
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

            // Start background worker task
            var workerTask = Task.Run(() => ProcessWorkQueueAsync(stoppingToken), stoppingToken);

            // Keep the service running and log metrics periodically
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                
                // Log active subscriptions and queue depth
                var activeCount = _subscriptions.Count;
                var queueCount = _workQueue.Reader.Count;
                
                if (activeCount > 0 || queueCount > 0)
                {
                    _logger.LogDebug("Active WebSocket subscriptions: {Count}, Queue depth: {QueueDepth}", 
                        activeCount, queueCount);
                }
            }
            
            await workerTask;
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

    private async Task ProcessWorkQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background work queue processor starting...");
        
        await foreach (var workItem in _workQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                switch (workItem.Type)
                {
                    case BackgroundWorkType.PersistCandles:
                        if (_chartingOptions.EnablePersistence && _candleRepository != null && workItem.Candles != null)
                        {
                            await _candleRepository.UpsertCandlesAsync(
                                workItem.Symbol, 
                                workItem.Interval, 
                                workItem.Candles, 
                                cancellationToken);
                        }
                        break;
                        
                    case BackgroundWorkType.BackfillGap:
                        await ProcessBackfillAsync(workItem, cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing work item of type {Type} for {Symbol} {Interval}", 
                    workItem.Type, workItem.Symbol, workItem.Interval);
            }
        }
        
        _logger.LogInformation("Background work queue processor stopped");
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
                _logger.LogInformation("Backfilling gap for {Symbol} {Interval} from {Start} to {End}", 
                    workItem.Symbol, workItem.Interval, workItem.GapStart, workItem.GapEnd);

                var backfillCandles = await _candleService.GetCandlesAsync(
                    workItem.Symbol,
                    workItem.Interval,
                    startTime: workItem.GapStart,
                    endTime: workItem.GapEnd,
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
        if (!_workQueue.Writer.TryWrite(workItem))
        {
            _logger.LogWarning("Failed to enqueue work item of type {Type} for {Symbol} {Interval} - queue may be full", 
                workItem.Type, workItem.Symbol, workItem.Interval);
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
                async data =>
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
                                        var gapThreshold = TimeSpan.FromTicks(expectedDuration.Ticks * 2);
                                        
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

            List<CandleDto> candleList = new();

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

            // Otherwise, fetch from Bitget REST API
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
                var latestCandle = candleList.OrderByDescending(c => c.OpenTime).FirstOrDefault();
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
        return interval.ToLowerInvariant() switch
        {
            "1m" => TimeSpan.FromMinutes(1),
            "5m" => TimeSpan.FromMinutes(5),
            "15m" => TimeSpan.FromMinutes(15),
            "30m" => TimeSpan.FromMinutes(30),
            "1h" => TimeSpan.FromHours(1),
            "4h" => TimeSpan.FromHours(4),
            "6h" => TimeSpan.FromHours(6),
            "12h" => TimeSpan.FromHours(12),
            "1d" => TimeSpan.FromDays(1),
            "3d" => TimeSpan.FromDays(3),
            "1w" => TimeSpan.FromDays(7),
            // Note: Using 30 days as approximation for 1 month interval
            // This may cause minor gap detection inaccuracies for months with 28, 29, or 31 days
            "1mo" or "1month" => TimeSpan.FromDays(30),
            _ => TimeSpan.FromMinutes(1)
        };
    }
}
