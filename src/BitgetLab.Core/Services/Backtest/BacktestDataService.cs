using BitgetLab.Core.Models;
using BitgetLab.Core.Services.Bitget;
using Microsoft.Extensions.Logging;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Service for preparing data for backtests with proper warmup periods
/// </summary>
public interface IBacktestDataService
{
    /// <summary>
    /// Get candles with lookback period for indicator warmup
    /// </summary>
    Task<List<CandleDto>> GetCandlesWithLookbackAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        int warmupPeriod,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of backtest data service
/// </summary>
public class BacktestDataService : IBacktestDataService
{
    private readonly ICandleService _candleService;
    private readonly ILogger<BacktestDataService> _logger;

    public BacktestDataService(
        ICandleService candleService,
        ILogger<BacktestDataService> logger)
    {
        _candleService = candleService;
        _logger = logger;
    }

    public async Task<List<CandleDto>> GetCandlesWithLookbackAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        int warmupPeriod,
        MarketType market = MarketType.Spot,
        CancellationToken cancellationToken = default)
    {
        // Calculate lookback start time
        // We need at least warmupPeriod + 1 candles before startTime for proper indicator calculation
        // Add a safety buffer of 3x to ensure we have enough data even with gaps
        var lookbackCandles = Math.Max(warmupPeriod + 1, 50) * 3;
        var intervalSpan = ParseIntervalToTimeSpan(interval);
        var lookbackStart = startTime - (intervalSpan * lookbackCandles);

        _logger.LogInformation(
            "Fetching candles for backtest: symbol={Symbol}, interval={Interval}, " +
            "backtest range={Start} to {End}, lookback start={LookbackStart} (warmup period={WarmupPeriod})",
            symbol, interval, startTime, endTime, lookbackStart, warmupPeriod);

        // Fetch candles from database
        var candles = await _candleService.GetCandlesAsync(
            symbol, interval, lookbackStart, endTime, 1000, market, cancellationToken);

        var candleList = candles.OrderBy(c => c.OpenTime).ToList();

        if (candleList.Count == 0)
        {
            throw new InvalidOperationException(
                $"No candles found for {symbol} {interval} from {lookbackStart:yyyy-MM-dd} to {endTime:yyyy-MM-dd}. " +
                "Please ensure candle data has been backfilled.");
        }

        _logger.LogInformation(
            "Loaded {Count} candles for backtest (including {Warmup} warmup candles)",
            candleList.Count,
            candleList.Count(c => c.OpenTime < startTime));

        return candleList;
    }

    private TimeSpan ParseIntervalToTimeSpan(string interval)
    {
        // Parse intervals like "1m", "5m", "15m", "1h", "4h", "1d"
        var unit = interval[^1];
        var value = int.Parse(interval[..^1]);

        return unit switch
        {
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => throw new ArgumentException($"Unsupported interval format: {interval}")
        };
    }
}
