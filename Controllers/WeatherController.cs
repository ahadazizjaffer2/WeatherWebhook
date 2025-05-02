using Microsoft.AspNetCore.Mvc;
using WeatherWebhook.Models;
using WeatherWebhook.Services;

namespace WeatherWebhook.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(IWeatherService weatherService, ILogger<WeatherController> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
        _logger.LogInformation("WeatherController initialized");
    }

    [HttpPost]
    public async Task<ActionResult<WeatherResponse>> GetWeather([FromBody] DialogflowWebhookRequest request)
    {
        _logger.LogInformation("Received webhook request for city: {City}, intent: {Intent}", 
            request.QueryResult.Parameters.City,
            request.QueryResult.Intent.DisplayName);

        try
        {
            if (string.IsNullOrEmpty(request.QueryResult.Parameters.City))
            {
                _logger.LogWarning("Request rejected: Missing city parameter");
                return BadRequest(new WeatherResponse { FulfillmentText = "Please provide a city name." });
            }

            DateTime? startDate = null;
            DateTime? endDate = null;

            // Try to parse date-time parameter first
            if (!string.IsNullOrEmpty(request.QueryResult.Parameters.DateTime))
            {
                _logger.LogDebug("Processing date-time parameter: {DateTime}", request.QueryResult.Parameters.DateTime);
                if (DateTime.TryParse(request.QueryResult.Parameters.DateTime, out var parsedDateTime))
                {
                    startDate = parsedDateTime;
                    _logger.LogDebug("Successfully parsed date-time: {ParsedDateTime}", parsedDateTime.ToString("yyyy-MM-dd"));
                }
                else
                {
                    _logger.LogWarning("Failed to parse date-time parameter: {DateTime}", request.QueryResult.Parameters.DateTime);
                }
            }

            // Process date period if it exists and has valid dates
            if (request.QueryResult.Parameters.DatePeriod != null && 
                !string.IsNullOrEmpty(request.QueryResult.Parameters.DatePeriod.StartDate))
            {
                _logger.LogDebug("Processing date period: Start={StartDate}, End={EndDate}",
                    request.QueryResult.Parameters.DatePeriod.StartDate,
                    request.QueryResult.Parameters.DatePeriod.EndDate);

                if (DateTime.TryParse(request.QueryResult.Parameters.DatePeriod.StartDate, out var parsedStartDate))
                {
                    startDate = parsedStartDate;
                    _logger.LogDebug("Successfully parsed start date: {StartDate}", parsedStartDate.ToString("yyyy-MM-dd"));
                }
                else
                {
                    _logger.LogWarning("Failed to parse start date: {StartDate}", request.QueryResult.Parameters.DatePeriod.StartDate);
                }

                if (!string.IsNullOrEmpty(request.QueryResult.Parameters.DatePeriod.EndDate) &&
                    DateTime.TryParse(request.QueryResult.Parameters.DatePeriod.EndDate, out var parsedEndDate))
                {
                    endDate = parsedEndDate;
                    _logger.LogDebug("Successfully parsed end date: {EndDate}", parsedEndDate.ToString("yyyy-MM-dd"));
                }
                else
                {
                    _logger.LogWarning("Failed to parse end date: {EndDate}", request.QueryResult.Parameters.DatePeriod.EndDate);
                }
            }

            _logger.LogInformation("Calling weather service for city: {City}, intent: {Intent}, startDate: {StartDate}, endDate: {EndDate}",
                request.QueryResult.Parameters.City,
                request.QueryResult.Intent.DisplayName,
                startDate?.ToString("yyyy-MM-dd"),
                endDate?.ToString("yyyy-MM-dd"));

            var response = await _weatherService.GetWeatherAsync(
                request.QueryResult.Parameters.City,
                request.QueryResult.Intent.DisplayName,
                startDate,
                endDate
            );

            _logger.LogInformation("Successfully processed weather request for {City}", request.QueryResult.Parameters.City);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing weather request for city: {City}", request.QueryResult.Parameters.City);
            return StatusCode(500, new WeatherResponse
            {
                FulfillmentText = "Sorry, I encountered an error while fetching the weather information."
            });
        }
    }
} 