using BitgetLab.Core.Models;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Service for retrieving account valuation information using Bitget.Net SDK
/// </summary>
public interface IAccountValuationService
{
    /// <summary>
    /// Gets account assets valuation across all account types
    /// </summary>
    Task<IEnumerable<AccountValuationDto>> GetAccountValuationAsync(CancellationToken cancellationToken = default);
}

public class AccountValuationService : IAccountValuationService
{
    private readonly IBitgetClientFactory _clientFactory;

    public AccountValuationService(IBitgetClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IEnumerable<AccountValuationDto>> GetAccountValuationAsync(CancellationToken cancellationToken = default)
    {
        using var client = _clientFactory.CreateRestClient();
        
        var result = await client.SpotApiV2.Account.GetAssetsValuationAsync(ct: cancellationToken);
        
        if (!result.Success)
        {
            throw new BitgetApiException($"Failed to get account valuation: {result.Error?.Message ?? "Unknown error"}");
        }

        return result.Data.Select(v => new AccountValuationDto
        {
            AccountType = v.AccountType,
            UsdtBalance = v.UsdtBalance
        }).ToList();
    }
}
