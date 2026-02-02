using Bitget.Net.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Options;
using BitgetLab.Core.Options;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Factory for creating Bitget.Net client instances based on configuration
/// </summary>
public interface IBitgetClientFactory
{
    /// <summary>
    /// Creates a Bitget REST client with specific credentials
    /// </summary>
    BitgetRestClient CreateClient(BitgetCredentials credentials);
    
    /// <summary>
    /// Creates a Bitget REST client for the current mode (ReadOnly or Trade)
    /// </summary>
    BitgetRestClient CreateClientForCurrentMode();
}

public class BitgetClientFactory : IBitgetClientFactory
{
    private readonly BitgetOptions _options;

    public BitgetClientFactory(IOptions<BitgetOptions> options)
    {
        _options = options.Value;
    }

    public BitgetRestClient CreateClient(BitgetCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            // Return client without credentials for public endpoints
            return new BitgetRestClient();
        }

        return new BitgetRestClient(options =>
        {
            options.ApiCredentials = new ApiCredentials(
                credentials.ApiKey,
                credentials.ApiSecret,
                credentials.Passphrase
            );
        });
    }

    public BitgetRestClient CreateClientForCurrentMode()
    {
        var credentials = _options.Mode?.ToLowerInvariant() == "trade"
            ? _options.Trade
            : _options.ReadOnly;
        
        return CreateClient(credentials);
    }
}
