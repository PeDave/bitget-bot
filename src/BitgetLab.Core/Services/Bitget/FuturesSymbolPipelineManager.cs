using System.Collections.Concurrent;
using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Manages per-symbol futures data pipelines
/// </summary>
public interface IFuturesSymbolPipelineManager
{
    /// <summary>
    /// Start a pipeline for a symbol
    /// </summary>
    Task<PipelineStatus> StartAsync(string symbol, string market, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop a pipeline for a symbol
    /// </summary>
    Task<PipelineStatus> StopAsync(string symbol, string market);

    /// <summary>
    /// Get status of a pipeline
    /// </summary>
    PipelineStatus GetStatus(string symbol, string market);
}

/// <summary>
/// Implementation of futures symbol pipeline manager
/// </summary>
public class FuturesSymbolPipelineManager : IFuturesSymbolPipelineManager
{
    private readonly ILogger<FuturesSymbolPipelineManager> _logger;
    private readonly PipelineOptions _options;
    private readonly ICandleService _candleService;
    private readonly ICandleRepository _candleRepository;
    private readonly IWebSocketSubscriptionService _wsService;

    private readonly ConcurrentDictionary<string, PipelineContext> _pipelines = new();

    private class PipelineContext
    {
        public PipelineStatus Status { get; set; } = new();
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public Task? BackfillTask { get; set; }
        public Task? RestSyncTask { get; set; }
    }

    public FuturesSymbolPipelineManager(
        ILogger<FuturesSymbolPipelineManager> logger,
        IOptions<PipelineOptions> options,
        ICandleService candleService,
        ICandleRepository candleRepository,
        IWebSocketSubscriptionService wsService)
    {
        _logger = logger;
        _options = options.Value;
        _candleService = candleService;
        _candleRepository = candleRepository;
        _wsService = wsService;
    }

    public async Task<PipelineStatus> StartAsync(string symbol, string market, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Pipeline feature is disabled in configuration");
        }

        // Normalize symbol
        symbol = symbol.Trim().ToUpperInvariant();
        market = market.Trim().ToLowerInvariant();

        var key = GetKey(symbol, market);

        // Check if already running
        if (_pipelines.TryGetValue(key, out var existing))
        {
            if (existing.Status.State == PipelineState.Running || existing.Status.State == PipelineState.Backfilling)
            {
                throw new InvalidOperationException($"Pipeline already running for {symbol} {market}");
            }
        }

        // Create new pipeline context
        var cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        var allIntervals = _options.WsIntervals.Concat(_options.RestIntervals).Distinct().ToArray();

        var context = new PipelineContext
        {
            Status = new PipelineStatus
            {
                Symbol = symbol,
                Market = market,
                State = PipelineState.Backfilling,
                Progress = "Starting backfill...",
                Intervals = allIntervals,
                CompletedIntervals = Array.Empty<string>(),
                StartedAt = DateTime.UtcNow
            },
            CancellationTokenSource = cts
        };

        _pipelines[key] = context;

        // Start backfill task
        context.BackfillTask = Task.Run(async () =>
        {
            try
            {
                await RunBackfillAsync(symbol, market, context, linkedCts.Token);
                
                // Check if backfill completed with errors or failed completely
                if (context.Status.State == PipelineState.Failed)
                {
                    // All intervals failed, don't proceed to live updates
                    _logger.LogError("Backfill failed for all intervals for {Symbol} {Market}, not starting live updates", symbol, market);
                    return;
                }
                
                // Start live updates even if some intervals failed (CompletedWithErrors state)
                // This allows the pipeline to continue tracking successful intervals
                await StartLiveUpdatesAsync(symbol, market, context, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Pipeline cancelled for {Symbol} {Market}", symbol, market);
                context.Status.State = PipelineState.Stopped;
                context.Status.Progress = "Cancelled";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline failed for {Symbol} {Market}", symbol, market);
                context.Status.State = PipelineState.Failed;
                context.Status.ErrorMessage = ex.Message;
                context.Status.Progress = "Failed";
            }
        }, linkedCts.Token);

        return context.Status;
    }

    public async Task<PipelineStatus> StopAsync(string symbol, string market)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        market = market.Trim().ToLowerInvariant();

        var key = GetKey(symbol, market);

        if (!_pipelines.TryGetValue(key, out var context))
        {
            return new PipelineStatus
            {
                Symbol = symbol,
                Market = market,
                State = PipelineState.Stopped,
                Progress = "Not running"
            };
        }

        // Cancel the pipeline
        context.CancellationTokenSource?.Cancel();

        // Unsubscribe from WebSocket
        foreach (var interval in _options.WsIntervals)
        {
            try
            {
                await _wsService.UnsubscribeAsync(symbol, interval);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unsubscribe {Symbol} {Interval}", symbol, interval);
            }
        }

        context.Status.State = PipelineState.Stopped;
        context.Status.Progress = "Stopped";

        return context.Status;
    }

    public PipelineStatus GetStatus(string symbol, string market)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        market = market.Trim().ToLowerInvariant();

        var key = GetKey(symbol, market);

        if (_pipelines.TryGetValue(key, out var context))
        {
            return context.Status;
        }

