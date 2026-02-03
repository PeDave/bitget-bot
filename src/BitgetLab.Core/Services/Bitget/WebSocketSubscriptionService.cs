using BitgetLab.Core.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
}

public class WebSocketSubscriptionService : BackgroundService, IWebSocketSubscriptionService
{
    private readonly ConcurrentDictionary<string, CandleSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, UpdateSubscription> _socketSubscriptions = new();
    private readonly IBitgetSocketClientFactory _socketClientFactory;
    private readonly ILogger<WebSocketSubscriptionService> _logger;
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private volatile IBitgetSocketClient? _socketClient;

    public WebSocketSubscriptionService(
        IBitgetSocketClientFactory socketClientFactory,
        ILogger<WebSocketSubscriptionService> logger)
    {
        _socketClientFactory = socketClientFactory;
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
                                _logger.LogDebug("Updated candle for {Symbol} {Interval}: Close={Close}", 
                                    symbol, interval, candle.Close);
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
                    SubscribedAt = DateTime.UtcNow
                };

                _subscriptions.TryAdd(key, subscription);
                _socketSubscriptions.TryAdd(key, result.Data);
                
                _logger.LogInformation("Successfully subscribed to {Symbol} {Interval}", symbol, interval);
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
