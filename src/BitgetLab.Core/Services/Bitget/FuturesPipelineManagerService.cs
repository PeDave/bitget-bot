using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for managing per-symbol futures data pipelines
/// </summary>
public interface IPipelineManagerService
{
    /// <summary>
    /// Start a pipeline for a symbol
    /// </summary>
    Task<bool> StartPipelineAsync(string symbol, MarketType market = MarketType.Futures, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop a pipeline for a symbol
    /// </summary>
    Task<bool> StopPipelineAsync(string symbol, MarketType market = MarketType.Futures, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get status of a pipeline
    /// </summary>
    PipelineStatus? GetPipelineStatus(string symbol, MarketType market = MarketType.Futures);

    /// <summary>
    /// Get all active pipelines
    /// </summary>
    IEnumerable<PipelineStatus> GetAllPipelines();
}

/// <summary>
/// Per-symbol pipeline instance
/// </summary>
internal class SymbolPipeline
{
    public string Symbol { get; set; } = string.Empty;
    public MarketType Market { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    public Task? BackgroundTask { get; set; }
    public PipelineStatus Status { get; set; } = new();
}

public class FuturesPipelineManagerService : BackgroundService, IPipelineManagerService
{
    private readonly ConcurrentDictionary<string, SymbolPipeline> _pipelines = new();
    private readonly ICandleService _candleService;
    private readonly IWebSocketSubscriptionService _wsService;
    private readonly ICandleRetentionService _retentionService;
    private readonly FuturesPipelineOptions _options;
    private readonly ILogger<FuturesPipelineManagerService> _logger;

    public FuturesPipelineManagerService(
        ICandleService candleService,
        IWebSocketSubscriptionService wsService,
        ICandleRetentionService retentionService,
        IOptions<FuturesPipelineOptions> options,
        ILogger<FuturesPipelineManagerService> logger)
    {
        _candleService = candleService;
        _wsService = wsService;
        _retentionService = retentionService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Futures pipeline is disabled in configuration");
            return;
        }

        _logger.LogInformation("Futures pipeline manager started");

        // Periodic REST sync loop
        var syncTimer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SyncEveryMinutes));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await syncTimer.WaitForNextTickAsync(stoppingToken);

                // Run REST sync for all active pipelines
                foreach (var pipeline in _pipelines.Values)
                {
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await PerformRestSyncAsync(pipeline, CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, 
                                    "Error in REST sync for {Symbol} {Market}",
                                    pipeline.Symbol, pipeline.Market.ToStringValue());
                            }
                        }, CancellationToken.None);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when service is stopping
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in pipeline manager main loop");
        }

        _logger.LogInformation("Futures pipeline manager stopped");
    }

    public async Task<bool> StartPipelineAsync(
        string symbol,
        MarketType market = MarketType.Futures,
        CancellationToken cancellationToken = default)
    {
        var key = GetPipelineKey(symbol, market);

        if (_pipelines.ContainsKey(key))
        {
            _logger.LogWarning("Pipeline for {Symbol} {Market} already running", symbol, market.ToStringValue());
            return false;
        }

        var pipeline = new SymbolPipeline
        {
            Symbol = symbol,
            Market = market,
            Status = new PipelineStatus
            {
                Symbol = symbol,
                Market = market,
                IsRunning = true,
                StartedAt = DateTime.UtcNow,
                ActiveWsIntervals = new List<string>(_options.WsIntervals),
                ActiveRestIntervals = new List<string>(_options.RestIntervals)
            }
        };

        if (!_pipelines.TryAdd(key, pipeline))
        {
            _logger.LogWarning("Failed to add pipeline for {Symbol} {Market}", symbol, market.ToStringValue());
            return false;
        }

        try
        {
            _logger.LogInformation("Starting pipeline for {Symbol} {Market}", symbol, market.ToStringValue());

            // Start background task for this pipeline
            pipeline.BackgroundTask = Task.Run(async () =>
            {
                try
                {
                    await RunPipelineAsync(pipeline, pipeline.CancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Pipeline task failed for {Symbol} {Market}", 
                        symbol, market.ToStringValue());
                    pipeline.Status.Errors.Add($"Pipeline task failed: {ex.Message}");
                }
            }, CancellationToken.None);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start pipeline for {Symbol} {Market}", symbol, market.ToStringValue());
            _pipelines.TryRemove(key, out _);
            return false;
        }
    }

    public async Task<bool> StopPipelineAsync(
        string symbol,
        MarketType market = MarketType.Futures,
        CancellationToken cancellationToken = default)
    {
        var key = GetPipelineKey(symbol, market);

        if (!_pipelines.TryRemove(key, out var pipeline))
        {
            _logger.LogWarning("Pipeline for {Symbol} {Market} not found", symbol, market.ToStringValue());
            return false;
        }

        try
        {
            _logger.LogInformation("Stopping pipeline for {Symbol} {Market}", symbol, market.ToStringValue());

            // Cancel the pipeline
            pipeline.CancellationTokenSource.Cancel();

            // Unsubscribe from WebSocket
            foreach (var interval in _options.WsIntervals)
            {
                try
                {
                    await _wsService.UnsubscribeAsync(symbol, interval, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Failed to unsubscribe from WS for {Symbol} {Interval}",
                        symbol, interval);
                }
            }

            // Wait for background task to complete (with timeout)
            if (pipeline.BackgroundTask != null)
            {
                await Task.WhenAny(pipeline.BackgroundTask, Task.Delay(5000, cancellationToken));
            }

            pipeline.Status.IsRunning = false;
            pipeline.CancellationTokenSource.Dispose();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop pipeline for {Symbol} {Market}", symbol, market.ToStringValue());
            return false;
        }
    }

    public PipelineStatus? GetPipelineStatus(string symbol, MarketType market = MarketType.Futures)
    {
        var key = GetPipelineKey(symbol, market);
        return _pipelines.TryGetValue(key, out var pipeline) ? pipeline.Status : null;
    }

    public IEnumerable<PipelineStatus> GetAllPipelines()
    {
        return _pipelines.Values.Select(p => p.Status).ToList();
    }

    private async Task RunPipelineAsync(SymbolPipeline pipeline, CancellationToken cancellationToken)
    {
        var symbol = pipeline.Symbol;
        var market = pipeline.Market;

        try
        {
            // Step 1: Initial backfill for all intervals
            _logger.LogInformation("Starting initial backfill for {Symbol} {Market}", symbol, market.ToStringValue());

            var allIntervals = _options.WsIntervals.Concat(_options.RestIntervals).Distinct().ToList();
            
            foreach (var interval in allIntervals)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await PerformBackfillAsync(pipeline, interval, cancellationToken);
                    pipeline.Status.LastBackfillTimes[interval] = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to backfill {Symbol} {Interval} {Market}",
                        symbol, interval, market.ToStringValue());
                    pipeline.Status.Errors.Add($"Backfill failed for {interval}: {ex.Message}");
                }
            }

            // Step 2: Subscribe to WebSocket for smaller intervals
            _logger.LogInformation("Starting WebSocket subscriptions for {Symbol} {Market}", symbol, market.ToStringValue());

            foreach (var interval in _options.WsIntervals)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var subscribed = await _wsService.SubscribeAsync(symbol, interval, cancellationToken);
                    if (subscribed)
                    {
                        _logger.LogInformation("Subscribed to WS for {Symbol} {Interval}", symbol, interval);
                        pipeline.Status.LastWsUpdate = DateTime.UtcNow;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to subscribe to WS for {Symbol} {Interval}", symbol, interval);
                        pipeline.Status.Errors.Add($"WS subscription failed for {interval}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error subscribing to WS for {Symbol} {Interval}",
                        symbol, interval);
                    pipeline.Status.Errors.Add($"WS subscription error for {interval}: {ex.Message}");
                }
            }

            // Step 3: Keep pipeline alive and update WS update time
            _logger.LogInformation("Pipeline running for {Symbol} {Market}", symbol, market.ToStringValue());

            while (!cancellationToken.IsCancellationRequested)
            {
                // Update WS status based on subscriptions
                var hasActiveWsSubscription = _options.WsIntervals.Any(interval =>
                    _wsService.GetLatestCandle(symbol, interval) != null);

                if (hasActiveWsSubscription)
                {
                    pipeline.Status.LastWsUpdate = DateTime.UtcNow;
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline cancelled for {Symbol} {Market}", symbol, market.ToStringValue());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline error for {Symbol} {Market}", symbol, market.ToStringValue());
            pipeline.Status.Errors.Add($"Pipeline error: {ex.Message}");
        }
    }

    private async Task PerformBackfillAsync(
        SymbolPipeline pipeline,
        string interval,
        CancellationToken cancellationToken)
    {
        var symbol = pipeline.Symbol;
        var market = pipeline.Market;

        // Calculate lookback period
        var lookbackDays = _options.LookbackDays.TryGetValue(interval, out var days) ? days : 90;
        var startTime = DateTime.UtcNow.AddDays(-lookbackDays);
        var endTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Backfilling {Symbol} {Interval} {Market} from {StartTime} to {EndTime} ({Days} days)",
            symbol, interval, market.ToStringValue(), startTime, endTime, lookbackDays);

        try
        {
            // Fetch candles with pagination (handled by CandleService)
            var candles = await _candleService.GetCandlesAsync(
                symbol, interval, startTime, endTime, 
                limit: 1000, market, cancellationToken);

            var candleCount = candles.Count();
            _logger.LogInformation(
                "Backfilled {Count} candles for {Symbol} {Interval} {Market}",
                candleCount, symbol, interval, market.ToStringValue());

            // Apply retention policy after backfill
            await _retentionService.ApplyRetentionPolicyAsync(symbol, interval, market, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Backfill failed for {Symbol} {Interval} {Market}",
                symbol, interval, market.ToStringValue());
            throw;
        }
    }

    private async Task PerformRestSyncAsync(SymbolPipeline pipeline, CancellationToken cancellationToken)
    {
        var symbol = pipeline.Symbol;
        var market = pipeline.Market;

        foreach (var interval in _options.RestIntervals)
        {
            try
            {
                // Fetch latest candles for this interval
                var candles = await _candleService.GetCandlesAsync(
                    symbol, interval, 
                    startTime: DateTime.UtcNow.AddDays(-7), // Fetch last 7 days to catch any gaps
                    endTime: DateTime.UtcNow,
                    limit: 1000, market, cancellationToken);

                var candleCount = candles.Count();
                _logger.LogDebug(
                    "REST sync fetched {Count} candles for {Symbol} {Interval} {Market}",
                    candleCount, symbol, interval, market.ToStringValue());

                pipeline.Status.LastRestSync = DateTime.UtcNow;

                // Apply retention policy after sync
                await _retentionService.ApplyRetentionPolicyAsync(symbol, interval, market, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "REST sync failed for {Symbol} {Interval} {Market}",
                    symbol, interval, market.ToStringValue());
                pipeline.Status.Errors.Add($"REST sync failed for {interval}: {ex.Message}");
            }
        }
    }

    private static string GetPipelineKey(string symbol, MarketType market)
    {
        return $"{symbol}_{market.ToStringValue()}";
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping all pipelines...");

        // Stop all pipelines
        var stopTasks = _pipelines.Keys
            .Select(key => StopPipelineAsync(key.Split('_')[0], MarketType.Futures, cancellationToken))
            .ToList();

        await Task.WhenAll(stopTasks);

        await base.StopAsync(cancellationToken);
    }
}
