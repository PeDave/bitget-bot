using System.Text.Json.Serialization;

namespace BitgetLab.Core.Models;

/// <summary>
/// Represents a backtest run with its configuration and results
/// </summary>
public class BacktestDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string Status { get; set; } = "pending";
    public BacktestSummary? Summary { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Summary of backtest results
/// </summary>
public class BacktestSummary
{
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal NetPnl { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal InitialBalance { get; set; }
    public decimal FinalBalance { get; set; }
    public decimal ReturnPercent { get; set; }
    public List<EquityPoint>? EquityCurve { get; set; }
}

/// <summary>
/// Point in the equity curve
/// </summary>
public class EquityPoint
{
    public DateTime Time { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>
/// Represents a simulated trade from a backtest
/// </summary>
public class BacktestTradeDto
{
    public Guid Id { get; set; }
    public Guid BacktestId { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public string Side { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public decimal Qty { get; set; }
    public decimal? Pnl { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request to run a backtest
/// </summary>
public class RunBacktestRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public decimal? FeeBps { get; set; }
    public decimal? SlippageBps { get; set; }
    public decimal? InitialBalance { get; set; }
}

/// <summary>
/// Strategy types
/// </summary>
public static class StrategyTypes
{
    public const string EmaCross = "ema_cross";
    public const string Rsi = "rsi";
}

/// <summary>
/// Backtest status values
/// </summary>
public static class BacktestStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
