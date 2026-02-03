namespace BitgetLab.Core.Models;

/// <summary>
/// Order detail data transfer object for spot and futures orders
/// </summary>
public class OrderDetailDto
{
    /// <summary>
    /// Order ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// Client order ID (optional)
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
    /// Order type (Market, Limit, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Order status (e.g., New, PartiallyFilled, Filled, Cancelled)
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Order price (optional for market orders)
    /// </summary>
    public decimal? Price { get; set; }
    
    /// <summary>
    /// Order quantity
    /// </summary>
    public decimal? Quantity { get; set; }
    
    /// <summary>
    /// Quantity filled
    /// </summary>
    public decimal? QuantityFilled { get; set; }
    
    /// <summary>
    /// Average fill price
    /// </summary>
    public decimal? AveragePrice { get; set; }
    
    /// <summary>
    /// Order creation time
    /// </summary>
    public DateTime CreateTime { get; set; }
    
    /// <summary>
    /// Order update time (optional)
    /// </summary>
    public DateTime? UpdateTime { get; set; }
    
    /// <summary>
    /// Source of the order: "spot" or "futures"
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Product type (e.g., "USDT-FUTURES", "USDC-FUTURES"), null for spot
    /// </summary>
    public string? ProductType { get; set; }
    
    /// <summary>
    /// Margin asset for futures orders (e.g., USDT, USDC), null for spot
    /// </summary>
    public string? MarginAsset { get; set; }
}
