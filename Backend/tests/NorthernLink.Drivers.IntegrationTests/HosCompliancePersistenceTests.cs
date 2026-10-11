using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Persistence.Auditing;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// The compliance aggregates against a real Postgres as the app role: each of the four new
/// tables round-trips (interval columns, string enums), the outbox gets its integration event,
/// the projections fill the four rm_* tables, the driver's attestation columns round-trip and
/// ride on driver-changed, and Row-Level Security keeps one tenant's records out of another's
/// on every new table — the EF filter and, with it bypassed, the Postgres policy.
/// </summary>
[Collection("postgres")]
public class HosCompliancePersistenceTests(PostgresFixture fixture)
{
    private static readonly DateOnly D = new(2026, 9, 14);
    private static readonly DateTimeOffset Now = At("15:00");
    private static readonly Guid OfficerId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static DateTimeOffset At(string hhmm, int dayOffset = 0) =>
        HosLocalTime.ToUtc(D.AddDays(dayOffset), TimeOnly.ParseExact(hhmm, "HH:mm"));

    [Fact]
    public async Task Every_compliance_aggregate_round_trips_and_publishes_its_event()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var exception = HosException.Declare(
            PostgresFixture.TenantA, driver.Id, HosExceptionKind.AdverseDrivingConditions, Guid.NewGuid(), Now,
            At("10:00"), null, "Whiteout on PR 391", "km 40", true, TimeSpan.FromMinutes(90), true, Guid.NewGuid()).Value;
        var outOfService = DriverOutOfService.Record(
            PostgresFixture.TenantA, driver.Id, At("14:00"), OfficerId, Now, null, false, "Inspection", "MPI 44-1021").Value;
        var malfunction = EldMalfunctionReport.Report(
            PostgresFixture.TenantA, driver.Id, driver.Name, null, "P", At("08:00"), Guid.NewGuid(), Now, "Light on").Value;
        var finding = ComplianceFinding.Open(
            PostgresFixture.TenantA, driver.Id, D, "Falsification", FindingSeverity.Severe, "Two logbooks", FindingSource.Enforcement, OfficerId, Now).Value;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            writer.Drivers.Add(driver);
            writer.HosExceptions.Add(exception);
            writer.DriverOutOfServiceRecords.Add(outOfService);
            writer.EldMalfunctionReports.Add(malfunction);
            writer.ComplianceFindings.Add(finding);
            await writer.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var loadedException = await reader.HosExceptions.AsNoTracking().SingleAsync(e => e.Id == exception.Id);
        Assert.Equal(TimeSpan.FromMinutes(90), loadedException.ExtraDriving);
        Assert.Equal(HosExceptionStatus.Declared, loadedException.Status);
        Assert.Equal(At("10:00"), loadedException.EffectiveFromUtc);
        Assert.True(loadedException.IsApplying);

        var loadedOos = await reader.DriverOutOfServiceRecords.AsNoTracking().SingleAsync(r => r.Id == outOfService.Id);
        Assert.Equal(At("14:00").AddHours(72), loadedOos.UntilUtc);
        Assert.Equal(OutOfServiceStatus.Active, loadedOos.Status);

        var loadedMalfunction = await reader.EldMalfunctionReports.AsNoTracking().SingleAsync(m => m.Id == malfunction.Id);
        Assert.Equal("P", loadedMalfunction.Code);
        Assert.Equal(driver.Name, loadedMalfunction.DriverName);

        var loadedFinding = await reader.ComplianceFindings.AsNoTracking().SingleAsync(f => f.Id == finding.Id);
        Assert.Equal(FindingSeverity.Severe, loadedFinding.Severity);
        Assert.Equal(FindingSource.Enforcement, loadedFinding.Source);

