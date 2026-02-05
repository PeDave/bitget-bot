using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// RSI Mean-Reversion Strategy - Long-only, buy when RSI is below entry threshold, exit when above exit threshold
/// </summary>
public class RsiReversionStrategy : IStrategy
{
    public string Name => "RSI Mean-Reversion (Long-Only)";
    
    private int _rsiPeriod = 14;
    private decimal _entryBelow = 30m;
    private decimal _exitAbove = 50m;

    public void Configure(Dictionary<string, object> parameters)
    {
        _rsiPeriod = ParameterHelper.GetInt32(parameters, "rsiPeriod", _rsiPeriod);
        _entryBelow = ParameterHelper.GetDecimal(parameters, "entryBelow", _entryBelow);
        _exitAbove = ParameterHelper.GetDecimal(parameters, "exitAbove", _exitAbove);

        if (_rsiPeriod < 2)
        {
            throw new ArgumentException("rsiPeriod must be at least 2");
        }

        if (_entryBelow >= _exitAbove)
        {
            throw new ArgumentException("entryBelow must be less than exitAbove");
        }

        if (_entryBelow < 0 || _exitAbove > 100)
        {
            throw new ArgumentException("RSI thresholds must be between 0 and 100");
        }
    }

    public IEnumerable<TradingSignal> GenerateSignals(List<CandleDto> candles)
    {
        var signals = new List<TradingSignal>();

        if (candles.Count < _rsiPeriod + 2)
        {
            return signals;
        }

        // Calculate RSI values
        var rsiValues = ComputeRSI(candles, _rsiPeriod);

        // Track state to avoid duplicate signals
        bool inPosition = false;

        for (int i = 0; i < rsiValues.Count; i++)
        {
            var currRsi = rsiValues[i];
            var candleIndex = _rsiPeriod + i;

            // Buy signal - RSI is below entry threshold and we're not in position
            if (!inPosition && currRsi <= _entryBelow)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[candleIndex].OpenTime,
                    Type = SignalType.Buy,
                    Price = candles[candleIndex].Close,
                    Reason = $"RSI({_rsiPeriod})={currRsi:F2} below entry threshold {_entryBelow}"
                });
                inPosition = true;
            }
            // Sell signal - RSI is above exit threshold and we're in position
            else if (inPosition && currRsi >= _exitAbove)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[candleIndex].OpenTime,
                    Type = SignalType.Sell,
                    Price = candles[candleIndex].Close,
                    Reason = $"RSI({_rsiPeriod})={currRsi:F2} above exit threshold {_exitAbove}"
                });
                inPosition = false;
            }
        }

        return signals;
    }

    private List<decimal> ComputeRSI(List<CandleDto> candles, int period)
    {
        var results = new List<decimal>();

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

        if (gains.Count < period)
        {
            return results;
        }

        // Calculate initial average gain/loss
        var avgGain = gains.Take(period).Average();
        var avgLoss = losses.Take(period).Average();

        // Calculate RSI
        for (int i = period; i < gains.Count; i++)
        {
            if (avgLoss == 0)
            {
                results.Add(100);
            }
            else
            {
                var rs = avgGain / avgLoss;
                var rsi = 100 - (100 / (1 + rs));
                results.Add(rsi);
            }

            // Smooth for next iteration using Wilder's smoothing
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        return results;
    }
}
