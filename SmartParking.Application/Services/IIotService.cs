using SmartParking.Application.Contracts;

namespace SmartParking.Application.Services;

public interface IIotService
{
    Task<TelemetryIngestResultDto> IngestAsync(TelemetryIngestDto dto, string deviceKey, CancellationToken ct);
    Task<DeviceConnectResultDto> ConnectAsync(string deviceCode, string deviceKey, CancellationToken ct);
    Task<DeviceConnectResultDto> RegisterSensorsAsync(DeviceRegisterDto dto, string deviceKey, CancellationToken ct);
}
