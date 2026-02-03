namespace BitgetLab.Core.Models;

/// <summary>
/// User trade data transfer object for spot and futures trades
/// </summary>
public class UserTradeDto
{
    /// <summary>
    /// Trade ID
    /// </summary>
    public string TradeId { get; set; } = string.Empty;
    
    /// <summary>
    /// Order ID associated with this trade
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
    /// Trade side (Buy, Sell)
    /// </summary>
    public string Side { get; set; } = string.Empty;
    
    /// <summary>
    /// Trade price
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// Trade quantity
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Trade timestamp
    /// </summary>
    public DateTime TradeTime { get; set; }
    
    /// <summary>
    /// Fee asset (e.g., USDT, BTC)
    /// </summary>
    public string FeeAsset { get; set; } = string.Empty;
    
    /// <summary>
    /// Fee amount
    /// </summary>
    public decimal Fee { get; set; }
    
    /// <summary>
    /// Fee deduction amount (for futures)
    /// </summary>
    public decimal? FeeDeduction { get; set; }
    
    /// <summary>
    /// Total fee deduction amount (for futures)
    /// </summary>
    public decimal? FeeTotalDeduction { get; set; }
    
    /// <summary>
    /// Source of the trade: "spot" or "futures"
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Product type (e.g., "USDT-FUTURES", "USDC-FUTURES"), null for spot
    /// </summary>
    public string? ProductType { get; set; }
    
    /// <summary>
    /// Margin asset for futures trades (e.g., USDT, USDC), null for spot
    /// </summary>
    public string? MarginAsset { get; set; }
}
