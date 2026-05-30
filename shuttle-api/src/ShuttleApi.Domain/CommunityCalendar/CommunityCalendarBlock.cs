using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.CommunityCalendar;

public sealed class CommunityCalendarBlock : Entity<Guid>
{
    public DateOnly BlockedDate { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime BlockedAt { get; private set; }

    private CommunityCalendarBlock() { }

    public static CommunityCalendarBlock Create(DateOnly date, string reason)
    {
        return new CommunityCalendarBlock
        {
            Id = Guid.NewGuid(),
            BlockedDate = date,
            Reason = reason,
            BlockedAt = DateTime.UtcNow
        };
    }
}
