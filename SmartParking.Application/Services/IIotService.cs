using SmartParking.Application.Contracts;

namespace SmartParking.Application.Services;

public interface IIotService
{
    Task<TelemetryIngestResultDto> IngestAsync(TelemetryIngestDto dto, string deviceKey, CancellationToken ct);
}
