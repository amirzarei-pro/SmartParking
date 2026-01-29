namespace SmartParking.Application.Contracts;

public sealed record DeviceConnectResultDto(
    Guid DeviceId,
    string DeviceCode,
    int SensorCount,
    List<SensorSlotInfoDto> Sensors
);

public sealed record SensorSlotInfoDto(
    Guid SensorId,
    string SensorCode,
    SlotInfoDto? Slot
);

public sealed record SlotInfoDto(
    Guid SlotId,
    string Label,
    string Zone,
    string Status,
    double OccupiedThresholdCm
);
