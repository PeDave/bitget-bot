using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using System.Collections.Concurrent;
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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebSocket Subscription Service starting...");
        
        try
        {
            // Initialize socket client
            _socketClient = _socketClientFactory.CreateSocketClient();
            _logger.LogInformation("WebSocket client initialized");

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                
                // Log active subscriptions count
                var activeCount = _subscriptions.Count;
                if (activeCount > 0)
                {
                    _logger.LogDebug("Active WebSocket subscriptions: {Count}", activeCount);
                }
            }
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
                                
                                _logger.LogDebug("Updated candle for {Symbol} {Interval}: Close={Close}", 
                                    symbol, interval, candle.Close);

                                // Gap detection and backfill (async, don't block)
                                if (_chartingOptions.EnableGapDetection)
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await DetectAndBackfillGapsAsync(symbol, interval, subscription);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Error in gap detection for {Symbol} {Interval}", symbol, interval);
                                        }
                                    });
                                }

                                // Persist to database if enabled
                                if (_chartingOptions.EnablePersistence && _candleRepository != null)
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await _candleRepository.UpsertCandleAsync(symbol, interval, candle, CancellationToken.None);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Error persisting candle for {Symbol} {Interval}", symbol, interval);
                                        }
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
                });
                
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

            // Try to load from database first if persistence is enabled
            if (_chartingOptions.EnablePersistence && _candleRepository != null)
            {
                var dbCandles = await _candleRepository.GetCandlesAsync(
                    symbol, 
                    interval, 
                    limit: _chartingOptions.BufferSize,
                    cancellationToken: CancellationToken.None);
                
                var dbCandleList = dbCandles.ToList();
                if (dbCandleList.Count > 0)
                {
                    subscription.RingBuffer.AddRange(dbCandleList);
                    _logger.LogInformation("Loaded {Count} candles from database for {Symbol} {Interval}", 
                        dbCandleList.Count, symbol, interval);
                    return;
                }
            }

            // Otherwise, fetch from Bitget REST API
            var candles = await _candleService.GetCandlesAsync(
                symbol,
                interval,
                limit: _chartingOptions.BufferSize,
                cancellationToken: CancellationToken.None);

            var candleList = candles.ToList();
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize buffer for {Symbol} {Interval}", symbol, interval);
        }
    }

    private async Task DetectAndBackfillGapsAsync(string symbol, string interval, CandleSubscription subscription)
    {
        try
        {
            var gaps = subscription.RingBuffer.DetectGaps(interval);
            
            if (gaps.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Detected {Count} gaps for {Symbol} {Interval}", gaps.Count, symbol, interval);

            foreach (var gap in gaps)
            {
                try
                {
                    _logger.LogInformation("Backfilling gap for {Symbol} {Interval} from {Start} to {End}", 
                        symbol, interval, gap.Start, gap.End);

                    var backfillCandles = await _candleService.GetCandlesAsync(
                        symbol,
                        interval,
                        startTime: gap.Start,
                        endTime: gap.End,
                        limit: 1000,
                        cancellationToken: CancellationToken.None);

                    var backfillList = backfillCandles.ToList();
                    if (backfillList.Count > 0)
                    {
                        subscription.RingBuffer.AddRange(backfillList);
                        _logger.LogInformation("Backfilled {Count} candles for {Symbol} {Interval}", 
                            backfillList.Count, symbol, interval);

                        // Persist backfilled candles if enabled
                        if (_chartingOptions.EnablePersistence && _candleRepository != null)
                        {
                            await _candleRepository.UpsertCandlesAsync(symbol, interval, backfillList, CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backfill gap for {Symbol} {Interval}", symbol, interval);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in gap detection for {Symbol} {Interval}", symbol, interval);
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
}
