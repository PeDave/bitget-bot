using BitgetLab.Core.Models;
using BitgetLab.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for managing candle data retention policies
/// </summary>
public interface ICandleRetentionService
{
    /// <summary>
    /// Apply retention policy to a symbol/interval combination
    /// Deletes candles older than lookback window and enforces max rows cap
    /// </summary>
    Task<(int deletedByTime, int deletedByMaxRows)> ApplyRetentionPolicyAsync(
        string symbol,
        string interval,
        MarketType market = MarketType.Futures,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the cutoff time for a given interval based on lookback days
    /// </summary>
    DateTime CalculateCutoffTime(string interval);
}

public class CandleRetentionService : ICandleRetentionService
{
    private readonly ICandleRepository? _candleRepository;
    private readonly FuturesPipelineOptions _pipelineOptions;
    private readonly ILogger<CandleRetentionService> _logger;

    public CandleRetentionService(
        IOptions<FuturesPipelineOptions> pipelineOptions,
        ILogger<CandleRetentionService> logger,
        ICandleRepository? candleRepository = null)
    {
        _pipelineOptions = pipelineOptions.Value;
        _logger = logger;
        _candleRepository = candleRepository;
    }

    public async Task<(int deletedByTime, int deletedByMaxRows)> ApplyRetentionPolicyAsync(
        string symbol,
        string interval,
        MarketType market = MarketType.Futures,
        CancellationToken cancellationToken = default)
    {
        if (_candleRepository == null)
        {
            _logger.LogDebug("Candle repository not available, skipping retention policy");
            return (0, 0);
        }

        var deletedByTime = 0;
        var deletedByMaxRows = 0;

        try
        {
            // Step 1: Delete candles older than the lookback window
            var cutoffTime = CalculateCutoffTime(interval);
            deletedByTime = await _candleRepository.DeleteCandlesOlderThanAsync(
                symbol, interval, cutoffTime, market, cancellationToken);

            // Step 2: Enforce max rows cap (delete oldest rows beyond limit)
            var currentCount = await _candleRepository.GetCandleCountAsync(
                symbol, interval, market, cancellationToken);

            if (currentCount > _pipelineOptions.MaxRows)
            {
                deletedByMaxRows = await _candleRepository.DeleteCandlesBeyondMaxRowsAsync(
                    symbol, interval, _pipelineOptions.MaxRows, market, cancellationToken);
            }

            if (deletedByTime > 0 || deletedByMaxRows > 0)
            {
                _logger.LogInformation(
                    "Retention policy applied for {Symbol} {Interval} {Market}: " +
                    "deleted {DeletedByTime} old candles, {DeletedByMaxRows} beyond max rows",
                    symbol, interval, market.ToStringValue(), deletedByTime, deletedByMaxRows);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to apply retention policy for {Symbol} {Interval} {Market}",
                symbol, interval, market.ToStringValue());
        }

        return (deletedByTime, deletedByMaxRows);
    }

    public DateTime CalculateCutoffTime(string interval)
    {
        // Get lookback days from configuration
        if (_pipelineOptions.LookbackDays.TryGetValue(interval, out var lookbackDays))
        {
            return DateTime.UtcNow.AddDays(-lookbackDays);
        }

        // Default fallback: 90 days if interval not found in config
        _logger.LogWarning(
            "Interval {Interval} not found in LookbackDays configuration, using default 90 days",
            interval);
        return DateTime.UtcNow.AddDays(-90);
    }
}
