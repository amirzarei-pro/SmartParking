using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Contracts;
using SmartParking.Application.Services;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public sealed class DeviceService : IDeviceService
{
    private readonly SmartParkingDbContext _db;

    public DeviceService(SmartParkingDbContext db) => _db = db;

    public async Task<List<DeviceDto>> GetAllAsync(CancellationToken ct)
    {
        var deviceList = await _db.Devices
            .Include(d => d.Sensors)
            .ThenInclude(s => s.Slot)
            .OrderBy(d => d.Code)
            .ToListAsync(ct);

        return deviceList.Select(d => new DeviceDto(
            d.Id,
            d.Code,
            d.ApiKey,
            d.LastSeenAt,
            d.Sensors.Select(s => new SensorInfoDto(
                s.Id,
                s.SensorCode,
                s.Slot?.Label
            )).ToList()
        )).ToList();
    }

    public async Task<DeviceDto?> GetByIdAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await _db.Devices
            .Include(d => d.Sensors)
            .ThenInclude(s => s.Slot)
            .FirstOrDefaultAsync(d => d.Id == deviceId, ct);

        if (device is null)
            return null;

        return MapToDto(device);
    }

    public async Task<DeviceDto?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var device = await _db.Devices
            .Include(d => d.Sensors)
            .ThenInclude(s => s.Slot)
            .FirstOrDefaultAsync(d => d.Code == code, ct);

        if (device is null)
            return null;

        return MapToDto(device);
    }

    public async Task<bool> CreateAsync(string code, string apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var exists = await _db.Devices.AnyAsync(d => d.Code == code, ct);
        if (exists)
            return false;

        var device = new Device
        {
            Code = code,
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? Guid.NewGuid().ToString("N") : apiKey,
            LastSeenAt = DateTimeOffset.Now
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateAsync(Guid deviceId, string code, string apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null)
            return false;

        // Check if code already exists on another device
        var codeExists = await _db.Devices.AnyAsync(d => d.Code == code && d.Id != deviceId, ct);
        if (codeExists)
            return false;

        device.Code = code;
        if (!string.IsNullOrWhiteSpace(apiKey))
            device.ApiKey = apiKey;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null)
            return false;

        _db.Devices.Remove(device);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateLastSeenAsync(Guid deviceId, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        if (device is null)
            return false;

        device.LastSeenAt = DateTimeOffset.Now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static DeviceDto MapToDto(Device device) => new(
        device.Id,
        device.Code,
        device.ApiKey,
        device.LastSeenAt,
        device.Sensors.Select(s => new SensorInfoDto(
            s.Id,
            s.SensorCode,
            s.Slot?.Label
        )).ToList()
    );
}
