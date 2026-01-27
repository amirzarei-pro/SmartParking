using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Contracts;
using SmartParking.Application.Services;
using SmartParking.Domain.Enums;

namespace SmartParking.Infrastructure.Services;

public sealed class IotService : IIotService
{
    private readonly SmartParkingDbContext _db;

    public IotService(SmartParkingDbContext db) => _db = db;

    public async Task<TelemetryIngestResultDto> IngestAsync(TelemetryIngestDto dto, string deviceKey, CancellationToken ct)
    {
        // 1) Device
        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.Code == dto.DeviceCode, ct);

        if (device is null)
            throw new InvalidOperationException("Device not found.");

        // فعلاً ساده: کلید دستگاه را مستقیم با DB چک می‌کنیم
        // بعداً اگر خواستید Hash می‌کنیم
        if (!string.Equals(device.ApiKey, deviceKey, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid device key.");

        device.LastSeenAt = DateTimeOffset.UtcNow;

        // 2) Sensor
        var sensor = await _db.Sensors
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id && s.SensorCode == dto.SensorCode, ct);

        if (sensor is null)
            throw new InvalidOperationException("Sensor not found.");

        sensor.LastSeenAt = DateTimeOffset.UtcNow;

        // 3) Slot (mapped by SensorId)
        var slot = await _db.Slots
            .FirstOrDefaultAsync(s => s.SensorId == sensor.Id, ct);

        if (slot is null)
        {
            await _db.SaveChangesAsync(ct);
            return new TelemetryIngestResultDto(false, null, null, null, null);
        }

        var thresholdCm = slot.OccupiedThresholdCm <= 0 ? 15.0 : slot.OccupiedThresholdCm;
        var newStatus = dto.DistanceCm < thresholdCm ? SlotStatus.Occupied : SlotStatus.Free;

        slot.Status = newStatus;
        slot.LastDistanceCm = dto.DistanceCm;
        slot.LastUpdateAt = DateTimeOffset.UtcNow;

_db.TelemetryLogs.Add(new SmartParking.Domain.Entities.TelemetryLog
{
    DeviceCode = device.Code,
    SensorCode = sensor.SensorCode,
    SlotLabel = slot.Label,
    DistanceCm = dto.DistanceCm,
    StatusAfter = slot.Status,
    ReceivedAtUtc = DateTimeOffset.UtcNow,
    DeviceTs = dto.Ts
});


        await _db.SaveChangesAsync(ct);

        return new TelemetryIngestResultDto(
            true,
            slot.Label,
            slot.Status.ToString(),
            slot.LastDistanceCm,
            slot.LastUpdateAt
        );
    }
}
