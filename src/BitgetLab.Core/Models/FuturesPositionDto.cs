namespace BitgetLab.Core.Models;

/// <summary>
/// Futures position data transfer object
/// </summary>
public class FuturesPositionDto
{
    /// <summary>
    /// Trading symbol (e.g., BTCUSDT)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Position side (Long, Short, Net)
    /// </summary>
    public string PositionSide { get; set; } = string.Empty;
    
    /// <summary>
    /// Total quantity of the position
    /// </summary>
    public decimal Total { get; set; }
    
    /// <summary>
    /// Available quantity (may be null)
    /// </summary>
    public decimal? Available { get; set; }
    
    /// <summary>
    /// Average open price
    /// </summary>
    public decimal AverageOpenPrice { get; set; }
    
    /// <summary>
    /// Unrealized profit and loss
    /// </summary>
    public decimal UnrealizedPnl { get; set; }
    
    /// <summary>
    /// Leverage
    /// </summary>
    public decimal Leverage { get; set; }
    
    /// <summary>
    /// Liquidation price
    /// </summary>
    public decimal LiquidationPrice { get; set; }
    
    /// <summary>
    /// Last update time
    /// </summary>
    public DateTime UpdateTime { get; set; }
    
    /// <summary>
    /// Product type (e.g., "USDT-FUTURES", "USDC-FUTURES")
    /// </summary>
    public string ProductType { get; set; } = string.Empty;
    
    /// <summary>
    /// Margin asset (e.g., USDT, USDC)
    /// </summary>
    public string MarginAsset { get; set; } = string.Empty;
}