        var routingKeys = await reader.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == PostgresFixture.TenantA)
            .Select(m => new { m.RoutingKey, m.Payload })
            .ToListAsync();
        Assert.Contains(routingKeys, m => m.RoutingKey == "drivers.hos-exception-changed"
            && JsonDocument.Parse(m.Payload).RootElement.GetProperty("exceptionId").GetGuid() == exception.Id);
        Assert.Contains(routingKeys, m => m.RoutingKey == "drivers.driver-out-of-service-changed"
            && JsonDocument.Parse(m.Payload).RootElement.GetProperty("recordId").GetGuid() == outOfService.Id);
        Assert.Contains(routingKeys, m => m.RoutingKey == "drivers.eld-malfunction-changed"
            && JsonDocument.Parse(m.Payload).RootElement.GetProperty("reportId").GetGuid() == malfunction.Id);

        // Findings are internal: no outbox row names this one.
        Assert.DoesNotContain(routingKeys, m => m.Payload.Contains(finding.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_projections_fill_the_four_read_tables()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var exception = HosException.ApproveDeferral(
            PostgresFixture.TenantA, driver.Id, OfficerId, Now, Guid.NewGuid(), At("06:00", 1), TimeSpan.FromHours(1), "Late return", null, false).Value;
        var outOfService = DriverOutOfService.Record(
            PostgresFixture.TenantA, driver.Id, At("14:00"), OfficerId, Now, null, true, "Inspection", null).Value;
        var malfunction = EldMalfunctionReport.Report(
            PostgresFixture.TenantA, driver.Id, driver.Name, null, "E", At("08:00"), Guid.NewGuid(), Now, "Engine sync").Value;
        var finding = ComplianceFinding.Open(
            PostgresFixture.TenantA, driver.Id, D, "DailyDriving13h", FindingSeverity.Major, "Over", FindingSource.Audit, OfficerId, Now).Value;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.HosExceptions.Add(exception);
            writer.DriverOutOfServiceRecords.Add(outOfService);
            writer.EldMalfunctionReports.Add(malfunction);
            writer.ComplianceFindings.Add(finding);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var exceptionRow = await reader.HosExceptionReadModels.SingleAsync(r => r.Id == exception.Id);
        Assert.Equal(HosDisplay.OffDutyDeferralKind, exceptionRow.Kind);
        Assert.Equal("Approved", exceptionRow.Status);
        Assert.True(exceptionRow.Applying);
        Assert.Equal(60, exceptionRow.DeferredOffDutyMinutes);

        var oosRow = await reader.DriverOutOfServiceReadModels.SingleAsync(r => r.Id == outOfService.Id);
        Assert.Equal("Active", oosRow.Status);
        Assert.Null(oosRow.EffectiveUntilUtc);

        var malfunctionRow = await reader.EldMalfunctionReportReadModels.SingleAsync(r => r.Id == malfunction.Id);
        Assert.Equal("Open", malfunctionRow.Status);
        Assert.Equal("E", malfunctionRow.Code);

        var findingRow = await reader.ComplianceFindingReadModels.SingleAsync(r => r.Id == finding.Id);
        Assert.Equal("DailyDriving13h", findingRow.Rule);
        Assert.Equal("Major", findingRow.Severity);
        Assert.Equal("Open", findingRow.Status);
    }

    [Fact]
    public async Task The_attestation_round_trips_on_the_driver_rides_on_driver_changed_and_projects_to_rm_drivers()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            writer.Drivers.Add(driver);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            var tracked = await writer.Drivers.SingleAsync(d => d.Id == driver.Id);
            Assert.True(tracked.AttestPriorRecords(D.AddDays(-1), OfficerId, "Keewatin logs", true, Now).IsSuccess);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var reloaded = await reader.Drivers.AsNoTracking().SingleAsync(d => d.Id == driver.Id);
        Assert.Equal(D.AddDays(-1), reloaded.PriorRecordsCoveredThroughDate);
        Assert.Equal(OfficerId, reloaded.PriorRecordsAttestedByUserId);
        Assert.True(reloaded.WorksForOtherCarriers);

        var row = await reader.DriverReadModels.SingleAsync(r => r.Id == driver.Id);
        Assert.Equal(D.AddDays(-1), row.PriorRecordsCoveredThroughDate);
        Assert.Equal("Keewatin logs", row.PriorRecordsNote);
        Assert.True(row.WorksForOtherCarriers);

        var latest = (await reader.Set<OutboxMessage>()
                .IgnoreQueryFilters()
                .Where(m => m.RoutingKey == "drivers.driver-changed" && m.TenantId == PostgresFixture.TenantA)
                .ToListAsync())
            .Select(m => JsonDocument.Parse(m.Payload).RootElement)
            .Where(e => e.GetProperty("driverId").GetGuid() == driver.Id)
            .OrderByDescending(e => e.GetProperty("occurredAtUtc").GetDateTimeOffset())
            .First();
        Assert.Equal("2026-09-13", latest.GetProperty("priorRecordsCoveredThroughDate").GetString());
        Assert.True(latest.GetProperty("worksForOtherCarriers").GetBoolean());
    }

    [Fact]
    public async Task A_tenant_cannot_see_another_tenants_compliance_records()
    {
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var exception = HosException.Declare(
            PostgresFixture.TenantA, driver.Id, HosExceptionKind.Emergency, Guid.NewGuid(), Now,
            At("10:00"), At("11:00"), "Medical", null, false, null, false, null).Value;
        var outOfService = DriverOutOfService.Record(
            PostgresFixture.TenantA, driver.Id, At("14:00"), OfficerId, Now, null, false, "Inspection", null).Value;
        var malfunction = EldMalfunctionReport.Report(
            PostgresFixture.TenantA, driver.Id, driver.Name, null, "T", At("08:00"), Guid.NewGuid(), Now, "Timing").Value;
        var finding = ComplianceFinding.Open(
            PostgresFixture.TenantA, driver.Id, D, "TwoRecords", FindingSeverity.Minor, "Paper and ELD", FindingSource.Audit, OfficerId, Now).Value;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.HosExceptions.Add(exception);
            writer.DriverOutOfServiceRecords.Add(outOfService);
            writer.EldMalfunctionReports.Add(malfunction);
            writer.ComplianceFindings.Add(finding);
            await writer.SaveChangesAsync();
        }

        await using var other = fixture.CreateContext(PostgresFixture.TenantB);

        // Both halves on every table: the EF filter and, with it bypassed, the Postgres policy.
        Assert.False(await other.HosExceptions.AnyAsync(e => e.Id == exception.Id));
        Assert.False(await other.HosExceptions.IgnoreQueryFilters().AnyAsync(e => e.Id == exception.Id));
        Assert.False(await other.DriverOutOfServiceRecords.AnyAsync(r => r.Id == outOfService.Id));
        Assert.False(await other.DriverOutOfServiceRecords.IgnoreQueryFilters().AnyAsync(r => r.Id == outOfService.Id));
        Assert.False(await other.EldMalfunctionReports.AnyAsync(m => m.Id == malfunction.Id));
        Assert.False(await other.EldMalfunctionReports.IgnoreQueryFilters().AnyAsync(m => m.Id == malfunction.Id));
        Assert.False(await other.ComplianceFindings.AnyAsync(f => f.Id == finding.Id));
        Assert.False(await other.ComplianceFindings.IgnoreQueryFilters().AnyAsync(f => f.Id == finding.Id));

        await using var own = fixture.CreateContext(PostgresFixture.TenantA);
        Assert.True(await own.HosExceptions.AnyAsync(e => e.Id == exception.Id));
        Assert.True(await own.ComplianceFindings.AnyAsync(f => f.Id == finding.Id));
    }

    [Fact]
    public async Task A_tenant_cannot_see_another_tenants_projected_compliance_rows()
    {
        var worker = fixture.BuildDriversProjectionWorker();
        var driver = TestDriverFactory.CreateDriver(PostgresFixture.TenantA);
        var finding = ComplianceFinding.Open(
            PostgresFixture.TenantA, driver.Id, D, "Tampering", FindingSeverity.Severe, "Seal broken", FindingSource.Enforcement, OfficerId, Now).Value;
        var malfunction = EldMalfunctionReport.Report(
            PostgresFixture.TenantA, driver.Id, driver.Name, null, "S", At("08:00"), Guid.NewGuid(), Now, "Data transfer").Value;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Drivers.Add(driver);
            writer.ComplianceFindings.Add(finding);
            writer.EldMalfunctionReports.Add(malfunction);
            await writer.SaveChangesAsync();
        }

        await worker.ProcessOnceAsync(CancellationToken.None);

        await using var other = fixture.CreateContext(PostgresFixture.TenantB);
        Assert.False(await other.ComplianceFindingReadModels.IgnoreQueryFilters().AnyAsync(r => r.Id == finding.Id));
        Assert.False(await other.EldMalfunctionReportReadModels.IgnoreQueryFilters().AnyAsync(r => r.Id == malfunction.Id));

        await using var own = fixture.CreateContext(PostgresFixture.TenantA);
        Assert.True(await own.ComplianceFindingReadModels.AnyAsync(r => r.Id == finding.Id));
    }
}
