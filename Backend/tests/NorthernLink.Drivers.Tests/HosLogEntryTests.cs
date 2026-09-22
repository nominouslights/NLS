using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.Record;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Drivers.Tests;

public class HosLogEntryTests
{
    private static readonly Guid DriverId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Manual_entry_is_recorded_as_paper_backup_and_raises_one_event()
    {
        var result = HosLogEntry.RecordManualEntry(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
            onDutyHours: 11m, drivingHours: 9.5m, offDutyHours: 10m, enteredBy: "  D. Wells  ", note: "  App was down  ");

        Assert.True(result.IsSuccess);
        var entry = result.Value;
        Assert.Equal(TestDrivers.TenantId, entry.TenantId);
        Assert.Equal(DriverId, entry.DriverId);
        Assert.Equal(new DateOnly(2026, 7, 17), entry.Date);
        Assert.Equal(DutyStatus.Driving, entry.Duty);
        Assert.Equal(11m, entry.OnDutyHours);
        Assert.Equal(9.5m, entry.DrivingHours);
        Assert.Equal(10m, entry.OffDutyHours);
        Assert.Equal(HosLogEntrySource.ManualPaperBackup, entry.Source);
        Assert.Equal("D. Wells", entry.EnteredBy);
        Assert.Equal("App was down", entry.Note);

        var domainEvent = Assert.IsType<HosDutyEntryRecordedDomainEvent>(Assert.Single(entry.DomainEvents));
        Assert.Equal(entry.Id, domainEvent.EntryId);
        Assert.Equal(DriverId, domainEvent.DriverId);
        Assert.Equal(TestDrivers.TenantId, domainEvent.TenantId);
    }

    [Fact]
    public void Driver_app_entry_sets_source_and_leaves_entered_by_null()
    {
        var result = HosLogEntry.RecordFromDriverApp(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.OnDuty,
            onDutyHours: 8m, drivingHours: 6m, offDutyHours: 10m, note: null);

        Assert.True(result.IsSuccess);
        var entry = result.Value;
        Assert.Equal(HosLogEntrySource.DriverApp, entry.Source);
        Assert.Null(entry.EnteredBy);
        Assert.Single(entry.DomainEvents);
    }

