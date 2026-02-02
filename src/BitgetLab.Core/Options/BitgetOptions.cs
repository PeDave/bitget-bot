using BitgetLab.Core.Services.Bitget;

namespace BitgetLab.Core.Options;

public class BitgetOptions
{
    public const string SectionName = "Bitget";
    
    /// <summary>
    /// Bitget API mode: ReadOnly or Trade
    /// </summary>
    public string Mode { get; set; } = "ReadOnly";
    
    /// <summary>
    /// Gets the parsed mode as enum
    /// </summary>
    public BitgetMode GetMode()
    {
        return Enum.TryParse<BitgetMode>(Mode, true, out var mode) ? mode : BitgetMode.ReadOnly;
    }
    
    public BitgetCredentials ReadOnly { get; set; } = new();
    public BitgetCredentials Trade { get; set; } = new();
}

public class BitgetCredentials
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Passphrase { get; set; } = string.Empty;
}
