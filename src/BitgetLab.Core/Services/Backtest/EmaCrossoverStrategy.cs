using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// EMA Crossover Strategy - Buy when fast EMA crosses above slow EMA, sell when it crosses below
/// </summary>
public class EmaCrossoverStrategy : IStrategy
{
    public string Name => "EMA Crossover";
    
    private int _fastPeriod = 10;
    private int _slowPeriod = 20;

    public void Configure(Dictionary<string, object> parameters)
    {
        _fastPeriod = ParameterHelper.GetInt32(parameters, "fastPeriod", _fastPeriod);
        _slowPeriod = ParameterHelper.GetInt32(parameters, "slowPeriod", _slowPeriod);

        if (_fastPeriod >= _slowPeriod)
        {
            throw new ArgumentException("Fast period must be less than slow period");
        }

        if (_fastPeriod < 2 || _slowPeriod < 2)
        {
            throw new ArgumentException("Periods must be at least 2");
        }
    }

    public IEnumerable<TradingSignal> GenerateSignals(List<CandleDto> candles)
    {
        var signals = new List<TradingSignal>();

        // Validate we have enough candles for the slow period (which is larger)
        // We need at least slowPeriod + 1 candles to calculate at least 2 slow EMA values for crossover detection
        if (candles.Count < _slowPeriod + 1)
        {
            throw new ArgumentException(
                $"Insufficient candle data. Need at least {_slowPeriod + 1} candles for slowPeriod={_slowPeriod}, but only {candles.Count} provided.");
        }

        // Calculate EMAs
        // ComputeEMA uses the first 'period' candles to compute the initial SMA, then calculates EMA for remaining candles
        // This means: fastEma will have (candles.Count - _fastPeriod + 1) values
        //             slowEma will have (candles.Count - _slowPeriod + 1) values
        // For example, with 50 candles and periods 10/20: fastEma has 41 values, slowEma has 31 values
        var fastEma = ComputeEMA(candles, _fastPeriod);
        var slowEma = ComputeEMA(candles, _slowPeriod);

        // To align the EMAs: slowEma[0] corresponds to candles[_slowPeriod - 1]
        // We need to find the offset in fastEma that corresponds to the same candle
        // fastEma[offset] should correspond to candles[_slowPeriod - 1]
        // Since fastEma[0] corresponds to candles[_fastPeriod - 1], we have:
        // offset = (_slowPeriod - 1) - (_fastPeriod - 1) = _slowPeriod - _fastPeriod
        int fastEmaOffset = _slowPeriod - _fastPeriod;

        // Find crossovers - iterate only up to slowEma.Count to avoid out of bounds
        for (int i = 1; i < slowEma.Count; i++)
        {
            // Get aligned EMA values
            int fastIdx = fastEmaOffset + i;
            int prevFastIdx = fastEmaOffset + i - 1;
            
            var prevFast = fastEma[prevFastIdx];
            var currFast = fastEma[fastIdx];
            var prevSlow = slowEma[i - 1];
            var currSlow = slowEma[i];

            // Calculate the actual candle index (slowEma[i] corresponds to candles[_slowPeriod - 1 + i])
            int candleIdx = _slowPeriod - 1 + i;

            // Bullish crossover - fast crosses above slow
            if (prevFast <= prevSlow && currFast > currSlow)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[candleIdx].OpenTime,
                    Type = SignalType.Buy,
                    Price = candles[candleIdx].Close,
                    Reason = $"Fast EMA({_fastPeriod}) crossed above Slow EMA({_slowPeriod})"
                });
            }
            // Bearish crossover - fast crosses below slow
            else if (prevFast >= prevSlow && currFast < currSlow)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[candleIdx].OpenTime,
                    Type = SignalType.Sell,
                    Price = candles[candleIdx].Close,
                    Reason = $"Fast EMA({_fastPeriod}) crossed below Slow EMA({_slowPeriod})"
                });
            }
        }

        return signals;
    }

    private List<decimal> ComputeEMA(List<CandleDto> candles, int period)
    {
        var results = new List<decimal>();
        var multiplier = 2m / (period + 1);

        // Start with SMA for first value
        var sum = 0m;
        for (int i = 0; i < period; i++)
        {
            sum += candles[i].Close;
        }
        var ema = sum / period;
        results.Add(ema);

        // Calculate EMA for remaining candles
        for (int i = period; i < candles.Count; i++)
        {
            ema = (candles[i].Close - ema) * multiplier + ema;
            results.Add(ema);
        }

        return results;
    }
}
