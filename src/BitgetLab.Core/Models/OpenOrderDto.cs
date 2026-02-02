namespace BitgetLab.Core.Models;

/// <summary>
/// Open order data transfer object
/// </summary>
public class OpenOrderDto
{
    /// <summary>
    /// Order ID from exchange
    /// </summary>
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Client order ID (nullable)
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
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Order status
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Order price (may be null for market orders)
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
    /// Order creation time
    /// </summary>
    public DateTime CreateTime { get; set; }
    
    /// <summary>
    /// Order last update time
    /// </summary>
    public DateTime UpdateTime { get; set; }
    
    /// <summary>
    /// Source of the order: "spot" or "futures"
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Product type for futures (e.g., "USDT-FUTURES", "USDC-FUTURES"), null for spot
    /// </summary>
    public string? ProductType { get; set; }
    
    /// <summary>
    /// Margin asset for futures (e.g., USDT, USDC), null for spot
    /// </summary>
    public string? MarginAsset { get; set; }
}
