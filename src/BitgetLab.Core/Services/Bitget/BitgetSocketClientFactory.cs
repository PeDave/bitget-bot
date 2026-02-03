using Bitget.Net.Clients;
using Bitget.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Options;
using BitgetLab.Core.Options;

namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Factory for creating Bitget.Net socket client instances
/// </summary>
public interface IBitgetSocketClientFactory
{
    /// <summary>
    /// Creates a Bitget socket client based on the configured mode
    /// </summary>
    IBitgetSocketClient CreateSocketClient();
}

public class BitgetSocketClientFactory : IBitgetSocketClientFactory
{
    private readonly BitgetOptions _options;

    public BitgetSocketClientFactory(IOptions<BitgetOptions> options)
    {
        _options = options.Value;
    }

    public IBitgetSocketClient CreateSocketClient()
    {
        var mode = _options.GetMode();
        var credentials = mode == BitgetMode.Trade ? _options.Trade : _options.ReadOnly;
        
        if (string.IsNullOrEmpty(credentials.ApiKey) || 
            string.IsNullOrEmpty(credentials.ApiSecret) || 
            string.IsNullOrEmpty(credentials.Passphrase))
        {
            // Return client without credentials for public endpoints
            return new BitgetSocketClient();
        }

        // Create client with credentials
        var client = new BitgetSocketClient(options =>
        {
            options.ApiCredentials = new ApiCredentials(
                credentials.ApiKey,
                credentials.ApiSecret,
                credentials.Passphrase
            );
        });

        return client;
    }
}
