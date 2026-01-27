namespace SmartParking.Application.Contracts;

public sealed record SlotDto(
    string Label,
    string Zone,
    string Status,
    double LastDistanceCm,
    DateTimeOffset LastUpdateAt,
    string? DeviceCode,
    string? SensorCode,
    double OccupiedThresholdCm
);
