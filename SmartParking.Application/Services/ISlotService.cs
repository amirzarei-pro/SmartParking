using SmartParking.Application.Contracts;

namespace SmartParking.Application.Services;

public interface ISlotService
{
    Task<List<SlotDto>> GetAllAsync(CancellationToken ct);
    Task<bool> UpdateThresholdAsync(string slotLabel, double thresholdCm, CancellationToken ct);

}
