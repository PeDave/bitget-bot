namespace BitgetLab.Core.Models;

/// <summary>
/// Copy trading current order (tracking item) data transfer object
/// </summary>
public class CopyTradingCurrentOrderDto
{
    /// <summary>
    /// Track order number
    /// </summary>
    public string TrackingNo { get; set; } = string.Empty;
    
    /// <summary>
    /// Trading symbol (e.g., BTCUSDT)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Trader ID
    /// </summary>
    public string TraderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Trader name/alias
    /// </summary>
    public string TraderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Opening order ID
    /// </summary>
    public string OpenOrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Closing order ID (optional)
    /// </summary>
    public string? CloseOrderId { get; set; }
    
    /// <summary>
    /// Position side (Long, Short, etc.)
    /// </summary>
    public string PositionSide { get; set; } = string.Empty;
    
    /// <summary>
    /// Leverage for opening position
    /// </summary>
    public int OpenLeverage { get; set; }
    
    /// <summary>
    /// Average entry price
    /// </summary>
    public decimal OpenAveragePrice { get; set; }
    
    /// <summary>
    /// Opening volume
    /// </summary>
    public decimal OpenSize { get; set; }
    
    /// <summary>
    /// Opening fee
    /// </summary>
    public decimal OpenFee { get; set; }
    
    /// <summary>
    /// Margin amount
    /// </summary>
    public decimal OpenMarginSize { get; set; }
    
    /// <summary>
    /// Position opening time
    /// </summary>
    public DateTime OpenTime { get; set; }
    
    /// <summary>
    /// Closing average price (optional)
    /// </summary>
    public decimal? CloseAveragePrice { get; set; }
    
    /// <summary>
    /// Closing volume (optional)
    /// </summary>
    public decimal? CloseSize { get; set; }
    
    /// <summary>
    /// Position closing time (optional)
    /// </summary>
    public DateTime? CloseTime { get; set; }
    
    /// <summary>
    /// Product type (e.g., "USDT-FUTURES", "USDC-FUTURES")
    /// </summary>
    public string ProductType { get; set; } = string.Empty;
}
