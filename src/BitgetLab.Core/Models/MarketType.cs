namespace BitgetLab.Core.Models;

/// <summary>
/// Represents the type of market for trading and candle data
/// </summary>
public enum MarketType
{
    /// <summary>
    /// Spot market
    /// </summary>
    Spot = 0,
    
    /// <summary>
    /// USDT-margined futures/perpetual market
    /// </summary>
    Futures = 1
}

/// <summary>
/// Helper methods for MarketType
/// </summary>
public static class MarketTypeExtensions
{
    /// <summary>
    /// Parse string to MarketType enum
    /// </summary>
    public static MarketType ParseMarketType(string? market)
    {
        if (string.IsNullOrWhiteSpace(market))
        {
            return MarketType.Spot; // Default to spot
        }
        
        return market.ToLowerInvariant() switch
        {
            "spot" => MarketType.Spot,
            "futures" => MarketType.Futures,
            _ => throw new ArgumentException($"Invalid market type: {market}. Valid values: spot, futures")
        };
    }
    
    /// <summary>
    /// Convert MarketType to string representation
    /// </summary>
    public static string ToStringValue(this MarketType market)
    {
        return market switch
        {
            MarketType.Spot => "spot",
            MarketType.Futures => "futures",
            _ => throw new ArgumentException($"Unknown market type: {market}")
        };
    }
    
    /// <summary>
    /// Convert MarketType to product type for Bitget API
    /// </summary>
    public static string ToProductType(this MarketType market)
    {
        return market switch
        {
            MarketType.Spot => "SPOT",
            MarketType.Futures => "USDT-FUTURES",
            _ => throw new ArgumentException($"Unknown market type: {market}")
        };
    }
}
