using FlightDelayBlazor.Models;
using System.Text.Json;
using System.Text;

namespace FlightDelayBlazor.Services;

public interface IFlightDelayApiService
{
    Task<List<Airport>> GetAirportsAsync();
    Task<FlightDelayResponse> PredictDelayAsync(FlightDelayRequest request);
}

public class FlightDelayApiService : IFlightDelayApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FlightDelayApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FlightDelayApiService(HttpClient httpClient, ILogger<FlightDelayApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<List<Airport>> GetAirportsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching airports from API");
            _logger.LogInformation("HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            
            var response = await _httpClient.GetAsync("/api/airports");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var airports = JsonSerializer.Deserialize<List<Airport>>(content, _jsonOptions);
                _logger.LogInformation("Successfully fetched {Count} airports", airports?.Count ?? 0);
                return airports ?? new List<Airport>();
            }
            else
            {
                _logger.LogError("Failed to fetch airports. Status: {StatusCode}", response.StatusCode);
                return new List<Airport>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching airports from API");
            return new List<Airport>();
        }
    }

    public async Task<FlightDelayResponse> PredictDelayAsync(FlightDelayRequest request)
    {
        try
        {
            _logger.LogInformation("Requesting delay prediction for {@Request}", request);
            
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/predict-delay", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<FlightDelayResponse>(responseContent, _jsonOptions);
                
                _logger.LogInformation("Successfully received prediction: {Probability:P1} confidence", 
                    result?.Prediction?.DelayProbability);
                
                return result ?? new FlightDelayResponse(false, null, "Invalid response format");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("API request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                
                return new FlightDelayResponse(false, null, 
                    $"API request failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting delay prediction");
            return new FlightDelayResponse(false, null, $"Request error: {ex.Message}");
        }
    }
}