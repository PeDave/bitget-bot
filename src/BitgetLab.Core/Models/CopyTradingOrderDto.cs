namespace BitgetLab.Core.Models;

/// <summary>
/// Copy trading order data transfer object
/// </summary>
public class CopyTradingOrderDto
{
    /// <summary>
    /// Order ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Trading symbol (e.g., BTCUSDT)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Trader ID being followed
    /// </summary>
    public string TraderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Trader name
    /// </summary>
    public string? TraderName { get; set; }
    
    /// <summary>
    /// Order side (Buy, Sell)
    /// </summary>
    public string Side { get; set; } = string.Empty;
    
    /// <summary>
    /// Order type
    /// </summary>
    public string OrderType { get; set; } = string.Empty;
    
    /// <summary>
    /// Order quantity
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Order price
    /// </summary>
    public decimal? Price { get; set; }
    
    /// <summary>
    /// Product type (e.g., "USDT-FUTURES", "USDC-FUTURES")
    /// </summary>
    public string ProductType { get; set; } = string.Empty;
    
    /// <summary>
    /// Order creation time
    /// </summary>
    public DateTime CreateTime { get; set; }
}
