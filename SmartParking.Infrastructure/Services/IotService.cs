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

        device.LastSeenAt = DateTimeOffset.Now;

        // 2) Sensor
        var sensor = await _db.Sensors
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id && s.SensorCode == dto.SensorCode, ct);

        if (sensor is null)
            throw new InvalidOperationException("Sensor not found.");

        sensor.LastSeenAt = DateTimeOffset.Now;

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
        slot.LastUpdateAt = DateTimeOffset.Now;

_db.TelemetryLogs.Add(new SmartParking.Domain.Entities.TelemetryLog
{
    DeviceCode = device.Code,
    SensorCode = sensor.SensorCode,
    SlotLabel = slot.Label,
    DistanceCm = dto.DistanceCm,
    StatusAfter = slot.Status,
    ReceivedAtUtc = DateTimeOffset.Now,
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

    public async Task<DeviceConnectResultDto> ConnectAsync(string deviceCode, string deviceKey, CancellationToken ct)
    {
        var device = await _db.Devices
            .Include(d => d.Sensors)
                .ThenInclude(s => s.Slot)
            .FirstOrDefaultAsync(d => d.Code == deviceCode, ct);

        if (device is null)
            throw new InvalidOperationException("Device not found.");

        if (!string.Equals(device.ApiKey, deviceKey, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid device key.");

        device.LastSeenAt = DateTimeOffset.Now;
        await _db.SaveChangesAsync(ct);

        var sensors = device.Sensors.Select(s => new SensorSlotInfoDto(
            s.Id,
            s.SensorCode,
            s.Slot is null ? null : new SlotInfoDto(
                s.Slot.Id,
                s.Slot.Label,
                s.Slot.Zone,
                s.Slot.Status.ToString(),
                s.Slot.OccupiedThresholdCm
            )
        )).ToList();

        return new DeviceConnectResultDto(
            device.Id,
            device.Code,
            sensors.Count,
            sensors
        );
    }

    public async Task<DeviceConnectResultDto> RegisterSensorsAsync(DeviceRegisterDto dto, string deviceKey, CancellationToken ct)
    {
        var device = await _db.Devices
            .Include(d => d.Sensors)
                .ThenInclude(s => s.Slot)
            .FirstOrDefaultAsync(d => d.Code == dto.DeviceCode, ct);

        if (device is null)
            throw new InvalidOperationException("Device not found.");

        if (!string.Equals(device.ApiKey, deviceKey, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid device key.");

        device.LastSeenAt = DateTimeOffset.Now;

        // Add new sensors that don't exist yet
        foreach (var sensorDto in dto.Sensors)
        {
            var existingSensor = device.Sensors.FirstOrDefault(s => s.SensorCode == sensorDto.SensorCode);
            if (existingSensor is null)
            {
                var newSensor = new SmartParking.Domain.Entities.Sensor
                {
                    DeviceId = device.Id,
                    SensorCode = sensorDto.SensorCode,
                    LastSeenAt = DateTimeOffset.Now
                };
                device.Sensors.Add(newSensor);
                _db.Sensors.Add(newSensor);

                // Create or link slot if provided
                if (sensorDto.Slot is not null)
                {
                    var slotStatus = Enum.TryParse<SlotStatus>(sensorDto.Slot.Status, true, out var parsed)
                        ? parsed
                        : SlotStatus.Free;

                    // Check from DB if slot exists with this slot code (Label)
                    var existingSlot = await _db.Slots
                        .FirstOrDefaultAsync(s => s.Label == sensorDto.Slot.Label, ct);

                    if (existingSlot is null)
                    {
                        var newSlot = new SmartParking.Domain.Entities.Slot
                        {
                            Label = sensorDto.Slot.Label,
                            Zone = sensorDto.Slot.Zone,
                            Status = slotStatus,
                            OccupiedThresholdCm = sensorDto.Slot.OccupiedThresholdCm,
                            Sensor = newSensor,
                            LastUpdateAt = DateTimeOffset.Now
                        };
                        newSensor.Slot = newSlot;
                        _db.Slots.Add(newSlot);
                    }
                    else
                    {
                        existingSlot.Label = sensorDto.Slot.Label;
                        existingSlot.Zone = sensorDto.Slot.Zone;
                        existingSlot.Status = slotStatus;
                        existingSlot.OccupiedThresholdCm = sensorDto.Slot.OccupiedThresholdCm;
                        existingSlot.LastUpdateAt = DateTimeOffset.Now;
                        newSensor.Slot = existingSlot;
                    }
                }
            }
            else
            {
                existingSensor.LastSeenAt = DateTimeOffset.Now;

                // Update or create slot if provided
                if (sensorDto.Slot is not null)
                {
                    var slotStatus = Enum.TryParse<SlotStatus>(sensorDto.Slot.Status, true, out var parsed)
                        ? parsed
                        : SlotStatus.Free;

                    // Check from DB if slot exists with this slot code (Label)
                    var existingSlot = existingSensor.Slot 
                        ?? await _db.Slots.FirstOrDefaultAsync(s => s.Label == sensorDto.Slot.Label, ct);

                    if (existingSlot is null)
                    {
                        var newSlot = new SmartParking.Domain.Entities.Slot
                        {
                            Label = sensorDto.Slot.Label,
                            Zone = sensorDto.Slot.Zone,
                            Status = slotStatus,
                            OccupiedThresholdCm = sensorDto.Slot.OccupiedThresholdCm,
                            Sensor = existingSensor,
                            LastUpdateAt = DateTimeOffset.Now
                        };
                        existingSensor.Slot = newSlot;
                        _db.Slots.Add(newSlot);
                    }
                    else
                    {
                        existingSlot.Label = sensorDto.Slot.Label;
                        existingSlot.Zone = sensorDto.Slot.Zone;
                        existingSlot.Status = slotStatus;
                        existingSlot.OccupiedThresholdCm = sensorDto.Slot.OccupiedThresholdCm;
                        existingSlot.LastUpdateAt = DateTimeOffset.Now;
                        existingSensor.Slot = existingSlot;
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        // Reload to get updated data with slots
        device = await _db.Devices
            .Include(d => d.Sensors)
                .ThenInclude(s => s.Slot)
            .FirstOrDefaultAsync(d => d.Code == dto.DeviceCode, ct);

        var sensors = device!.Sensors.Select(s => new SensorSlotInfoDto(
            s.Id,
            s.SensorCode,
            s.Slot is null ? null : new SlotInfoDto(
                s.Slot.Id,
                s.Slot.Label,
                s.Slot.Zone,
                s.Slot.Status.ToString(),
                s.Slot.OccupiedThresholdCm
            )
        )).ToList();

        return new DeviceConnectResultDto(
            device.Id,
            device.Code,
            sensors.Count,
            sensors
        );
    }
}
