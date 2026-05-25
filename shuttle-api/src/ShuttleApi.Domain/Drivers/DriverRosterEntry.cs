using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Drivers;

public sealed class DriverRosterEntry : Entity<Guid>
{
    public Guid DriverId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public RosterStatus Status { get; private set; }
    public TimeOnly? ShiftStart { get; private set; }
    public TimeOnly? ShiftEnd { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private DriverRosterEntry() { }

    public static DriverRosterEntry Create(
        Guid driverId,
        DateOnly entryDate,
        RosterStatus status,
        TimeOnly? shiftStart,
        TimeOnly? shiftEnd)
    {
        return new DriverRosterEntry
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            EntryDate = entryDate,
            Status = status,
            ShiftStart = shiftStart,
            ShiftEnd = shiftEnd,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(RosterStatus status, TimeOnly? shiftStart, TimeOnly? shiftEnd)
    {
        Status = status;
        ShiftStart = shiftStart;
        ShiftEnd = shiftEnd;
        UpdatedAt = DateTime.UtcNow;
    }
}
