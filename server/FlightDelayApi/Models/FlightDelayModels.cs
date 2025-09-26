namespace FlightDelayApi.Models;

public record FlightDelayRequest(
    int DayOfWeek,
    int OriginAirportID,
    int DestAirportID,
    int Month = 1,
    string Carrier = "AA",
    int CRSDepTime = 800
);

public record FlightDelayPrediction(
    double DelayProbability,
    double ConfidencePercent,
    double LogisticProbability,
    double HistoricalPairProbability,
    string PredictionMethod = "Combined"
);

public record FlightDelayResponse(
    bool Success,
    FlightDelayPrediction? Prediction = null,
    string? Error = null
);