using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Contracts;
using SmartParking.Application.Services;

namespace SmartParking.Infrastructure.Services;

public sealed class SlotService : ISlotService
{
    private readonly SmartParkingDbContext _db;

    public SlotService(SmartParkingDbContext db) => _db = db;

    public async Task<List<SlotDto>> GetAllAsync(CancellationToken ct)
    {
        return await _db.Slots
            .Include(s => s.Sensor)
            .ThenInclude(sn => sn!.Device)
            .OrderBy(s => s.Label)
            .Select(s => new SlotDto(
                        s.Label,
                        s.Zone,
                        s.Status.ToString(),
                        s.LastDistanceCm,
                        s.LastUpdateAt,
                        s.Sensor != null ? s.Sensor.Device.Code : null,
                        s.Sensor != null ? s.Sensor.SensorCode : null,
                        s.OccupiedThresholdCm
                    )
                )

            .ToListAsync(ct);
    }

    public async Task<bool> UpdateThresholdAsync(string slotLabel, double thresholdCm, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slotLabel))
            return false;

        if (thresholdCm < 1 || thresholdCm > 200)
            return false;

        var slot = await _db.Slots.FirstOrDefaultAsync(x => x.Label == slotLabel, ct);
        if (slot is null)
            return false;

        slot.OccupiedThresholdCm = thresholdCm;
        slot.LastUpdateAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

}
