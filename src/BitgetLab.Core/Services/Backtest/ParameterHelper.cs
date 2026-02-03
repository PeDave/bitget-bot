using System.Text.Json;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Helper methods for extracting parameter values from dictionaries
/// </summary>
internal static class ParameterHelper
{
    /// <summary>
    /// Safely converts a parameter value to int32, handling both JsonElement and primitive types
    /// </summary>
    public static int GetInt32(object value)
    {
        if (value is JsonElement jsonElement)
        {
            return jsonElement.GetInt32();
        }
        return Convert.ToInt32(value);
    }

    /// <summary>
    /// Safely converts a parameter value to decimal, handling both JsonElement and primitive types
    /// </summary>
    public static decimal GetDecimal(object value)
    {
        if (value is JsonElement jsonElement)
        {
            return jsonElement.GetDecimal();
        }
        return Convert.ToDecimal(value);
    }
}
