namespace BitgetLab.Core.Models;

/// <summary>
/// Result of placing an order
/// </summary>
public class OrderResult
{
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