        return new PipelineStatus
        {
            Symbol = symbol,
            Market = market,
            State = PipelineState.Stopped,
            Progress = "Not running"
        };
    }

    private async Task RunBackfillAsync(string symbol, string market, PipelineContext context, CancellationToken cancellationToken)
    {
        var marketType = market == "futures" ? MarketType.Futures : MarketType.Spot;

        // Sequential backfill for each interval - use configured intervals
        var intervals = _options.WsIntervals.Concat(_options.RestIntervals).Distinct().ToArray();
        var completed = new List<string>();
        var intervalErrors = new Dictionary<string, string>();

        foreach (var interval in intervals)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_options.LookbackDays.TryGetValue(interval, out var lookbackDays))
            {
                _logger.LogWarning("No lookback days configured for interval {Interval}, skipping", interval);
                continue;
            }

            context.Status.Progress = $"Backfilling {interval} ({lookbackDays} days)...";
            _logger.LogInformation("Starting backfill for {Symbol} {Interval} ({LookbackDays} days)", symbol, interval, lookbackDays);

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddDays(-lookbackDays);

            try
            {
                // Fetch candles using existing CandleService
                var candles = await _candleService.GetCandlesAsync(
                    symbol,
                    interval,
                    startTime,
                    endTime,
                    market: marketType,
                    cancellationToken: cancellationToken);

                var candleList = candles.ToList();
                _logger.LogInformation("Fetched {Count} candles for {Symbol} {Interval}", candleList.Count, symbol, interval);

                // Persist to database
                if (candleList.Count > 0)
                {
                    await _candleRepository.UpsertCandlesAsync(symbol, interval, candleList, marketType, cancellationToken);
                    _logger.LogInformation("Persisted {Count} candles for {Symbol} {Interval}", candleList.Count, symbol, interval);
                }

                // Apply retention
                var cutoffTime = DateTime.UtcNow.AddDays(-lookbackDays);
                await _candleRepository.TrimRetentionAsync(symbol, interval, marketType, cutoffTime, _options.MaxRows, cancellationToken);

                completed.Add(interval);
                context.Status.CompletedIntervals = completed.ToArray();
                _logger.LogInformation("Completed backfill for {Symbol} {Interval}", symbol, interval);
            }
            catch (Exception ex)
            {
                var errorMsg = $"{ex.GetType().Name}: {ex.Message}";
                intervalErrors[interval] = errorMsg;
                context.Status.IntervalErrors = intervalErrors;
                context.Status.ErrorMessage = errorMsg; // Update last error
                
                _logger.LogError(ex, "Failed to backfill {Symbol} {Interval}. Continuing with remaining intervals.", symbol, interval);
                // Continue with next interval instead of throwing
            }
        }

        // Determine final state based on results
        if (intervalErrors.Count == 0)
        {
            // All intervals completed successfully
            context.Status.Progress = "Backfill complete";
        }
        else if (completed.Count > 0)
        {
            // Some intervals completed, some failed
            context.Status.Progress = $"Backfill completed with errors ({completed.Count}/{intervals.Length} intervals succeeded)";
            context.Status.State = PipelineState.CompletedWithErrors;
        }
        else
        {
            // All intervals failed
            context.Status.Progress = "Backfill failed for all intervals";
            context.Status.State = PipelineState.Failed;
        }
    }

    private async Task StartLiveUpdatesAsync(string symbol, string market, PipelineContext context, CancellationToken cancellationToken)
    {
        var marketType = market == "futures" ? MarketType.Futures : MarketType.Spot;

        // Subscribe to WebSocket for small intervals
        foreach (var interval in _options.WsIntervals)
        {
            try
            {
                await _wsService.SubscribeAsync(symbol, interval);
                _logger.LogInformation("Subscribed to WebSocket for {Symbol} {Interval}", symbol, interval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to WebSocket for {Symbol} {Interval}", symbol, interval);
            }
        }

        // Start REST sync task for large intervals
        context.RestSyncTask = Task.Run(async () =>
        {
            await RunRestSyncLoopAsync(symbol, market, marketType, cancellationToken);
        }, cancellationToken);

        context.Status.State = PipelineState.Running;
        context.Status.Progress = "Running - WS active, REST sync scheduled";
    }

    private async Task RunRestSyncLoopAsync(string symbol, string market, MarketType marketType, CancellationToken cancellationToken)
    {
        var syncInterval = TimeSpan.FromMinutes(_options.SyncEveryMinutes);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(syncInterval, cancellationToken);

                _logger.LogInformation("Starting REST sync for {Symbol} {Market}", symbol, market);

                foreach (var interval in _options.RestIntervals)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!_options.LookbackDays.TryGetValue(interval, out var lookbackDays))
                    {
                        continue;
                    }

                    try
                    {
                        var endTime = DateTime.UtcNow;
                        // Sync recent data - use a small window relative to the interval's lookback
                        var syncWindow = Math.Min(lookbackDays, 7); // Sync last 7 days or less
                        var startTime = endTime.AddDays(-syncWindow);

                        var candles = await _candleService.GetCandlesAsync(
                            symbol,
                            interval,
                            startTime,
                            endTime,
                            market: marketType,
                            cancellationToken: cancellationToken);

                        var candleList = candles.ToList();
                        if (candleList.Count > 0)
                        {
                            await _candleRepository.UpsertCandlesAsync(symbol, interval, candleList, marketType, cancellationToken);
                            
                            // Apply retention
                            var cutoffTime = DateTime.UtcNow.AddDays(-lookbackDays);
                            await _candleRepository.TrimRetentionAsync(symbol, interval, marketType, cutoffTime, _options.MaxRows, cancellationToken);
                            
                            _logger.LogDebug("REST sync updated {Count} candles for {Symbol} {Interval}", candleList.Count, symbol, interval);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to sync {Symbol} {Interval} via REST", symbol, interval);
                    }
                }

                _logger.LogInformation("Completed REST sync for {Symbol} {Market}", symbol, market);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in REST sync loop for {Symbol} {Market}", symbol, market);
            }
        }
    }

    private static string GetKey(string symbol, string market) => $"{symbol}_{market}";
}
