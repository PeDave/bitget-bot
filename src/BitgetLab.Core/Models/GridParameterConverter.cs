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

                    // Read array element and convert to appropriate type
                    // Skip null values - they should not be in the grid arrays
                    if (reader.TokenType == JsonTokenType.Null)
                    {
                        continue;
                    }

                    object value = reader.TokenType switch
                    {
                        // For numbers, preserve integer type if possible
                        // Use TryGetInt64 for better range support while still favoring integers
                        JsonTokenType.Number => reader.TryGetInt64(out long longValue) && 
                                                longValue >= int.MinValue && 
                                                longValue <= int.MaxValue
                            ? (int)longValue
                            : (object)reader.GetDecimal(),
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
