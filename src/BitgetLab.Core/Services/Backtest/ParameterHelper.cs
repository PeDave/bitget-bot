using System.Text.Json;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Helper class for safely extracting typed values from strategy parameters
/// that may be JsonElement objects or native types
/// </summary>
public static class ParameterHelper
{
    /// <summary>
    /// Gets an integer value from parameters dictionary, handling both JsonElement and native types
    /// </summary>
    public static int GetInt32(Dictionary<string, object> parameters, string key, int defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement jsonElement => jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.GetInt32(),
                JsonValueKind.String => int.TryParse(jsonElement.GetString(), out var result) ? result : defaultValue,
                _ => defaultValue
            },
            int intValue => intValue,
            long longValue => (int)longValue,
            double doubleValue => (int)doubleValue,
            decimal decimalValue => (int)decimalValue,
            string stringValue => int.TryParse(stringValue, out var result) ? result : defaultValue,
            _ => Convert.ToInt32(value)
        };
    }

    /// <summary>
    /// Gets a decimal value from parameters dictionary, handling both JsonElement and native types
    /// </summary>
    public static decimal GetDecimal(Dictionary<string, object> parameters, string key, decimal defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement jsonElement => jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(jsonElement.GetString(), out var result) ? result : defaultValue,
                _ => defaultValue
            },
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            double doubleValue => (decimal)doubleValue,
            float floatValue => (decimal)floatValue,
            string stringValue => decimal.TryParse(stringValue, out var result) ? result : defaultValue,
            _ => Convert.ToDecimal(value)
        };
    }

    /// <summary>
    /// Gets a boolean value from parameters dictionary, handling both JsonElement and native types
    /// </summary>
    public static bool GetBoolean(Dictionary<string, object> parameters, string key, bool defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement jsonElement => jsonElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(jsonElement.GetString(), out var result) ? result : defaultValue,
                _ => defaultValue
            },
            bool boolValue => boolValue,
            string stringValue => bool.TryParse(stringValue, out var result) ? result : defaultValue,
            _ => Convert.ToBoolean(value)
        };
    }

    /// <summary>
    /// Gets a string value from parameters dictionary, handling both JsonElement and native types
    /// </summary>
    public static string GetString(Dictionary<string, object> parameters, string key, string defaultValue)
    {
        if (!parameters.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return value switch
        {
            JsonElement jsonElement => jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString() ?? defaultValue,
                JsonValueKind.Number => jsonElement.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => defaultValue
            },
            string stringValue => stringValue,
            _ => value.ToString() ?? defaultValue
        };
    }
}
