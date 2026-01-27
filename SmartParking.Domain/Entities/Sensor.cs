namespace SmartParking.Domain.Entities;

public class Sensor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = default!;

    public string SensorCode { get; set; } = default!; // S1, S2
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.MinValue;

    public Slot? Slot { get; set; }
}
