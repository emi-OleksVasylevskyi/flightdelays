using FlightDelayApi.Services;
using FlightDelayApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Flight Delay Prediction API", 
        Version = "v1",
        Description = "API for predicting flight delays based on historical data and machine learning models"
    });
});
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Add CORS policy for Blazor frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClientPolicy", builder =>
        builder.WithOrigins("http://localhost:5000", "https://localhost:5001", "http://localhost:5016", "https://localhost:7109")
               .AllowAnyMethod()
               .AllowAnyHeader());
});

// Register custom services
builder.Services.AddSingleton<IFlightDelayModelService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<FlightDelayModelService>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    httpClient.Timeout = TimeSpan.FromSeconds(30); // Set reasonable timeout
    return new FlightDelayModelService(logger, configuration, httpClient);
});
builder.Services.AddSingleton<IAirportService, AirportService>();
builder.Services.AddSingleton<ICacheService, CacheService>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flight Delay API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();
app.UseCors("BlazorClientPolicy");

// Initialize the model service on startup
var modelService = app.Services.GetRequiredService<IFlightDelayModelService>();
await modelService.InitializeAsync();

// Flight delay prediction endpoint
app.MapPost("/api/predict-delay", async (
    [FromBody] FlightDelayRequest request,
    [FromServices] IFlightDelayModelService modelService,
    [FromServices] ICacheService cacheService,
    [FromServices] ILogger<Program> logger) =>
{
    try
    {
        // Validate request
        if (request.DayOfWeek < 1 || request.DayOfWeek > 7)
        {
            return Results.BadRequest(new FlightDelayResponse(false, null, "DayOfWeek must be between 1-7"));
        }

        // Check cache first
        var cacheKey = cacheService.GeneratePredictionCacheKey(request);
        var cachedPrediction = await cacheService.GetAsync<FlightDelayPrediction>(cacheKey);
        
        if (cachedPrediction != null)
        {
            logger.LogInformation("Returning cached prediction for {CacheKey}", cacheKey);
            return Results.Ok(new FlightDelayResponse(true, cachedPrediction));
        }

        // Get prediction from model
        var prediction = await modelService.PredictDelayAsync(request);
        
        // Cache the result
        await cacheService.SetAsync(cacheKey, prediction, TimeSpan.FromMinutes(60));
        
        logger.LogInformation("Generated new prediction for request: {@Request}", request);
        return Results.Ok(new FlightDelayResponse(true, prediction));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error predicting flight delay for request: {@Request}", request);
        return Results.Problem("An error occurred while predicting flight delay");
    }
})
.WithName("PredictFlightDelay")
.WithSummary("Predict flight delay probability")
.WithDescription("Returns the probability of a flight being delayed more than 15 minutes along with confidence percentage");

// Airports list endpoint
app.MapGet("/api/airports", async (
    [FromServices] IAirportService airportService,
    [FromServices] ICacheService cacheService,
    [FromServices] ILogger<Program> logger) =>
{
    try
    {
        // Check cache first
        const string cacheKey = "all_airports";
        var cachedAirports = await cacheService.GetAsync<List<Airport>>(cacheKey);
        
        if (cachedAirports != null)
        {
            logger.LogInformation("Returning cached airports list ({Count} airports)", cachedAirports.Count);
            return Results.Ok(cachedAirports);
        }

        // Get airports from service
        var airports = await airportService.GetAirportsAsync();
        var airportsList = airports.ToList();
        
        // Cache the result for longer since airport data doesn't change often
        await cacheService.SetAsync(cacheKey, airportsList, TimeSpan.FromHours(24));
        
        logger.LogInformation("Generated new airports list ({Count} airports)", airportsList.Count);
        return Results.Ok(airportsList);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving airports list");
        return Results.Problem("An error occurred while retrieving airports list");
    }
})
.WithName("GetAirports")
.WithSummary("Get all airports")
.WithDescription("Returns a list of all airports sorted alphabetically by name");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
.WithName("HealthCheck");

app.Run();

// Make Program class accessible for testing
public partial class Program { }
