# Flight Delay API Server

This .NET 9 Web API provides flight delay predictions based on historical flight data from 2013. The API allows users to predict the probability of flight delays exceeding 15 minutes based on the day of the week and arrival airport.

## Architecture Overview

The server implements a clean architecture with the following layers:

- **API Layer**: Minimal API endpoints using ASP.NET Core
- **Service Layer**: Business logic services for predictions and data access
- **Models**: Data transfer objects and domain models
- **Caching**: In-memory caching for improved performance

## Key Features

### 1. Flight Delay Prediction
- **Endpoint**: `POST /api/predict-delay`
- **Purpose**: Predicts flight delay probability based on day of week and arrival airport
- **Caching**: Results cached for 60 minutes to improve response times
- **Validation**: Input validation for day of week (1-7 range)

### 2. Airport Data
- **Endpoint**: `GET /api/airports`
- **Purpose**: Returns list of all available airports sorted alphabetically
- **Caching**: Results cached for 24 hours (airport data is relatively static)

### 3. Health Check
- **Endpoint**: `GET /health`
- **Purpose**: Basic health monitoring endpoint

## Services

### FlightDelayModelService
- Handles machine learning model initialization and predictions
- Loads the trained model on application startup
- Processes prediction requests and returns probability calculations

### AirportService
- Manages airport data retrieval and processing
- Provides sorted list of airports for frontend consumption

### CacheService
- Implements distributed caching using IMemoryCache
- Provides cache key generation and management
- Configurable expiration times for different data types

## Models

### FlightDelayRequest
- Input model for delay predictions
- Contains day of week and airport identifier

### FlightDelayResponse
- Response wrapper for API calls
- Includes success status, data, and error messages

### FlightDelayPrediction
- Contains prediction results with probability and confidence metrics

### Airport
- Represents airport data with identifier and name

## Configuration

The API uses standard ASP.NET Core configuration with:
- Development and production settings
- Logging configuration (Console and Debug providers)
- OpenAPI integration for development environment
- HTTPS redirection for security

## Dependencies

- **Microsoft.AspNetCore.OpenApi**: OpenAPI documentation generation
- **CsvHelper**: CSV file processing for airport data
- **Memory Caching**: Built-in ASP.NET Core memory cache
- **Logging**: ASP.NET Core logging framework

## Running the Server

The server can be run using:
```bash
dotnet run --project FlightDelayApi
```

Or using Visual Studio's F5 debugging.

## Testing

Unit tests are included in the `FlightDelayApi.Tests` project to ensure API reliability and correctness.

## Performance Considerations

- **Caching Strategy**: Two-tier caching with different expiration times
  - Predictions: 60 minutes (user behavior driven)
  - Airports: 24 hours (static reference data)
- **Async Operations**: All service operations use async/await patterns
- **Memory Management**: Singleton service registration for shared resources

## Error Handling

- Comprehensive exception handling with proper HTTP status codes
- Structured logging for debugging and monitoring
- Graceful degradation with meaningful error messages

## Security

- HTTPS redirection enabled
- Input validation on all endpoints
- No sensitive data exposure in error responses