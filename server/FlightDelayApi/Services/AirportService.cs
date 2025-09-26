using FlightDelayApi.Models;
using CsvHelper;
using System.Globalization;

namespace FlightDelayApi.Services;

public interface IAirportService
{
    Task<IEnumerable<Airport>> GetAirportsAsync();
    Task<Airport?> GetAirportByIdAsync(int airportId);
}

public class AirportService : IAirportService
{
    private readonly ILogger<AirportService> _logger;
    private readonly string _airportDataPath;
    private List<Airport>? _cachedAirports;

    public AirportService(ILogger<AirportService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _airportDataPath = configuration["AirportDataPath"] ?? "/workspaces/flightdelays/data/airports.csv";
    }

    public async Task<IEnumerable<Airport>> GetAirportsAsync()
    {
        if (_cachedAirports == null)
        {
            await LoadAirportsAsync();
        }

        return _cachedAirports?.OrderBy(a => a.AirportName) ?? Enumerable.Empty<Airport>();
    }

    public async Task<Airport?> GetAirportByIdAsync(int airportId)
    {
        if (_cachedAirports == null)
        {
            await LoadAirportsAsync();
        }

        return _cachedAirports?.FirstOrDefault(a => a.AirportID == airportId);
    }

    private async Task LoadAirportsAsync()
    {
        try
        {
            using var reader = new StringReader(await File.ReadAllTextAsync(_airportDataPath));
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            _cachedAirports = csv.GetRecords<Airport>().ToList();
            _logger.LogInformation("Loaded {Count} airports from {Path}", _cachedAirports.Count, _airportDataPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load airports from {Path}", _airportDataPath);
            _cachedAirports = new List<Airport>();
        }
    }
}