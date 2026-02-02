using Bitget.Net.Enums;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving account balance information using Bitget.Net SDK
/// </summary>
public interface IAccountBalanceService
{
    /// <summary>
    /// Gets spot account balances
    /// </summary>
    Task<IEnumerable<BalanceDto>> GetSpotBalancesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets futures account balances for specified product types
    /// </summary>
    Task<IEnumerable<BalanceDto>> GetFuturesBalancesAsync(CancellationToken cancellationToken = default);
}

public class AccountBalanceService : IAccountBalanceService
{
    private readonly IBitgetClientFactory _clientFactory;

    public AccountBalanceService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<BalanceDto>> GetSpotBalancesAsync(CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.SpotApiV2.Account.GetSpotBalancesAsync(ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get spot balances: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(b => new BalanceDto
        {
            Asset = b.Asset,
            Available = b.Available,
            Total = b.Available + b.Frozen + b.Locked,
            Source = "spot",
            ProductType = null,
            UpdateTime = b.UpdateTime
        }).ToList();
    }

    public async Task<IEnumerable<BalanceDto>> GetFuturesBalancesAsync(CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var balances = new List<BalanceDto>();
        
        // Query USDT Futures
        var usdtResult = await client.FuturesApiV2.Account.GetBalancesAsync(
            BitgetProductTypeV2.UsdtFutures, 
            ct: cancellationToken);
        
        if (!usdtResult.Success)
        {
            throw new BitgetApiException($"Failed to get USDT futures balances: {usdtResult.Error?.Message ?? "Unknown error"}");
        }

        balances.AddRange(usdtResult.Data.Select(b => new BalanceDto
        {
            Asset = b.MarginAsset,
            Available = b.Available,
            Total = b.Equity,
            Source = "futures",
            ProductType = "USDT-FUTURES",
            // Note: Bitget futures API doesn't provide an update timestamp, using current time
            UpdateTime = DateTime.UtcNow
        }));
        
        // Query USDC Futures
        var usdcResult = await client.FuturesApiV2.Account.GetBalancesAsync(
            BitgetProductTypeV2.UsdcFutures, 
            ct: cancellationToken);
        
        if (!usdcResult.Success)
        {
            throw new BitgetApiException($"Failed to get USDC futures balances: {usdcResult.Error?.Message ?? "Unknown error"}");
        }

        balances.AddRange(usdcResult.Data.Select(b => new BalanceDto
        {
            Asset = b.MarginAsset,
            Available = b.Available,
            Total = b.Equity,
            Source = "futures",
            ProductType = "USDC-FUTURES",
            // Note: Bitget futures API doesn't provide an update timestamp, using current time
            UpdateTime = DateTime.UtcNow
        }));
        
        return balances;
    }
}

/// <summary>
/// Balance data transfer object
/// </summary>
public class BalanceDto
{
    /// <summary>
    /// Asset name (e.g., BTC, USDT, ETH)
    /// </summary>
    public string Asset { get; set; } = string.Empty;
    
    /// <summary>
    /// Available balance that can be traded or transferred
    /// </summary>
    public decimal Available { get; set; }
    
    /// <summary>
    /// Total balance including locked/frozen amounts (or equity for futures)
    /// </summary>
    public decimal Total { get; set; }
    
    /// <summary>
    /// Source of the balance: "spot" or "futures"
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Product type for futures (e.g., "USDT-FUTURES", "USDC-FUTURES"), null for spot
    /// </summary>
    public string? ProductType { get; set; }
    
    /// <summary>
    /// Last update time
    /// </summary>
    public DateTime UpdateTime { get; set; }
}
