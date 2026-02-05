namespace BitgetLab.Core.Models;

/// <summary>
/// Request DTO for the /api/backtest/run endpoint
/// </summary>
public class BacktestRunRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = "spot";
    public string Interval { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public StrategyConfig Strategy { get; set; } = new();
    public decimal FeesBps { get; set; } = 10m;
    public decimal SlippageBps { get; set; } = 0m;
    public decimal InitialQuote { get; set; } = 1000m;
}

/// <summary>
/// Strategy configuration for backtest
/// </summary>
public class StrategyConfig
{
    public string Type { get; set; } = "rsi-reversion";
    public int RsiPeriod { get; set; } = 14;
    public decimal EntryBelow { get; set; } = 30m;
    public decimal ExitAbove { get; set; } = 50m;
}

/// <summary>
/// Response DTO for the /api/backtest/run endpoint
/// </summary>
public class BacktestRunResponse
{
    public SummaryMetrics Summary { get; set; } = new();
    public List<TradeDetail> Trades { get; set; } = new();
    public List<EquityPoint> EquityCurve { get; set; } = new();
    public BacktestParameters Parameters { get; set; } = new();
}

/// <summary>
/// Summary metrics for backtest results
/// </summary>
public class SummaryMetrics
{
    public decimal TotalPnL { get; set; }
    public decimal TotalReturnPct { get; set; }
    public int TradeCount { get; set; }
    public decimal WinRate { get; set; }
    public decimal MaxDrawdownPct { get; set; }
    public decimal ProfitFactor { get; set; }
}

/// <summary>
/// Individual trade detail
/// </summary>
public class TradeDetail
{
    public DateTime EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Qty { get; set; }
    public decimal Pnl { get; set; }
    public decimal PnlPct { get; set; }
}

/// <summary>
/// Parameters echoed back in response
/// </summary>
public class BacktestParameters
{
    public string Symbol { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public StrategyConfig Strategy { get; set; } = new();
    public decimal FeesBps { get; set; }
    public decimal SlippageBps { get; set; }
    public decimal InitialQuote { get; set; }
}
