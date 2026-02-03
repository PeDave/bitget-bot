using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Trading signal from a strategy
/// </summary>
public class TradingSignal
{
    public DateTime Time { get; set; }
    public SignalType Type { get; set; }
    public decimal Price { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Signal types
/// </summary>
public enum SignalType
{
    None,
    Buy,
    Sell
}

/// <summary>
/// Interface for trading strategies
/// </summary>
public interface IStrategy
{
    string Name { get; }
    void Configure(Dictionary<string, object> parameters);
    IEnumerable<TradingSignal> GenerateSignals(List<CandleDto> candles);
}
