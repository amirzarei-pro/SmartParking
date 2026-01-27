using SmartParking.Application.Contracts;

namespace SmartParking.Application.Services;

public interface ITelemetryLogService
{
    Task<List<TelemetryLogDto>> GetRecentAsync(int take, string? slotLabel, string? deviceCode, CancellationToken ct);
}
