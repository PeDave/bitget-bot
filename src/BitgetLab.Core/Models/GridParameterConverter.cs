using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitgetLab.Core.Models;

/// <summary>
/// Custom JSON converter for Grid parameter that properly handles array deserialization
/// </summary>
public class GridParameterConverter : JsonConverter<Dictionary<string, List<object>>>
{
    public override Dictionary<string, List<object>>? Read(
        ref Utf8JsonReader reader, 
        Type typeToConvert, 
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var result = new Dictionary<string, List<object>>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return result;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            string propertyName = reader.GetString()!;
            reader.Read();

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<object>();
                
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }

                    // Skip null values - they should not be in the grid arrays
                    if (reader.TokenType == JsonTokenType.Null)
                    {
                        continue;
                    }

                    object value = reader.TokenType switch
                    {
                        JsonTokenType.Number => ConvertNumber(ref reader),
                        JsonTokenType.String => reader.GetString()!,
                        JsonTokenType.True => true,
                        JsonTokenType.False => false,
                        _ => throw new JsonException($"Unexpected token type: {reader.TokenType}")
                    };

                    list.Add(value);
                }

                result[propertyName] = list;
            }
            else
            {
                throw new JsonException($"Expected StartArray token for property '{propertyName}'");
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    /// <summary>
    /// Converts a JSON number to the most appropriate .NET numeric type.
    /// Prefers int for integer values within int32 range, otherwise uses decimal.
    /// Supports int64 range for integers and decimal range for decimals.
    /// </summary>
    private static object ConvertNumber(ref Utf8JsonReader reader)
    {
        // Try to get as int64 first
        if (reader.TryGetInt64(out long longValue))
        {
            // If it fits in int32, return as int for better compatibility
            if (longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                return (int)longValue;
            }
            // Otherwise return as long
            return longValue;
        }

        // If not an integer, try to get as decimal
        if (reader.TryGetDecimal(out decimal decimalValue))
        {
            return decimalValue;
        }

        // If decimal also fails, try double as a fallback
        // This handles edge cases like very large numbers or special values
        try
        {
            return reader.GetDouble();
        }
        catch
        {
            throw new JsonException($"Unable to convert number value to a supported numeric type");
        }
    }

    public override void Write(
        Utf8JsonWriter writer, 
        Dictionary<string, List<object>> value, 
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            writer.WriteStartArray();

            foreach (var item in kvp.Value)
            {
                JsonSerializer.Serialize(writer, item, options);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
