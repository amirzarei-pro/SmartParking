namespace SmartParking.Application.Contracts;

public sealed record DeviceRegisterDto(
    string DeviceCode,
    List<SensorRegisterDto> Sensors
);

public sealed record SensorRegisterDto(
    string SensorCode,
    SlotRegisterDto? Slot
);

public sealed record SlotRegisterDto(
    string Label,
    string Zone,
    string Status,
    double OccupiedThresholdCm
);
