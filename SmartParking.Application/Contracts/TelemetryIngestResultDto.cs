namespace SmartParking.Application.Contracts;

public sealed record TelemetryIngestResultDto(
    bool Updated,
    string? SlotLabel,
    string? Status,
    double? DistanceCm,
    DateTimeOffset? UpdatedAt
);
