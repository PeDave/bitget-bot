namespace BitgetLab.Core.Options;

/// <summary>
/// Configuration options for Bitget futures public API
/// </summary>
public class BitgetFuturesOptions
{
    public const string SectionName = "Bitget:Futures";

    /// <summary>
    /// Product type for futures trading
    /// </summary>
    public string ProductType { get; set; } = "usdt-futures";

    /// <summary>
    /// Base URL for Bitget public REST API
    /// </summary>
    public string PublicRestBaseUrl { get; set; } = "https://api.bitget.com";
}
