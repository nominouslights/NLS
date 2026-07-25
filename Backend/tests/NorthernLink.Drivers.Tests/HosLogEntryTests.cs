using NorthernLink.Drivers.Application.Abstractions;
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
                11m, 9m, 10m, "D. Wells", null),
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
                11m, 9m, 10m, "D. Wells", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var added = Assert.Single(repository.Added);
        Assert.Equal(result.Value, added.Id);
        Assert.Equal(HosLogEntrySource.ManualPaperBackup, added.Source);
        Assert.True(repository.Saved);
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
