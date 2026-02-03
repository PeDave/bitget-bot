using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for computing technical indicators from candle data
/// </summary>
public interface IIndicatorService
{
    /// <summary>
    /// Computes indicators for a given symbol and time period
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <param name="interval">Candle interval</param>
    /// <param name="indicator">Indicator type (SMA, EMA, RSI)</param>
    /// <param name="period">Period for the indicator calculation</param>
    /// <param name="startTime">Optional start time filter</param>
    /// <param name="endTime">Optional end time filter</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of indicator values</returns>
    Task<IEnumerable<IndicatorValueDto>> ComputeIndicatorAsync(
        string symbol,
        string interval,
        string indicator,
        int period,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public class IndicatorService : IIndicatorService
{
    private readonly ICandleService _candleService;

    public IndicatorService(ICandleService candleService)
    {
        _candleService = candleService;
    }

    public async Task<IEnumerable<IndicatorValueDto>> ComputeIndicatorAsync(
        string symbol,
        string interval,
        string indicator,
        int period,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (period <= 0)
        {
            throw new ArgumentException("Period must be greater than 0", nameof(period));
        }

        // Fetch enough candles to compute indicators (need extra for warmup)
        var requiredCandles = limit + period + 100; // Extra buffer for warmup
        var candles = await _candleService.GetCandlesAsync(
            symbol, interval, startTime, endTime, requiredCandles, cancellationToken);

        var candleList = candles.OrderBy(c => c.OpenTime).ToList();

        if (candleList.Count < period)
        {
            throw new InvalidOperationException($"Not enough candles to compute {indicator} with period {period}. Need at least {period}, got {candleList.Count}");
        }

        return indicator.ToUpperInvariant() switch
        {
            IndicatorTypes.SMA => ComputeSMA(candleList, period, limit),
            IndicatorTypes.EMA => ComputeEMA(candleList, period, limit),
            IndicatorTypes.RSI => ComputeRSI(candleList, period, limit),
            _ => throw new ArgumentException($"Unsupported indicator: {indicator}. Supported: SMA, EMA, RSI")
        };
    }

    private IEnumerable<IndicatorValueDto> ComputeSMA(List<CandleDto> candles, int period, int limit)
    {
        var results = new List<IndicatorValueDto>();

        for (int i = period - 1; i < candles.Count; i++)
        {
            var sum = 0m;
            for (int j = 0; j < period; j++)
            {
                sum += candles[i - j].Close;
            }
            var sma = sum / period;

            results.Add(new IndicatorValueDto
            {
                OpenTime = candles[i].OpenTime,
                Value = sma
            });
        }

        return results.TakeLast(limit).ToList();
    }

    private IEnumerable<IndicatorValueDto> ComputeEMA(List<CandleDto> candles, int period, int limit)
    {
        var results = new List<IndicatorValueDto>();
        var multiplier = 2m / (period + 1);

        // Start with SMA for first value
        var sum = 0m;
        for (int i = 0; i < period; i++)
        {
            sum += candles[i].Close;
        }
        var ema = sum / period;

        results.Add(new IndicatorValueDto
        {
            OpenTime = candles[period - 1].OpenTime,
            Value = ema
        });

        // Calculate EMA for remaining candles
        for (int i = period; i < candles.Count; i++)
        {
            ema = (candles[i].Close - ema) * multiplier + ema;
            results.Add(new IndicatorValueDto
            {
                OpenTime = candles[i].OpenTime,
                Value = ema
            });
        }

        return results.TakeLast(limit).ToList();
    }

    private IEnumerable<IndicatorValueDto> ComputeRSI(List<CandleDto> candles, int period, int limit)
    {
        var results = new List<IndicatorValueDto>();

        if (candles.Count < period + 1)
        {
            return results;
        }

        // Calculate price changes
        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }

        // Ensure we have enough data for the period
        if (gains.Count < period)
        {
            return results;
        }

        // Calculate initial average gain/loss
        var avgGain = gains.Take(period).DefaultIfEmpty(0).Average();
        var avgLoss = losses.Take(period).DefaultIfEmpty(0).Average();

        // Calculate RSI
        for (int i = period; i < gains.Count; i++)
        {
            if (avgLoss == 0)
            {
                results.Add(new IndicatorValueDto
                {
                    OpenTime = candles[i + 1].OpenTime,
                    Value = 100
                });
            }
            else
            {
                var rs = avgGain / avgLoss;
                var rsi = 100 - (100 / (1 + rs));

                results.Add(new IndicatorValueDto
                {
                    OpenTime = candles[i + 1].OpenTime,
                    Value = rsi
                });
            }

            // Smooth for next iteration
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        return results.TakeLast(limit).ToList();
    }
}
