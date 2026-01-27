namespace SmartParking.Application.Contracts;

public sealed record TelemetryIngestDto(
    string DeviceCode,
    string SensorCode,
    double DistanceCm,
    DateTimeOffset? Ts
);
