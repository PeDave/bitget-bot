namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Bitget API operation mode
/// </summary>
public enum BitgetMode
{
    /// <summary>
    /// Read-only mode: only market data queries allowed, trading operations blocked
    /// </summary>
    ReadOnly,
    
    /// <summary>
    /// Trade mode: all operations including order placement allowed
    /// </summary>
    Trade
}
