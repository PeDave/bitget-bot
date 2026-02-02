namespace BitgetLab.Core.Services.Bitget;

/// <summary>
/// Exception thrown when a Bitget API operation fails
/// </summary>
public class BitgetApiException : Exception
{
    public BitgetApiException(string message) : base(message)
    {
    }

    public BitgetApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
