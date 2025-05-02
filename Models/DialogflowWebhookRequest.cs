using System.Text.Json.Serialization;
using System.Text.Json;

namespace WeatherWebhook.Models;

public class DialogflowWebhookRequest
{
    [JsonPropertyName("queryResult")]
    public QueryResult QueryResult { get; set; } = new();
}

public class QueryResult
{
    [JsonPropertyName("parameters")]
    public Parameters Parameters { get; set; } = new();

    [JsonPropertyName("intent")]
    public Intent Intent { get; set; } = new();
}

public class Parameters
{
    [JsonPropertyName("geo-city")]
    public string? City { get; set; }

    [JsonPropertyName("date-time")]
    public string? DateTime { get; set; }

    [JsonPropertyName("date-period")]
    [JsonConverter(typeof(DatePeriodConverter))]
    public DatePeriod? DatePeriod { get; set; }
}

public class DatePeriod
{
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }
}

public class Intent
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public class DatePeriodConverter : JsonConverter<DatePeriod?>
{
    public override DatePeriod? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
        }
        else if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<DatePeriod>(ref reader, options);
        }
        
        throw new JsonException($"Unexpected token type {reader.TokenType} when parsing DatePeriod");
    }

    public override void Write(Utf8JsonWriter writer, DatePeriod? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        JsonSerializer.Serialize(writer, value, options);
    }
} 