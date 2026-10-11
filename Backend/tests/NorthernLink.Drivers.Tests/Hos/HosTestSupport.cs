using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// Fixed time and fakes for the ledger tests. D = 2026-09-14 (Monday, CDT, UTC−5): the same
/// anchor the engine's fixture catalogue uses, so a local "HH:mm" here means what it means
/// there. "Now" is 15:00 local that day unless a test says otherwise.
/// </summary>
public static class HosTestSupport
{
    public static readonly DateOnly D = new(2026, 9, 14);
    public static readonly Guid DriverId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly DateTimeOffset Now = At("15:00");

    /// <summary>The instant of a local wall-clock time on D (or D + <paramref name="dayOffset"/>).</summary>
    public static DateTimeOffset At(string hhmm, int dayOffset = 0) =>
        HosLocalTime.ToUtc(D.AddDays(dayOffset), TimeOnly.ParseExact(hhmm, "HH:mm"));

    public static DutyEventDraft Draft(
        string hhmm,
        DutyStatus status,
        DutyEventSource source = DutyEventSource.DriverApp,
        Guid? eventId = null,
        string? enteredBy = null,
        decimal? startKm = null,
        decimal? endKm = null,
        bool attest = false,
        int dayOffset = 0,
        string? note = null,
        string? carrierName = null) =>
        new(
            eventId ?? Guid.NewGuid(),
            At(hhmm, dayOffset),
            status,
            source,
            UserId,
            enteredBy,
            carrierName,
            note,
            startKm,
            endKm,
            attest,
            attest,
            attest);

    public static DutyDay NewDay(DateOnly? date = null) =>
        DutyDay.Open(TestDrivers.TenantId, DriverId, date ?? D, Now);

    /// <summary>A day with the given events already appended (each must succeed) and its events cleared.</summary>
    public static DutyDay DayWith(params DutyEventDraft[] drafts)
    {
        var day = NewDay();
        foreach (var draft in drafts)
        {
            var result = day.Append(draft, Now);
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"Fixture append failed: {result.Error.Code} — {result.Error.Message}");
            }
        }

        day.ClearDomainEvents();
        return day;
    }

    public sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    public sealed class FakeActor(Guid? userId, params string[] roles) : ICurrentActor
    {
        public Guid? UserId => userId;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => roles;
    }

    public sealed class FakeDutyDayRepository(bool driverExists = true) : IDutyDayRepository
    {
        public List<DutyDay> Days { get; } = [];

        public int Saves { get; private set; }

        public Task<DutyDay?> GetAsync(Guid driverId, DateOnly localDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(Days.FirstOrDefault(d => d.DriverId == driverId && d.LocalDate == localDate));

        public Task<IReadOnlyList<DutyDay>> LoadRangeAsync(Guid driverId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DutyDay>>(
                Days.Where(d => d.DriverId == driverId && d.LocalDate >= from && d.LocalDate <= to).OrderBy(d => d.LocalDate).ToList());

        public Task<IReadOnlyList<DutyDay>> LoadRangeForAllDriversAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DutyDay>>(
                Days.Where(d => d.LocalDate >= from && d.LocalDate <= to).OrderBy(d => d.LocalDate).ToList());

        public Task<DateOnly?> EarliestLocalDateAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Days.Where(d => d.DriverId == driverId).Select(d => (DateOnly?)d.LocalDate).Min());

        public Task<IReadOnlyDictionary<Guid, DateOnly>> EarliestLocalDatesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(
                Days.GroupBy(d => d.DriverId).ToDictionary(g => g.Key, g => g.Min(d => d.LocalDate)));

        public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(driverExists);

        public void Add(DutyDay day) => Days.Add(day);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;

            // Mirror ModuleDbContext: a save drains the raised events, so "no new domain event
            // after a replay" is observable against this fake too.
            foreach (var day in Days)
            {
                day.ClearDomainEvents();
            }

            return Task.CompletedTask;
        }
    }

    public sealed class FakeDriverReadService(params DriverResponse[] drivers) : IDriverReadService
    {
        public Task<IReadOnlyList<DriverResponse>> GetDriversAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DriverResponse>>(drivers);

        public Task<DriverResponse?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(drivers.FirstOrDefault(d => d.Id == driverId));

        public Task<DriverResponse?> GetDriverByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(drivers.FirstOrDefault(d => d.UserId == userId));
    }

    public static DriverResponse Roster(Guid id, string name = "J. Spence") => new(
        id, null, name, null, "Class 2", null, "Northern Link", false, "Active",
        0, null, null, null, null, Now, Now);
}
