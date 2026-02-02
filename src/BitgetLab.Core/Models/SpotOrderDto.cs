namespace BitgetLab.Core.Models;

/// <summary>
/// Spot order data transfer object
/// </summary>
public class SpotOrderDto
{
    /// <summary>
    /// Order ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Client order ID
    /// </summary>
    public string? ClientOrderId { get; set; }
    
    /// <summary>
    /// Trading symbol (e.g., BTCUSDT)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Order side (Buy, Sell)
    /// </summary>
    public string Side { get; set; } = string.Empty;
    
    /// <summary>
    /// Order type (Limit, Market, etc.)
    /// </summary>
    public string OrderType { get; set; } = string.Empty;
    
    /// <summary>
    /// Order price
    /// </summary>
    public decimal? Price { get; set; }
    
    /// <summary>
    /// Order quantity
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Filled quantity
    /// </summary>
    public decimal QuantityFilled { get; set; }
    
    /// <summary>
    /// Order status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Order creation time
    /// </summary>
    public DateTime CreateTime { get; set; }
    
    /// <summary>
    /// Order update time
    /// </summary>
    public DateTime UpdateTime { get; set; }
}
