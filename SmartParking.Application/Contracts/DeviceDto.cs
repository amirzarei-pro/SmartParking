namespace SmartParking.Application.Contracts;

public sealed record DeviceDto(
    Guid Id,
    string Code,
    string ApiKey,
    DateTimeOffset LastSeenAt,
    List<SensorInfoDto> Sensors
);

public sealed record SensorInfoDto(
    Guid Id,
    string SensorCode,
    string? SlotLabel
);
