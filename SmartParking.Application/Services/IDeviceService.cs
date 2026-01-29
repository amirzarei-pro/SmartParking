using SmartParking.Application.Contracts;

namespace SmartParking.Application.Services;

public interface IDeviceService
{
    Task<List<DeviceDto>> GetAllAsync(CancellationToken ct);
    Task<DeviceDto?> GetByIdAsync(Guid deviceId, CancellationToken ct);
    Task<DeviceDto?> GetByCodeAsync(string code, CancellationToken ct);
    Task<bool> CreateAsync(string code, string apiKey, CancellationToken ct);
    Task<bool> UpdateAsync(Guid deviceId, string code, string apiKey, CancellationToken ct);
    Task<bool> DeleteAsync(Guid deviceId, CancellationToken ct);
    Task<bool> UpdateLastSeenAsync(Guid deviceId, CancellationToken ct);
}
