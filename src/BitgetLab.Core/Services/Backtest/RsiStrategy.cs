using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// RSI Threshold Strategy - Buy when RSI is oversold, sell when overbought
/// </summary>
public class RsiStrategy : IStrategy
{
    public string Name => "RSI Threshold";
    
    private int _period = 14;
    private decimal _oversoldThreshold = 30;
    private decimal _overboughtThreshold = 70;

    public void Configure(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("period", out var period))
        {
            _period = ParameterHelper.GetInt32(period);
        }
        
        if (parameters.TryGetValue("oversoldThreshold", out var oversold))
        {
            _oversoldThreshold = ParameterHelper.GetDecimal(oversold);
        }
        
        if (parameters.TryGetValue("overboughtThreshold", out var overbought))
        {
            _overboughtThreshold = ParameterHelper.GetDecimal(overbought);
        }

        if (_period < 2)
        {
            throw new ArgumentException("Period must be at least 2");
        }

        if (_oversoldThreshold >= _overboughtThreshold)
        {
            throw new ArgumentException("Oversold threshold must be less than overbought threshold");
        }

        if (_oversoldThreshold < 0 || _overboughtThreshold > 100)
        {
            throw new ArgumentException("Thresholds must be between 0 and 100");
        }
    }

    public IEnumerable<TradingSignal> GenerateSignals(List<CandleDto> candles)
    {
        var signals = new List<TradingSignal>();

        if (candles.Count < _period + 2)
        {
            return signals;
        }

        // Calculate RSI values
        var rsiValues = ComputeRSI(candles, _period);

        // Track state to avoid duplicate signals
        bool inPosition = false;

        for (int i = 1; i < rsiValues.Count; i++)
        {
            var prevRsi = rsiValues[i - 1];
            var currRsi = rsiValues[i];
            var candleIndex = _period + i;

            // Buy signal - RSI crosses below oversold threshold
            if (!inPosition && currRsi <= _oversoldThreshold && prevRsi > _oversoldThreshold)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[candleIndex].OpenTime,
                    Type = SignalType.Buy,
                    Price = candles[candleIndex].Close,
                    Reason = $"RSI({_period}) crossed below {_oversoldThreshold} (oversold)"
                });
                inPosition = true;
            }
            // Sell signal - RSI crosses above overbought threshold
            else if (inPosition && currRsi >= _overboughtThreshold && prevRsi < _overboughtThreshold)
            {
                signals.Add(new TradingSignal
                {
                    Time = candles[candleIndex].OpenTime,
                    Type = SignalType.Sell,
                    Price = candles[candleIndex].Close,
                    Reason = $"RSI({_period}) crossed above {_overboughtThreshold} (overbought)"
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

            // Smooth for next iteration
            avgGain = (avgGain * (period - 1) + gains[i]) / period;
            avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
        }

        return results;
    }
}
