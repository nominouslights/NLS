using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Persistence.Auditing;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// The DutyDay ledger against a real Postgres as the app role: the owned jsonb event array
/// round-trips (including an amendment that voids an element in place), the one-record-per-day
/// index holds, the outbox gets the duty-day-changed event, the projection fills
/// <c>rm_duty_days</c> and the rm_drivers rollup prefers the ledger, and Row-Level Security
/// keeps one tenant's days out of another's.
/// </summary>
[Collection("postgres")]
public class DutyDayPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateOnly D = new(2026, 9, 14);
    private static readonly DateTimeOffset Now = At("15:00");

    /// <summary>A local CDT wall-clock time on D as an instant.</summary>
    private static DateTimeOffset At(string hhmm) =>
        HosLocalTime.ToUtc(D, TimeOnly.ParseExact(hhmm, "HH:mm"));

    private static DutyEventDraft Draft(string hhmm, DutyStatus status, decimal? startKm = null, decimal? endKm = null, bool attest = false) =>
        new(Guid.NewGuid(), At(hhmm), status, DutyEventSource.DriverApp, Guid.NewGuid(),
            OdometerStartKm: startKm, OdometerEndKm: endKm,
            PcNoPassengersOrCargo: attest, PcNoTrailer: attest, PcNoWork: attest);

    [Fact]
    public async Task Events_round_trip_through_jsonb_and_an_amendment_voids_in_place()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var day = DutyDay.Open(PostgresFixture.TenantA, driver.Id, D, Now);
        var original = Draft("06:30", DutyStatus.Driving);
        Assert.True(day.Append(Draft("06:00", DutyStatus.OnDuty), Now).IsSuccess);
        Assert.True(day.Append(original, Now).IsSuccess);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            writer.Drivers.Add(driver);
            writer.DutyDays.Add(day);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            var tracked = await writer.DutyDays.SingleAsync(d => d.Id == day.Id);
            Assert.Equal(2, tracked.Events.Count);
            Assert.Equal(DutyStatus.Driving, tracked.Events[1].Status);
            Assert.Equal(At("06:30"), tracked.Events[1].AtUtc);

            var amended = tracked.Amend(original.EventId, Draft("06:45", DutyStatus.Driving), "ELD shows 06:45", Now);
            Assert.True(amended.IsSuccess, amended.IsFailure ? amended.Error.Message : null);
            await writer.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var reloaded = await reader.DutyDays.AsNoTracking().SingleAsync(d => d.Id == day.Id);
        Assert.Equal(3, reloaded.Events.Count);
        Assert.True(reloaded.Events.Single(e => e.EventId == original.EventId).IsVoided);
        var replacement = reloaded.Events.Single(e => e.AmendsEventId == original.EventId);
        Assert.Equal("ELD shows 06:45", replacement.AmendmentReason);
        Assert.Equal([DutyStatus.OnDuty, DutyStatus.Driving], reloaded.EffectiveTimeline().Select(e => e.Status));
        Assert.Equal(3, reloaded.LastSequence);

        // The amendment changed only the jsonb array and LastSequence — and that scalar is what
        // makes the second save an aggregate write the audit pipeline sees: version 2, a second
        // journal row, a second upsert-shaped outbox event. Same "now" on both saves on purpose,
        // so UpdatedAtUtc cannot be what made the row Modified.
        Assert.Equal(2, reloaded.Version);
        var outbox = await reader.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.RoutingKey == "drivers.duty-day-changed" && m.TenantId == PostgresFixture.TenantA)
            .ToListAsync();
        var mine = outbox
            .Select(m => JsonDocument.Parse(m.Payload).RootElement)
            .Where(e => e.GetProperty("dutyDayId").GetGuid() == day.Id)
            .ToList();

        // Two events appended in the first save (one outbox row each, both at version 1), then
        // the amendment at version 2 — every row carrying the whole effective timeline.
        Assert.Equal([1, 1, 2], mine.Select(e => e.GetProperty("version").GetInt32()).Order());
        var latest = mine.Single(e => e.GetProperty("version").GetInt32() == 2);
        Assert.Equal(
            [HosDisplay.OnDuty, HosDisplay.Driving],
            latest.GetProperty("effectiveEvents").EnumerateArray().Select(e => e.GetProperty("status").GetString()));
        Assert.Equal(2, latest.GetProperty("effectiveEvents").GetArrayLength());
    }

    [Fact]
    public async Task Personal_conveyance_odometers_survive_the_round_trip_and_the_system_switch_is_stored()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var day = DutyDay.Open(PostgresFixture.TenantA, driver.Id, D, Now);
        Assert.True(day.Append(Draft("06:00", DutyStatus.OffDuty), Now).IsSuccess);
        Assert.True(day.Append(Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000.5m, attest: true), Now).IsSuccess);
        Assert.True(day.Append(Draft("12:00", DutyStatus.OffDuty, endKm: 1100.5m), Now).IsSuccess);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.DutyDays.Add(day);
            await writer.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var reloaded = await reader.DutyDays.AsNoTracking().SingleAsync(d => d.Id == day.Id);
        Assert.Equal(75.0m, reloaded.PersonalConveyanceKm);
        var system = Assert.Single(reloaded.Events, e => e.Source == DutyEventSource.System);
        Assert.Equal(At("11:30"), system.AtUtc);
        Assert.Equal(1075.5m, system.OdometerEndKm);
        Assert.Equal(1000.5m, reloaded.Events[1].OdometerStartKm);
        Assert.True(reloaded.Events[1].PcNoWork);
    }

    [Fact]
    public async Task One_record_per_driver_per_day_is_enforced_by_the_database()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var first = DutyDay.Open(PostgresFixture.TenantA, driver.Id, D, Now);
        Assert.True(first.Append(Draft("06:00", DutyStatus.OnDuty), Now).IsSuccess);
        var second = DutyDay.Open(PostgresFixture.TenantA, driver.Id, D, Now);
        Assert.True(second.Append(Draft("07:00", DutyStatus.OnDuty), Now).IsSuccess);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.DutyDays.Add(first);
            await writer.SaveChangesAsync();
        }

        await using var duplicate = fixture.CreateContext(PostgresFixture.TenantA);
        duplicate.DutyDays.Add(second);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    }

    [Fact]
    public async Task The_projection_fills_rm_duty_days_and_the_roster_rollup_prefers_the_ledger()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var legacy = HosLogEntry.RecordManualEntry(
            PostgresFixture.TenantA, driver.Id, D.AddDays(-30), DutyStatus.OffDuty, 0m, 0m, 24m, "D. Wells", null).Value;
        var day = DutyDay.Open(PostgresFixture.TenantA, driver.Id, D, Now);
        Assert.True(day.Append(Draft("06:00", DutyStatus.OnDuty), Now).IsSuccess);
        Assert.True(day.Append(Draft("06:30", DutyStatus.Driving), Now).IsSuccess);
        Assert.True(day.Append(Draft("12:00", DutyStatus.OnDuty), Now).IsSuccess);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.HosLogEntries.Add(legacy);
            writer.DutyDays.Add(day);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var row = await reader.DutyDayReadModels.SingleAsync(r => r.Id == day.Id);
        Assert.Equal(driver.Id, row.DriverId);
        Assert.Equal(D, row.LocalDate);
        Assert.Equal(5 * 60 + 30, row.DrivingMinutes);
        Assert.Equal(HosDisplay.OnDuty, row.LastStatus);
        Assert.Equal(3, row.EventCount);
        Assert.False(row.Certified);
        Assert.Equal(day.Version, row.Version);

        // Postgres normalises jsonb text, so read it back as JSON rather than as a substring.
        using var events = JsonDocument.Parse(row.EventsJson);
        Assert.Equal(
            [HosDisplay.OnDuty, HosDisplay.Driving, HosDisplay.OnDuty],
            events.RootElement.EnumerateArray().Select(e => e.GetProperty("status").GetString()));

        // The legacy entry is newer by RecordedAtUtc but older by date, and the ledger wins
        // regardless: a driver with any DutyDay is rolled up from it.
        var driverRow = await reader.DriverReadModels.SingleAsync(r => r.Id == driver.Id);
        Assert.Equal(HosDisplay.OnDuty, driverRow.LatestDutyStatus);
        Assert.Equal(D, driverRow.LatestHosDate);
        Assert.Equal(5.5m, driverRow.LatestDrivingHours);
    }

    [Fact]
    public async Task A_tenant_cannot_see_another_tenants_duty_days()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var day = DutyDay.Open(PostgresFixture.TenantA, driver.Id, D, Now);
        Assert.True(day.Append(Draft("06:00", DutyStatus.OnDuty), Now).IsSuccess);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.DutyDays.Add(day);
            await writer.SaveChangesAsync();
        }

        await using var other = fixture.CreateContext(PostgresFixture.TenantB);

        // Both halves: the EF filter and, with it bypassed, the Postgres policy.
        Assert.False(await other.DutyDays.AnyAsync(d => d.Id == day.Id));
        Assert.False(await other.DutyDays.IgnoreQueryFilters().AnyAsync(d => d.Id == day.Id));
    }
}
