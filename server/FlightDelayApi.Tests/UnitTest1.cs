using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using FlightDelayApi.Models;
using FlightDelayApi.Services;
using System.Text.Json;
using System.Text;

namespace FlightDelayApi.Tests;

public class FlightDelayApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public FlightDelayApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetAirports_ReturnsSuccessAndCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/airports");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json; charset=utf-8", 
            response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetAirports_ReturnsAirportsList()
    {
        // Act
        var response = await _client.GetAsync("/api/airports");
        var content = await response.Content.ReadAsStringAsync();
        var airports = JsonSerializer.Deserialize<List<Airport>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(airports);
        Assert.NotEmpty(airports);
        
        // Check that airports are sorted alphabetically by name
        var sortedAirports = airports.OrderBy(a => a.AirportName).ToList();
        Assert.Equal(sortedAirports.Select(a => a.AirportName), 
                    airports.Select(a => a.AirportName));
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var healthStatus = JsonSerializer.Deserialize<JsonElement>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        Assert.Equal("Healthy", healthStatus.GetProperty("status").GetString());
    }
}

public class CacheServiceTests
{
    [Fact]
    public async Task SetAndGet_WithValidData_ReturnsCorrectValue()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CacheService>();
        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        
        var cacheService = new CacheService(memoryCache, logger);
        var testValue = new Airport(123, "Test Airport");

        // Act
        await cacheService.SetAsync("test-key", testValue);
        var result = await cacheService.GetAsync<Airport>("test-key");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testValue.AirportID, result.AirportID);
        Assert.Equal(testValue.AirportName, result.AirportName);
    }

    [Fact]
    public async Task Get_WithNonExistentKey_ReturnsNull()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CacheService>();
        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        
        var cacheService = new CacheService(memoryCache, logger);

        // Act
        var result = await cacheService.GetAsync<Airport>("non-existent-key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GeneratePredictionCacheKey_WithValidRequest_ReturnsConsistentKey()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<CacheService>();
        var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        
        var cacheService = new CacheService(memoryCache, logger);
        var request = new FlightDelayRequest(1, 10397, 10721, 6, "AA", 800);

        // Act
        var key1 = cacheService.GeneratePredictionCacheKey(request);
        var key2 = cacheService.GeneratePredictionCacheKey(request);

        // Assert
        Assert.Equal(key1, key2);
        Assert.Contains("prediction_", key1);
        Assert.Contains("1", key1);
        Assert.Contains("10397", key1);
        Assert.Contains("10721", key1);
    }
}

public class AirportServiceTests
{
    [Fact]
    public async Task GetAirports_ReturnsOrderedList()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AirportService>();
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AirportDataPath"] = "/workspaces/flightdelays/data/airports.csv"
            })
            .Build();

        var airportService = new AirportService(logger, configuration);

        // Act
        var airports = await airportService.GetAirportsAsync();
        var airportsList = airports.ToList();

        // Assert
        Assert.NotEmpty(airportsList);
        
        // Verify they are ordered by name
        var sortedNames = airportsList.Select(a => a.AirportName).OrderBy(name => name).ToList();
        var actualNames = airportsList.Select(a => a.AirportName).ToList();
        
        Assert.Equal(sortedNames, actualNames);
    }

    [Fact]
    public async Task GetAirportById_WithValidId_ReturnsAirport()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AirportService>();
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AirportDataPath"] = "/workspaces/flightdelays/data/airports.csv"
            })
            .Build();

        var airportService = new AirportService(logger, configuration);

        // Act
        var airport = await airportService.GetAirportByIdAsync(10397); // Atlanta

        // Assert
        Assert.NotNull(airport);
        Assert.Equal(10397, airport.AirportID);
        Assert.Contains("Atlanta", airport.AirportName);
    }

    [Fact]
    public async Task GetAirportById_WithInvalidId_ReturnsNull()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AirportService>();
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AirportDataPath"] = "/workspaces/flightdelays/data/airports.csv"
            })
            .Build();

        var airportService = new AirportService(logger, configuration);

        // Act
        var airport = await airportService.GetAirportByIdAsync(99999); // Non-existent ID

        // Assert
        Assert.Null(airport);
    }
}
