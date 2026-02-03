using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Bitget.Net.Clients;
using Bitget.Net.Enums;
using Bitget.Net.Objects.Models.V2;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

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

public class WebSocketSubscriptionService : IWebSocketSubscriptionService, IHostedService
{
    private readonly ConcurrentDictionary<string, CandleSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, UpdateSubscription> _socketSubscriptions = new();
    private readonly BitgetOptions _options;
    private readonly ILogger<WebSocketSubscriptionService> _logger;
    private BitgetSocketClient? _socketClient;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private int _reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 5;
    private const int BaseBackoffSeconds = 2;

    public WebSocketSubscriptionService(
        IOptions<BitgetOptions> options,
        ILogger<WebSocketSubscriptionService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting WebSocket subscription service");
        await InitializeSocketClientAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping WebSocket subscription service");
        
        // Unsubscribe from all active subscriptions
        foreach (var kvp in _socketSubscriptions)
        {
            try
            {
                await kvp.Value.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing subscription for {Key}", kvp.Key);
            }
        }
        
        _socketSubscriptions.Clear();
        
        // Dispose socket client
        _socketClient?.Dispose();
        _socketClient = null;
    }

    private async Task InitializeSocketClientAsync()
    {
        try
        {
            var mode = _options.GetMode();
            var credentials = mode == BitgetMode.Trade ? _options.Trade : _options.ReadOnly;
            
            if (string.IsNullOrEmpty(credentials.ApiKey) || 
                string.IsNullOrEmpty(credentials.ApiSecret) || 
                string.IsNullOrEmpty(credentials.Passphrase))
            {
                // Create client without credentials for public endpoints
                _socketClient = new BitgetSocketClient();
                _logger.LogInformation("WebSocket client initialized without credentials (public endpoints only)");
            }
            else
            {
                // Create client with credentials
                _socketClient = new BitgetSocketClient(options =>
                {
                    options.ApiCredentials = new ApiCredentials(
                        credentials.ApiKey,
                        credentials.ApiSecret,
                        credentials.Passphrase
                    );
                });
                _logger.LogInformation("WebSocket client initialized with credentials");
            }
            
            _reconnectAttempts = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebSocket client");
            throw;
        }
    }

    private async Task HandleReconnectAsync()
    {
        await _reconnectLock.WaitAsync();
        try
        {
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                _logger.LogError("Max reconnect attempts ({MaxAttempts}) reached, giving up", MaxReconnectAttempts);
                return;
            }

            _reconnectAttempts++;
            var backoffSeconds = BaseBackoffSeconds * Math.Pow(2, _reconnectAttempts - 1);
            _logger.LogWarning("Attempting reconnect #{Attempt} after {Seconds} seconds", _reconnectAttempts, backoffSeconds);
            
            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds));
            
            // Dispose old client
            _socketClient?.Dispose();
            
            // Create new client
            await InitializeSocketClientAsync();
            
            // Resubscribe to all active subscriptions
            var subscriptionsToRestore = _subscriptions.Values.ToList();
            foreach (var subscription in subscriptionsToRestore)
            {
                try
                {
                    await SubscribeToKlineAsync(subscription.Symbol, subscription.Interval);
                    _logger.LogInformation("Restored subscription for {Symbol}:{Interval}", subscription.Symbol, subscription.Interval);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore subscription for {Symbol}:{Interval}", subscription.Symbol, subscription.Interval);
                }
            }
        }
        finally
        {
            _reconnectLock.Release();
        }
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

        var subscription = new CandleSubscription
        {
            Symbol = symbol,
            Interval = interval,
            SubscribedAt = DateTime.UtcNow
        };

        var added = _subscriptions.TryAdd(key, subscription);
        
        if (added)
        {
            try
            {
                await SubscribeToKlineAsync(symbol, interval);
                _logger.LogInformation("Subscribed to kline updates for {Symbol}:{Interval}", symbol, interval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to kline updates for {Symbol}:{Interval}", symbol, interval);
                _subscriptions.TryRemove(key, out _);
                throw;
            }
        }

        return added;
    }

    private async Task SubscribeToKlineAsync(string symbol, string interval)
    {
        if (_socketClient == null)
        {
            throw new InvalidOperationException("Socket client is not initialized");
        }

        try
        {
            var socketInterval = ParseIntervalToSocketEnum(interval);
            var key = GetSubscriptionKey(symbol, interval);

            _logger.LogInformation("Attempting to subscribe to {Symbol}:{Interval} (mapped to {SocketInterval})", 
                symbol, interval, socketInterval);

            var result = await _socketClient.SpotApiV2.SubscribeToKlineUpdatesAsync(
                symbol,
                socketInterval,
                data => HandleKlineUpdate(symbol, interval, data));

            if (!result.Success)
            {
                var errorMsg = result.Error?.Message ?? "No error message";
                var errorCode = result.Error?.Code;
                _logger.LogError("Failed to subscribe to kline updates for {Symbol}:{Interval}. Error: {Error}, Code: {Code}", 
                    symbol, interval, errorMsg, errorCode);
                throw new BitgetApiException($"Failed to subscribe to kline updates: {errorMsg}. Check logs for details.");
            }

            // Store the socket subscription for cleanup
            _socketSubscriptions.TryAdd(key, result.Data);
            
            // Reset reconnect attempts on successful subscription
            _reconnectAttempts = 0;
            
            // Handle connection lost event
            result.Data.ConnectionLost += () =>
            {
                _logger.LogWarning("WebSocket connection lost for {Symbol}:{Interval}", symbol, interval);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleReconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Reconnect failed for {Symbol}:{Interval}", symbol, interval);
                    }
                });
            };

            _logger.LogInformation("Successfully subscribed to {Symbol}:{Interval}", symbol, interval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during subscription to {Symbol}:{Interval}", symbol, interval);
            throw;
        }
    }

    private void HandleKlineUpdate(string symbol, string interval, DataEvent<BitgetKlineUpdate[]> data)
    {
        try
        {
            if (data.Data == null || data.Data.Length == 0)
            {
                return;
            }

            // Get the first kline update (latest)
            var kline = data.Data[0];
            var key = GetSubscriptionKey(symbol, interval);

            if (_subscriptions.TryGetValue(key, out var subscription))
            {
                subscription.LatestCandle = new CandleDto
                {
                    OpenTime = kline.OpenTime,
                    Open = kline.OpenPrice,
                    High = kline.HighPrice,
                    Low = kline.LowPrice,
                    Close = kline.ClosePrice,
                    Volume = kline.Volume,
                    QuoteVolume = kline.QuoteVolume
                };

                _logger.LogDebug("Updated latest candle for {Symbol}:{Interval} - Close: {Close}", 
                    symbol, interval, kline.ClosePrice);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling kline update for {Symbol}:{Interval}", symbol, interval);
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

        var removed = _subscriptions.TryRemove(key, out _);

        if (removed && _socketSubscriptions.TryRemove(key, out var socketSub))
        {
            try
            {
                await socketSub.CloseAsync();
                _logger.LogInformation("Unsubscribed from kline updates for {Symbol}:{Interval}", symbol, interval);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unsubscribing from kline updates for {Symbol}:{Interval}", symbol, interval);
            }
        }

        return removed;
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

    private BitgetStreamKlineIntervalV2 ParseIntervalToSocketEnum(string interval)
    {
        // Map string intervals to Bitget.Net socket enum
        return interval switch
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
            _ => throw new ArgumentException($"Invalid interval: {interval}. Valid values: 1m, 5m, 15m, 30m, 1h, 4h, 6h, 12h, 1d, 3d, 1w, 1mo (or 1month)")
        };
    }
}
