namespace SmartParking.Application.Contracts;

public sealed record TelemetryLogDto(
    string DeviceCode,
    string SensorCode,
    string? SlotLabel,
    double DistanceCm,
    string StatusAfter,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? DeviceTs
);