    [Fact]
    public void An_over_limit_driving_day_is_still_loggable()
    {
        // CVDHS over-limit (driving > 13h) must NOT be rejected — logging violations is the point.
        var result = HosLogEntry.RecordManualEntry(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
            onDutyHours: 15m, drivingHours: 14m, offDutyHours: 6m, enteredBy: "D. Wells", note: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Manual_entry_without_entered_by_is_rejected()
    {
        var result = HosLogEntry.RecordManualEntry(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
            onDutyHours: 11m, drivingHours: 9m, offDutyHours: 10m, enteredBy: "  ", note: null);

        Assert.True(result.IsFailure);
        Assert.Equal(HosErrors.EnteredByRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData(25, 5, 5)]
    [InlineData(5, -1, 5)]
    [InlineData(5, 5, 24.5)]
    public void Hours_outside_zero_to_twenty_four_are_rejected(double onDuty, double driving, double offDuty)
    {
        var result = HosLogEntry.RecordManualEntry(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.OnDuty,
            (decimal)onDuty, (decimal)driving, (decimal)offDuty, enteredBy: "D. Wells", note: null);

        Assert.True(result.IsFailure);
        Assert.Equal(HosErrors.HoursOutOfRange, result.Error);
    }

    [Fact]
    public async Task Recording_an_entry_for_an_unknown_driver_fails_with_driver_not_found()
    {
        var handler = new RecordHosEntryCommandHandler(new FakeHosLogRepository(driverExists: false));

        var result = await handler.Handle(
            new RecordHosEntryCommand(
                TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
                11m, 9m, 10m, HosLogEntrySource.ManualPaperBackup, "D. Wells", null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Recording_an_entry_for_an_existing_driver_persists_it()
    {
        var repository = new FakeHosLogRepository(driverExists: true);
        var handler = new RecordHosEntryCommandHandler(repository);

        var result = await handler.Handle(
            new RecordHosEntryCommand(
                TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
                11m, 9m, 10m, HosLogEntrySource.ManualPaperBackup, "D. Wells", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = Assert.Single(repository.Added);
        Assert.Equal(result.Value, added.Id);
        Assert.Equal(HosLogEntrySource.ManualPaperBackup, added.Source);
        Assert.True(repository.Saved);
    }

    // ---- Source on the wire (the Driver Field App's submission path) ----

    /// <summary>
    /// The behaviour the Dispatch Console depends on. It sends no source field, and has done
    /// since before the field existed; if the default ever flips, every dispatcher's paper-backup
    /// entry silently starts claiming to be a driver's own submission — a corrupted compliance
    /// record that nothing else would flag.
    /// </summary>
    [Fact]
    public void An_absent_source_still_means_manual_paper_backup()
    {
        var result = HosDisplay.SourceFromWire(null);

        Assert.True(result.IsSuccess);
        Assert.Equal(HosLogEntrySource.ManualPaperBackup, result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_source_is_treated_as_absent(string value)
    {
        var result = HosDisplay.SourceFromWire(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(HosLogEntrySource.ManualPaperBackup, result.Value);
    }

    [Fact]
    public void The_driver_app_source_string_round_trips()
    {
        // Symmetry with SourceToWire is the point: the strings the API emits are the strings it
        // accepts, so the console's chip labels and the Field App's payload are one vocabulary.
        var result = HosDisplay.SourceFromWire(HosDisplay.SourceToWire(HosLogEntrySource.DriverApp));

        Assert.True(result.IsSuccess);
        Assert.Equal(HosLogEntrySource.DriverApp, result.Value);
    }

    [Fact]
    public void The_manual_source_string_round_trips()
    {
        var result = HosDisplay.SourceFromWire(
            HosDisplay.SourceToWire(HosLogEntrySource.ManualPaperBackup));

        Assert.True(result.IsSuccess);
        Assert.Equal(HosLogEntrySource.ManualPaperBackup, result.Value);
    }

    /// <summary>
    /// An unknown string must be a 400, never a 500 — an offline Field App replaying a queue
    /// needs a parkable rejection, and an enum parse that throws would be an exception on the
    /// request path instead.
    /// </summary>
    [Theory]
    [InlineData("DriverApp")]
    [InlineData("driver app")]
    [InlineData("Telematics")]
    public void An_unrecognized_source_is_a_validation_error_not_an_exception(string value)
    {
        var result = HosDisplay.SourceFromWire(value);

        Assert.True(result.IsFailure);
        Assert.Equal(HosErrors.InvalidSource, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    // ---- EnteredBy branches on source ----

    [Fact]
    public void A_driver_app_entry_is_valid_with_no_entered_by()
    {
        var result = HosLogEntry.Record(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
            onDutyHours: 11m, drivingHours: 9m, offDutyHours: 10m,
            HosLogEntrySource.DriverApp, enteredBy: null, note: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EnteredBy);
    }

    /// <summary>
    /// The decision, pinned: a driver-app submission's author is the authenticated driver, so a
    /// supplied dispatcher name is dropped rather than stored or rejected. Storing it would put a
    /// forgeable "who" on a compliance record; rejecting it would wedge an offline replay queue.
    /// The payoff is that EnteredBy means exactly one thing — a human dispatcher typed this.
    /// </summary>
    [Fact]
    public void A_driver_app_entry_discards_any_entered_by_the_client_sent()
    {
        var result = HosLogEntry.Record(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
            onDutyHours: 11m, drivingHours: 9m, offDutyHours: 10m,
            HosLogEntrySource.DriverApp, enteredBy: "J. Spence", note: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EnteredBy);
        Assert.Equal(HosLogEntrySource.DriverApp, result.Value.Source);
    }

    [Fact]
    public void A_manual_entry_with_no_entered_by_is_still_rejected()
    {
        var result = HosLogEntry.Record(
            TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
            onDutyHours: 11m, drivingHours: 9m, offDutyHours: 10m,
            HosLogEntrySource.ManualPaperBackup, enteredBy: null, note: null);

        Assert.True(result.IsFailure);
        Assert.Equal(HosErrors.EnteredByRequired, result.Error);
    }

    [Fact]
    public async Task The_commands_source_reaches_the_stored_entry()
    {
        var repository = new FakeHosLogRepository(driverExists: true);
        var handler = new RecordHosEntryCommandHandler(repository);

        var result = await handler.Handle(
            new RecordHosEntryCommand(
                TestDrivers.TenantId, DriverId, new DateOnly(2026, 7, 17), DutyStatus.Driving,
                11m, 9m, 10m, HosLogEntrySource.DriverApp, EnteredBy: null, Note: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = Assert.Single(repository.Added);
        Assert.Equal(HosLogEntrySource.DriverApp, added.Source);
        Assert.Null(added.EnteredBy);
    }

    private sealed class FakeHosLogRepository(bool driverExists) : IHosLogRepository
    {
        public List<HosLogEntry> Added { get; } = [];

        public bool Saved { get; private set; }

        public Task<HosLogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Added.FirstOrDefault(e => e.Id == id));

        public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(driverExists);

        public void Add(HosLogEntry entry) => Added.Add(entry);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }
}
