using System.Text.Json.Serialization;

namespace WeatherWebhook.Models;

public class OpenWeatherResponse
{
    [JsonPropertyName("weather")]
    public List<Weather> Weather { get; set; } = new();

    [JsonPropertyName("main")]
    public Main Main { get; set; } = new();

    [JsonPropertyName("dt")]
    public long Timestamp { get; set; }
}

public class Weather
{
    [JsonPropertyName("main")]
    public string Main { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class Main
{
    [JsonPropertyName("temp")]
    public double Temperature { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }
}

public class ForecastResponse
{
    [JsonPropertyName("list")]
    public List<ForecastItem> List { get; set; } = new();
}

public class ForecastItem
{
    [JsonPropertyName("dt")]
    public long Timestamp { get; set; }

    [JsonPropertyName("weather")]
    public List<Weather> Weather { get; set; } = new();

    [JsonPropertyName("main")]
    public Main Main { get; set; } = new();
} 