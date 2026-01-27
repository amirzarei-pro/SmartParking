using SmartParking.Domain.Enums;

namespace SmartParking.Domain.Entities;

public class Slot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Label { get; set; } = default!;  // A1, A2
    public string Zone { get; set; } = "A";

    public Guid? SensorId { get; set; }
    public Sensor? Sensor { get; set; }

    public SlotStatus Status { get; set; } = SlotStatus.Offline;
    public double OccupiedThresholdCm { get; set; } = 15.0;

    public double LastDistanceCm { get; set; }
    public DateTimeOffset LastUpdateAt { get; set; } = DateTimeOffset.MinValue;
}
