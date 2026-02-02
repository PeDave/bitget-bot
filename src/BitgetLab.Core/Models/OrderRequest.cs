namespace BitgetLab.Core.Models;

/// <summary>
/// Request to place an order
/// </summary>
public class OrderRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty; // "Buy" or "Sell"
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; } // Optional for market orders
}
