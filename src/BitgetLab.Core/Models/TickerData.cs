namespace BitgetLab.Core.Models;

/// <summary>
/// Ticker data for a symbol
/// </summary>
public class TickerData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal High24h { get; set; }
    public decimal Low24h { get; set; }
    public decimal Change24h { get; set; }
}
