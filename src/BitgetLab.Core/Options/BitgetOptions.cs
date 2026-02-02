namespace BitgetLab.Core.Options;

public class BitgetOptions
{
    public const string SectionName = "Bitget";
    
    /// <summary>
    /// Bitget API mode: ReadOnly or Trade
    /// </summary>
    public string Mode { get; set; } = "ReadOnly";
    
    public BitgetCredentials ReadOnly { get; set; } = new();
    public BitgetCredentials Trade { get; set; } = new();
}

public class BitgetCredentials
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Passphrase { get; set; } = string.Empty;
}
