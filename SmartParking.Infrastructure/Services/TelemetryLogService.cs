using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Contracts;
using SmartParking.Application.Services;
using SmartParking.Domain.Enums;

namespace SmartParking.Infrastructure.Services;

public sealed class TelemetryLogService : ITelemetryLogService
{
    private readonly SmartParkingDbContext _db;

    public TelemetryLogService(SmartParkingDbContext db) => _db = db;

    public async Task<List<TelemetryLogDto>> GetRecentAsync(int take, string? slotLabel, string? deviceCode, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 500);

        var q = _db.TelemetryLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(slotLabel))
            q = q.Where(x => x.SlotLabel == slotLabel);

        if (!string.IsNullOrWhiteSpace(deviceCode))
            q = q.Where(x => x.DeviceCode == deviceCode);

        return await q
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Take(take)
            .Select(x => new TelemetryLogDto(
                x.DeviceCode,
                x.SensorCode,
                x.SlotLabel,
                x.DistanceCm,
                x.StatusAfter.ToString(),
                x.ReceivedAtUtc,
                x.DeviceTs
            ))
            .ToListAsync(ct);
    }

    public async Task<List<SlotOccupancyStatsDto>> GetSlotOccupancyStatsAsync(int hoursBack, CancellationToken ct)
    {
        var since = DateTimeOffset.Now.AddHours(-hoursBack);

        // Get all telemetry logs within the time window, ordered by time
        var logs = await _db.TelemetryLogs
            .AsNoTracking()
            .Where(x => x.ReceivedAtUtc >= since && x.SlotLabel != null)
            .OrderBy(x => x.SlotLabel)
            .ThenBy(x => x.ReceivedAtUtc)
            .ToListAsync(ct);

        var result = new List<SlotOccupancyStatsDto>();

        // Group by slot and calculate occupancy periods
        var groupedBySlot = logs.GroupBy(x => x.SlotLabel!);

        foreach (var slotGroup in groupedBySlot)
        {
            var slotLabel = slotGroup.Key;
            var slotLogs = slotGroup.ToList();
            var periods = new List<OccupancyPeriodDto>();

            DateTimeOffset? occupiedStart = null;

            for (int i = 0; i < slotLogs.Count; i++)
            {
                var log = slotLogs[i];

                if (log.StatusAfter == SlotStatus.Occupied)
                {
                    // Start of occupied period
                    if (occupiedStart == null)
                    {
                        occupiedStart = log.ReceivedAtUtc;
                    }
                }
                else if (occupiedStart != null)
                {
                    // End of occupied period (status changed from Occupied to Free/Offline)
                    var endTime = log.ReceivedAtUtc;
                    var duration = (endTime - occupiedStart.Value).TotalMinutes;
                    periods.Add(new OccupancyPeriodDto(occupiedStart.Value, endTime, duration));
                    occupiedStart = null;
                }
            }

            // Handle case where slot is still occupied (no end event)
            if (occupiedStart != null)
            {
                var duration = (DateTimeOffset.Now - occupiedStart.Value).TotalMinutes;
                periods.Add(new OccupancyPeriodDto(occupiedStart.Value, null, duration));
            }

            var totalOccupiedMinutes = periods.Sum(p => p.DurationMinutes);
            result.Add(new SlotOccupancyStatsDto(slotLabel, totalOccupiedMinutes, periods));
        }

        return result;
    }

    public async Task<List<SlotHourlyOccupancyDto>> GetSlotHourlyOccupancyAsync(CancellationToken ct)
    {
        // Get start of current day (local)
        var today = DateTimeOffset.Now.Date;
        var startOfDay = new DateTimeOffset(today, DateTimeOffset.Now.Offset);
        var now = DateTimeOffset.Now;

        // Get all slots first to ensure we have data for all slots
        var allSlots = await _db.Slots
            .AsNoTracking()
            .Select(s => s.Label)
            .ToListAsync(ct);

        // Get all telemetry logs for today
        var logs = await _db.TelemetryLogs
            .AsNoTracking()
            .Where(x => x.ReceivedAtUtc >= startOfDay && x.SlotLabel != null)
            .OrderBy(x => x.SlotLabel)
            .ThenBy(x => x.ReceivedAtUtc)
            .ToListAsync(ct);

        var result = new List<SlotHourlyOccupancyDto>();

        // Group logs by slot
        var groupedBySlot = logs.GroupBy(x => x.SlotLabel!).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var slotLabel in allSlots)
        {
            var hourlyMinutes = new List<double>(new double[24]); // Initialize 24 hours with 0

            if (!groupedBySlot.TryGetValue(slotLabel, out var slotLogs) || slotLogs.Count == 0)
            {
                result.Add(new SlotHourlyOccupancyDto(slotLabel, hourlyMinutes));
                continue;
            }

            // Track occupancy periods and distribute across hours
            DateTimeOffset? occupiedStart = null;

            for (int i = 0; i < slotLogs.Count; i++)
            {
                var log = slotLogs[i];

                if (log.StatusAfter == SlotStatus.Occupied)
                {
                    if (occupiedStart == null)
                    {
                        occupiedStart = log.ReceivedAtUtc;
                    }
                }
                else if (occupiedStart != null)
                {
                    // End of occupied period - distribute minutes across hours
                    DistributeMinutesAcrossHours(hourlyMinutes, occupiedStart.Value, log.ReceivedAtUtc, startOfDay);
                    occupiedStart = null;
                }
            }

            // Handle ongoing occupation (still occupied now)
            if (occupiedStart != null)
            {
                DistributeMinutesAcrossHours(hourlyMinutes, occupiedStart.Value, now, startOfDay);
            }

            result.Add(new SlotHourlyOccupancyDto(slotLabel, hourlyMinutes));
        }

        return result;
    }

    private static void DistributeMinutesAcrossHours(List<double> hourlyMinutes, DateTimeOffset start, DateTimeOffset end, DateTimeOffset startOfDay)
    {
        // Clamp to today
        if (start < startOfDay) start = startOfDay;
        if (end > startOfDay.AddDays(1)) end = startOfDay.AddDays(1);

        var current = start;
        while (current < end)
        {
            var hour = current.Hour;
            if (hour < 0 || hour >= 24) break;

            // Calculate end of current hour
            var hourEnd = startOfDay.AddHours(hour + 1);
            var periodEnd = end < hourEnd ? end : hourEnd;

            // Add minutes to this hour
            var minutes = (periodEnd - current).TotalMinutes;
            hourlyMinutes[hour] += minutes;

            current = periodEnd;
        }
    }
}
