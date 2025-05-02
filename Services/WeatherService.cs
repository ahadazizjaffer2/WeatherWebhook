using System.Text.Json;
using WeatherWebhook.Models;

namespace WeatherWebhook.Services;

public interface IWeatherService
{
    Task<WeatherResponse> GetWeatherAsync(string city, string intent, DateTime? date = null, DateTime? endDate = null);
}

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, IConfiguration configuration, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = _configuration["OpenWeatherMap:ApiKey"] ?? throw new ArgumentNullException("OpenWeatherMap:ApiKey");
        _logger.LogInformation("WeatherService initialized with API key");
    }

    public async Task<WeatherResponse> GetWeatherAsync(string city, string intent, DateTime? date = null, DateTime? endDate = null)
    {
        _logger.LogInformation("Getting weather for city: {City}, intent: {Intent}, date: {Date}, endDate: {EndDate}", 
            city, intent, date?.ToString("yyyy-MM-dd"), endDate?.ToString("yyyy-MM-dd"));

        try
        {
            if (intent.Equals("Get Current Weather", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Processing current weather request for {City}", city);
                return await GetCurrentWeatherAsync(city);
            }
            else if (intent.Equals("Get Weather Forecast", StringComparison.OrdinalIgnoreCase))
            {
                var today = DateTime.UtcNow.Date;
                var startDate = date ?? today;
                var forecastEndDate = endDate ?? startDate.AddDays(8);

                _logger.LogInformation("Processing forecast request for {City} from {StartDate} to {EndDate}", 
                    city, startDate.ToString("yyyy-MM-dd"), forecastEndDate.ToString("yyyy-MM-dd"));
                return await GetForecastAsync(city, startDate, forecastEndDate);
            }
            else
            {
                _logger.LogWarning("Unknown intent: {Intent}", intent);
                throw new ArgumentException($"Unknown intent: {intent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather for {City}", city);
            throw;
        }
    }

    private async Task<WeatherResponse> GetCurrentWeatherAsync(string city)
    {
        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={_apiKey}&units=metric";
            _logger.LogDebug("Calling OpenWeather API for current weather: {Url}", url);

            var httpResponse = await _httpClient.GetAsync(url);
            httpResponse.EnsureSuccessStatusCode();

            var content = await httpResponse.Content.ReadAsStringAsync();
            _logger.LogDebug("Received response from OpenWeather API: {Content}", content);

            var weather = JsonSerializer.Deserialize<OpenWeatherResponse>(content);

            if (weather == null || !weather.Weather.Any())
            {
                _logger.LogError("Failed to get weather data for {City}. Response was null or empty", city);
                throw new Exception("Failed to get weather data");
            }

            var description = GetFriendlyDescription(weather.Weather[0].Description);
            var weatherResponse = new WeatherResponse
            {
                FulfillmentText = $"The current weather in {city} is {description} with a temperature of {Math.Round(weather.Main.Temperature)}°C."
            };

            _logger.LogInformation("Successfully retrieved current weather for {City}: {Description} at {Temperature}°C",
                city, description, Math.Round(weather.Main.Temperature));

            return weatherResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while getting current weather for {City}", city);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse weather response for {City}", city);
            throw;
        }
    }

    private async Task<WeatherResponse> GetForecastAsync(string city, DateTime startDate, DateTime endDate)
    {
        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/forecast?q={city}&appid={_apiKey}&units=metric";
            _logger.LogDebug("Calling OpenWeather API for forecast: {Url}", url);

            var httpResponse = await _httpClient.GetAsync(url);
            httpResponse.EnsureSuccessStatusCode();

            var content = await httpResponse.Content.ReadAsStringAsync();
            _logger.LogDebug("Received forecast response from OpenWeather API: {Content}", content);

            var forecast = JsonSerializer.Deserialize<ForecastResponse>(content);

            if (forecast == null || !forecast.List.Any())
            {
                _logger.LogError("Failed to get forecast data for {City}. Response was null or empty", city);
                throw new Exception("Failed to get forecast data");
            }

            // Group forecast items by date and get the most common weather condition for each day
            var relevantDays = forecast.List
                .Where(f => DateTimeOffset.FromUnixTimeSeconds(f.Timestamp).DateTime.Date >= startDate.Date &&
                           DateTimeOffset.FromUnixTimeSeconds(f.Timestamp).DateTime.Date <= endDate.Date)
                .GroupBy(f => DateTimeOffset.FromUnixTimeSeconds(f.Timestamp).DateTime.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Weather = g.SelectMany(f => f.Weather)
                        .GroupBy(w => w.Description)
                        .OrderByDescending(w => w.Count())
                        .First()
                        .First()
                })
                .ToList();

            if (!relevantDays.Any())
            {
                _logger.LogWarning("No forecast available for {City} between {StartDate} and {EndDate}",
                    city, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
                return new WeatherResponse
                {
                    FulfillmentText = $"No forecast available for {city} between {startDate:MMM d} and {endDate:MMM d}."
                };
            }

            var descriptions = relevantDays
                .Select(d => GetFriendlyDescription(d.Weather.Description))
                .Distinct()
                .ToList();

            var dateRange = startDate.Date == endDate.Date
                ? startDate.ToString("MMMM d")
                : $"{startDate:MMMM d} to {endDate:MMM d}";

            var weatherResponse = new WeatherResponse
            {
                FulfillmentText = $"The forecast for {city} from {dateRange} is {string.Join(" with ", descriptions)}."
            };

            _logger.LogInformation("Successfully retrieved forecast for {City} from {StartDate} to {EndDate}: {Descriptions}",
                city, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), string.Join(", ", descriptions));

            return weatherResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while getting forecast for {City}", city);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse forecast response for {City}", city);
            throw;
        }
    }

    private string GetFriendlyDescription(string description)
    {
        _logger.LogDebug("Converting weather description: {OriginalDescription}", description);
        var friendlyDescription = description.ToLower() switch
        {
            "clear sky" => "clear and sunny",
            "few clouds" => "partly cloudy",
            "scattered clouds" => "cloudy",
            "broken clouds" => "mostly cloudy",
            "shower rain" => "rainy",
            "rain" => "rainy",
            "thunderstorm" => "stormy",
            "snow" => "snowy",
            "mist" => "misty",
            _ => description
        };
        _logger.LogDebug("Converted to friendly description: {FriendlyDescription}", friendlyDescription);
        return friendlyDescription;
    }
} 