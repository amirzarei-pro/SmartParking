using SmartParking.Application.Contracts;

namespace SmartParking.Application.Services;

public interface ITelemetryLogService
{
    Task<List<TelemetryLogDto>> GetRecentAsync(int take, string? slotLabel, string? deviceCode, CancellationToken ct);
    
    /// <summary>
    /// Gets occupancy statistics for all slots within a given time window.
    /// </summary>
    Task<List<SlotOccupancyStatsDto>> GetSlotOccupancyStatsAsync(int hoursBack, CancellationToken ct);
    
    /// <summary>
    /// Gets hourly occupancy data for all slots for the specified day (24 hours).
    /// Returns minutes occupied per hour for each slot.
    /// </summary>
    /// <param name="date">The date to get occupancy for. If null, uses today.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<List<SlotHourlyOccupancyDto>> GetSlotHourlyOccupancyAsync(DateOnly? date, CancellationToken ct);
}
