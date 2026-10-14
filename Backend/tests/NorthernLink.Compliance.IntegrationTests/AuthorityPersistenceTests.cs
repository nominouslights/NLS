using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Compliance.Application.Authority;
using NorthernLink.Compliance.Application.Insurance;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Application.Vehicles;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.Messaging;
using Xunit;

namespace NorthernLink.Compliance.IntegrationTests;

/// <summary>
/// Epic 2 tasks 1–4 against real Postgres with RLS enforced: what the in-memory unit tests cannot
/// prove — the migration applies, the jsonb value objects round-trip, the one-Current partial
/// unique index survives a renewal's UPDATE + INSERT in one SaveChanges, tenants cannot see each
/// other, and the polling consumer drives both the record registry (status path, Compliance's own
/// outbox) and the replicas and stamp-back (fan-out path).
/// </summary>
[Collection("postgres")]
public class AuthorityPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task An_SFC_registers_itself_as_R20_gets_its_number_stamped_back_and_a_new_one_supersedes_it()
    {
        await using var module = fixture.BuildModule();
        var tenant = Guid.NewGuid();

        await Send(module, tenant, new RecordSfcCommand(tenant, "MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1), null));
        await PostgresFixture.PollOnceAsync(module); // status path: compliance.compliance-record-produced → registry
        await PostgresFixture.PollOnceAsync(module); // fan-out: compliance.compliance-record-registered → stamp

        var first = await Authority(module, tenant);
        Assert.NotNull(first.CurrentSfc!.DocumentNumber);
        Assert.NotNull(first.CurrentSfc.ComplianceRecordId);

        await Send(module, tenant, new RecordSfcCommand(tenant, "MB-SFC-002", new DateOnly(2027, 9, 20), new DateOnly(2028, 9, 20), null));
        await PostgresFixture.PollOnceAsync(module);
        await PostgresFixture.PollOnceAsync(module);

        var second = await Authority(module, tenant);
        Assert.NotNull(second.CurrentSfc!.DocumentNumber);
        Assert.Equal(first.CurrentSfc.DocumentNumber, Assert.Single(second.SfcHistory).DocumentNumber);

        await using var context = fixture.CreateContext(tenant);
        var records = await context.ComplianceRecords.Where(r => r.SourceModule == ComplianceSourceIds.Module).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(ComplianceRecordType.R20, r.RecordType));
        var predecessor = Assert.Single(records, r => r.Id == first.CurrentSfc.ComplianceRecordId);
        var successor = Assert.Single(records, r => r.Id == second.CurrentSfc.ComplianceRecordId);
        Assert.Equal(ComplianceRecordStatus.Superseded, predecessor.Status);
        Assert.Equal(ComplianceRecordStatus.Active, successor.Status);
        Assert.Equal(predecessor.Id, successor.SupersedesId);
    }

    [Fact]
    public async Task A_renewal_saves_under_the_one_current_index_and_the_index_refuses_a_second_current_row()
    {
        await using var module = fixture.BuildModule();
        var tenant = Guid.NewGuid();
        var vehicleId = await SeedVehicleAsync(module, tenant, seating: 15);

        var firstId = await Send(module, tenant, new RecordInsurancePolicyCommand(tenant, vehicleId, Policy("MPI-1", 1_000_000m), null));
        var renewedId = await Send(module, tenant, new RenewInsurancePolicyCommand(tenant, firstId, Policy("MPI-2", 2_000_000m), null));

        await using (var context = fixture.CreateContext(tenant))
        {
            var policies = await context.InsurancePolicies.OrderBy(p => p.CreatedAtUtc).ToListAsync();
            Assert.Equal(2, policies.Count);
            Assert.Equal(AuthorityDocumentStatus.Superseded, policies.Single(p => p.Id == firstId).Status);
            Assert.True(policies.Single(p => p.Id == firstId).IsBelowMinimum); // saved, not refused
            Assert.Equal(AuthorityDocumentStatus.Current, policies.Single(p => p.Id == renewedId).Status);
        }

        // The backstop under the handler's own check: a second Current row for the vehicle.
        await using (var context = fixture.CreateContext(tenant))
        {
            var rogue = InsurancePolicy.Record(tenant, vehicleId, "101", Policy("MPI-3", 2_000_000m), new InsuranceTerms("MPI", 2_000_000m), null, DateTimeOffset.UtcNow).Value;
            context.InsurancePolicies.Add(rogue);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Another_tenant_sees_none_of_the_authority_policies_or_vehicles()
    {
        await using var module = fixture.BuildModule();
        var tenant = Guid.NewGuid();
        var vehicleId = await SeedVehicleAsync(module, tenant, seating: 15);
        await Send(module, tenant, new RecordNscNumberCommand(tenant, "MB 123 456", null));
        await Send(module, tenant, new RecordInsurancePolicyCommand(tenant, vehicleId, Policy("MPI-1", 2_000_000m), null));

        // RLS alone (no query filter): a context with no tenant session at all sees nothing.
        await using (var anonymous = fixture.CreateContext(tenantId: null))
        {
            Assert.Empty(await anonymous.CarrierAuthorities.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await anonymous.InsurancePolicies.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await anonymous.VehicleLookups.IgnoreQueryFilters().ToListAsync());
        }

        // RLS alone, other tenant.
        await using (var other = fixture.CreateContext(PostgresFixture.TenantB))
        {
            Assert.DoesNotContain(await other.CarrierAuthorities.IgnoreQueryFilters().ToListAsync(), a => a.TenantId == tenant);
            Assert.DoesNotContain(await other.InsurancePolicies.IgnoreQueryFilters().ToListAsync(), p => p.TenantId == tenant);
            Assert.DoesNotContain(await other.VehicleLookups.IgnoreQueryFilters().ToListAsync(), v => v.TenantId == tenant);
        }

        var own = await Authority(module, tenant);
        Assert.Equal("MB 123 456", own.NscNumber);
    }

    [Fact]
    public async Task The_vehicle_replica_reads_by_its_own_cursor_leaves_the_producer_row_pending_and_keeps_the_override()
    {
        await using var module = fixture.BuildModule();
        var tenant = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var created = new VehicleChangedIntegrationEvent(vehicleId, tenant, "101", "Active", "2", 9, "Fleet");

        await fixture.SeedProducerOutboxAsync("fleet", tenant, created);
        await PostgresFixture.PollOnceAsync(module);
        await PostgresFixture.PollOnceAsync(module); // a second poll must not replay past the cursor

        // Another module's status-tracked consumer of the key (Trips) still sees the row.
        var status = await fixture.SystemScalarAsync<string>(
            "SELECT processing_status FROM fleet.outbox_messages WHERE id = @id", ("id", created.EventId));
        Assert.Equal("Pending", status);

        var cursor = await fixture.SystemScalarAsync<long>(
            "SELECT last_position FROM compliance.projection_checkpoints WHERE projection_name = 'inbox:fleet.vehicle-changed'");
        Assert.True(cursor > 0);

        // Nine seats is not a bus — until a compliance officer says it is.
        await Send(module, tenant, new SetVehicleRegulatedCommand(tenant, vehicleId, true, null));
        await fixture.SeedProducerOutboxAsync("fleet", tenant, new VehicleChangedIntegrationEvent(vehicleId, tenant, "102", "Active", "2", 8, "Fleet"));
        await PostgresFixture.PollOnceAsync(module);

        await using var context = fixture.CreateContext(tenant);
        var row = await context.VehicleLookups.SingleAsync(v => v.VehicleId == vehicleId);
        Assert.Equal("102", row.UnitNumber);
        Assert.Equal(8, row.SeatingCapacity);
        Assert.True(row.RegulatedOverride);
        Assert.True(row.IsRegulated(minSeatingCapacity: 11));
    }

    private static InsurancePolicyDetails Policy(string number, decimal limit) => new()
    {
        PolicyNumber = number,
        LiabilityLimitCad = limit,
        EffectiveOn = new DateOnly(2026, 10, 1),
        ExpiresOn = new DateOnly(2027, 10, 1),
    };

    /// <summary>A Fleet vehicle-changed event delivered through the fan-out path, as in production.</summary>
    private async Task<Guid> SeedVehicleAsync(ServiceProvider module, Guid tenant, int seating)
    {
        var vehicleId = Guid.NewGuid();
        await fixture.SeedProducerOutboxAsync("fleet", tenant, new VehicleChangedIntegrationEvent(vehicleId, tenant, "101", "Active", "2", seating, "Fleet"));
        await PostgresFixture.PollOnceAsync(module);
        return vehicleId;
    }

    private static Task<CarrierAuthorityResponse> Authority(ServiceProvider module, Guid tenant) =>
        PostgresFixture.AsTenantAsync(module, tenant, async services =>
            (await services.GetRequiredService<ISender>().Query(new GetCarrierAuthorityQuery(tenant, new DateOnly(2026, 10, 14)))).Value);

    private static async Task Send(ServiceProvider module, Guid tenant, ICommand command)
    {
        var result = await PostgresFixture.AsTenantAsync(module, tenant, services => services.GetRequiredService<ISender>().Send(command));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
    }

    private static async Task<T> Send<T>(ServiceProvider module, Guid tenant, ICommand<T> command)
    {
        var result = await PostgresFixture.AsTenantAsync(module, tenant, services => services.GetRequiredService<ISender>().Send(command));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : "");
        return result.Value;
    }
}
