using System.Text.Json;

namespace BitgetLab.Core.Services.Backtest;

/// <summary>
/// Helper class for safely extracting parameter values from Dictionary&lt;string, object&gt;
/// Handles both JsonElement (from System.Text.Json deserialization) and native types
/// </summary>
public static class ParameterHelper
{
    /// <summary>
    /// Safely extracts an integer parameter value
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <param name="key">The parameter key</param>
    /// <param name="defaultValue">The default value if key not found</param>
    /// <returns>The integer value</returns>
    public static int GetInt32(Dictionary<string, object> parameters, string key, int defaultValue = 0)
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
    /// Safely extracts a decimal parameter value
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <param name="key">The parameter key</param>
    /// <param name="defaultValue">The default value if key not found</param>
    /// <returns>The decimal value</returns>
    public static decimal GetDecimal(Dictionary<string, object> parameters, string key, decimal defaultValue = 0m)
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
    /// Safely extracts a string parameter value
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <param name="key">The parameter key</param>
    /// <param name="defaultValue">The default value if key not found</param>
    /// <returns>The string value</returns>
    public static string GetString(Dictionary<string, object> parameters, string key, string defaultValue = "")
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

    /// <summary>
    /// Safely extracts a boolean parameter value
    /// </summary>
    /// <param name="parameters">The parameters dictionary</param>
    /// <param name="key">The parameter key</param>
    /// <param name="defaultValue">The default value if key not found</param>
    /// <returns>The boolean value</returns>
    public static bool GetBoolean(Dictionary<string, object> parameters, string key, bool defaultValue = false)
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
                JsonValueKind.Number => jsonElement.GetInt32() != 0,
                _ => defaultValue
            },
            bool boolValue => boolValue,
            string stringValue => bool.TryParse(stringValue, out var result) ? result : defaultValue,
            int intValue => intValue != 0,
            _ => Convert.ToBoolean(value)
        };
    }
}
