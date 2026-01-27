using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Contracts;
using SmartParking.Application.Services;

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
}
