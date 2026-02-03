namespace BitgetLab.Core.Models;

/// <summary>
/// Request model for WebSocket subscription operations
/// </summary>
public class SubscriptionRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
}
