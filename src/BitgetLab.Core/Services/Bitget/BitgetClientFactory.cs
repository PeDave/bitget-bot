using Bitget.Net.Clients;
using Bitget.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Options;
using BitgetLab.Core.Options;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Factory for creating Bitget.Net client instances
/// </summary>
public interface IBitgetClientFactory
{
    /// <summary>
    /// Creates a Bitget REST client based on the configured mode
    /// </summary>
    IBitgetRestClient CreateRestClient();
    
    /// <summary>
    /// Gets the current Bitget mode
    /// </summary>
    BitgetMode GetMode();
    
    /// <summary>
    /// Validates if trade operations are allowed
    /// </summary>
    bool IsTradeAllowed();
}

public class BitgetClientFactory : IBitgetClientFactory
{
    private readonly BitgetOptions _options;

    public BitgetClientFactory(IOptions<BitgetOptions> options)
    {
        _options = options.Value;
    }

    public IBitgetRestClient CreateRestClient()
    {
        var mode = _options.GetMode();
        var credentials = mode == BitgetMode.Trade ? _options.Trade : _options.ReadOnly;
        
        if (string.IsNullOrEmpty(credentials.ApiKey) || 
            string.IsNullOrEmpty(credentials.ApiSecret) || 
            string.IsNullOrEmpty(credentials.Passphrase))
        {
            // Return client without credentials for public endpoints
            return new BitgetRestClient();
        }

        // Create client with credentials
        var client = new BitgetRestClient(options =>
        {
            options.ApiCredentials = new ApiCredentials(
                credentials.ApiKey,
                credentials.ApiSecret,
                credentials.Passphrase
            );
        });

        return client;
    }

    public BitgetMode GetMode()
    {
        return _options.GetMode();
    }

    public bool IsTradeAllowed()
    {
        return GetMode() == BitgetMode.Trade;
    }
}
