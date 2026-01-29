namespace SmartParking.Application.Contracts;

public sealed record SlotOccupancyStatsDto(
    string SlotLabel,
    double TotalOccupiedMinutes,
    List<OccupancyPeriodDto> OccupancyPeriods
);

public sealed record OccupancyPeriodDto(
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    double DurationMinutes
);

/// <summary>
/// Hourly occupancy data for a single slot (24 hours)
/// </summary>
public sealed record SlotHourlyOccupancyDto(
    string SlotLabel,
    List<double> HourlyOccupancyMinutes // 24 values, one per hour (0-23)
);
