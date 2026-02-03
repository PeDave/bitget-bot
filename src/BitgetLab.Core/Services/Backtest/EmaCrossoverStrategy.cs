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
        if (parameters.TryGetValue("fastPeriod", out var fastPeriod))
        {
            _fastPeriod = ParameterHelper.GetInt32(fastPeriod);
        }
        
        if (parameters.TryGetValue("slowPeriod", out var slowPeriod))
        {
            _slowPeriod = ParameterHelper.GetInt32(slowPeriod);
        }

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

        if (candles.Count < _slowPeriod + 1)
        {
            return signals;
        }

        // Calculate EMAs
        var fastEma = ComputeEMA(candles, _fastPeriod);
        var slowEma = ComputeEMA(candles, _slowPeriod);

        // Find crossovers
        for (int i = 1; i < fastEma.Count; i++)
        {
            var prevFast = fastEma[i - 1];
            var currFast = fastEma[i];
            var prevSlow = slowEma[i - 1];
            var currSlow = slowEma[i];

            // Bullish crossover - fast crosses above slow
            if (prevFast <= prevSlow && currFast > currSlow)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[_slowPeriod + i - 1].OpenTime,
                    Type = SignalType.Buy,
                    Price = candles[_slowPeriod + i - 1].Close,
                    Reason = $"Fast EMA({_fastPeriod}) crossed above Slow EMA({_slowPeriod})"
                });
            }
            // Bearish crossover - fast crosses below slow
            else if (prevFast >= prevSlow && currFast < currSlow)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[_slowPeriod + i - 1].OpenTime,
                    Type = SignalType.Sell,
                    Price = candles[_slowPeriod + i - 1].Close,
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
