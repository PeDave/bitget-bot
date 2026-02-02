namespace BitgetLab.Core.Models;

/// <summary>
/// Account valuation data transfer object
/// </summary>
public class AccountValuationDto
{
    /// <summary>
    /// Account type (e.g., spot, p2p, coin_futures, usdt_futures, usdc_futures)
    /// </summary>
    public string AccountType { get; set; } = string.Empty;
    
    /// <summary>
    /// USDT balance/valuation
    /// </summary>
    public decimal UsdtBalance { get; set; }
}
