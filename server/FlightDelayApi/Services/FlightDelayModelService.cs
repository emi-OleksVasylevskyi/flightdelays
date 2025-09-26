using FlightDelayApi.Models;
using System.Text.Json;
using System.Text;

namespace FlightDelayApi.Services;

public interface IFlightDelayModelService
{
    Task<FlightDelayPrediction> PredictDelayAsync(FlightDelayRequest request);
    Task InitializeAsync();
    void Dispose();
}

public class FlightDelayModelService : IFlightDelayModelService, IDisposable
{
    private readonly ILogger<FlightDelayModelService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _pythonModelServiceUrl;
    private bool _initialized;

    public FlightDelayModelService(ILogger<FlightDelayModelService> logger, IConfiguration configuration, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _pythonModelServiceUrl = configuration.GetValue<string>("PythonModelServiceUrl") ?? "http://localhost:5108";
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            // Test connection to Python model service
            var response = await _httpClient.GetAsync($"{_pythonModelServiceUrl}/health");
            response.EnsureSuccessStatusCode();
            
            var healthContent = await response.Content.ReadAsStringAsync();
            var healthData = JsonSerializer.Deserialize<JsonElement>(healthContent);
            
            if (healthData.GetProperty("model_loaded").GetBoolean())
            {
                _logger.LogInformation("Connected to Python model service successfully - using ACTUAL trained model");
                _initialized = true;
            }
            else
            {
                throw new InvalidOperationException("Python model service indicates model is not loaded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Python model service connection");
            throw;
        }
    }

    public async Task<FlightDelayPrediction> PredictDelayAsync(FlightDelayRequest request)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Model service not initialized");
        }

        try
        {
            // Prepare request for Python model service
            var pythonRequest = new
            {
                DayOfWeek = request.DayOfWeek,
                OriginAirportID = request.OriginAirportID,
                DestAirportID = request.DestAirportID,
                Month = request.Month,
                Carrier = request.Carrier,
                CRSDepTime = request.CRSDepTime
            };

            var jsonContent = JsonSerializer.Serialize(pythonRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Call Python model service
            var response = await _httpClient.PostAsync($"{_pythonModelServiceUrl}/predict", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var pythonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Extract prediction results
            var prediction = new FlightDelayPrediction(
                DelayProbability: pythonResponse.GetProperty("DelayProbability").GetDouble(),
                ConfidencePercent: pythonResponse.GetProperty("ConfidencePercent").GetDouble(),
                LogisticProbability: pythonResponse.GetProperty("LogisticProbability").GetDouble(),
                HistoricalPairProbability: pythonResponse.GetProperty("HistoricalPairProbability").GetDouble(),
                PredictionMethod: pythonResponse.GetProperty("PredictionMethod").GetString() ?? "Unknown"
            );

            _logger.LogDebug("Generated prediction using trained model: {DelayProb:P1} (confidence: {Confidence:P1})", 
                prediction.DelayProbability, prediction.ConfidencePercent / 100.0);

            return prediction;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to communicate with Python model service");
            throw new InvalidOperationException("Model service unavailable", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting flight delay for request {@Request}", request);
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _logger.LogDebug("FlightDelayModelService disposed");
    }
}