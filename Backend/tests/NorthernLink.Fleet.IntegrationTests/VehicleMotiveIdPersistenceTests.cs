using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The Motive vehicle id against a real Postgres as the app role: the per-tenant partial unique
/// index's 23505 is translated by <see cref="VehicleRepository.TrySaveMotiveVehicleIdAsync"/>
/// (the lost check-then-save race becomes a domain conflict, not a 500) while any OTHER unique
/// violation still throws, the existence query, and disposal freeing the id for a replacement.
/// </summary>
[Collection("postgres")]
public class VehicleMotiveIdPersistenceTests(PostgresFixture fixture)
{
    private static string NewMotiveId() => $"mv-{Guid.NewGuid():N}";

    private async Task<(Vehicle Linked, Vehicle Other, string MotiveId)> SeedLinkedPairAsync()
    {
        var motiveId = NewMotiveId();
        var linked = TestVehicleFactory.Create(PostgresFixture.TenantA);
        var other = TestVehicleFactory.Create(PostgresFixture.TenantA);
        Assert.True(linked.SetMotiveVehicleId(motiveId).IsSuccess);

        await using var writer = fixture.CreateContext(PostgresFixture.TenantA);
        writer.Vehicles.AddRange(linked, other);
        await writer.SaveChangesAsync();
        return (linked, other, motiveId);
    }

    [Fact]
    public async Task Losing_the_race_to_the_unique_index_is_reported_not_thrown()
    {
        var (_, other, motiveId) = await SeedLinkedPairAsync();

        // Skip the handler's existence check on purpose — exactly what a concurrent request that
        // passed its check before the winner committed looks like at save time.
        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        var repository = new VehicleRepository(context);
        var loser = await repository.GetByIdAsync(other.Id);
        Assert.True(loser!.SetMotiveVehicleId(motiveId).IsSuccess);

        Assert.False(await repository.TrySaveMotiveVehicleIdAsync());

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var stored = await reader.Vehicles.AsNoTracking().SingleAsync(v => v.Id == other.Id);
        Assert.Null(stored.MotiveVehicleId);
    }

    [Fact]
    public async Task Another_unique_violation_is_not_mislabelled_as_a_motive_conflict()
    {
        var (linked, other, _) = await SeedLinkedPairAsync();

        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        var repository = new VehicleRepository(context);
        var clash = await repository.GetByIdAsync(other.Id);

        // Collide on the unit-number index instead.
        Assert.True(clash!.UpdateDetails(
            linked.UnitNumber, clash.Vin, clash.Make, clash.Model, clash.Year, clash.SeatingCapacity,
            clash.LicencePlate, clash.RequiredLicenceClass, clash.AcquisitionCostCad, clash.EndOfLifeKm).IsSuccess);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => repository.TrySaveMotiveVehicleIdAsync());
        var postgres = Assert.IsType<PostgresException>(thrown.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.NotEqual(VehicleRepository.MotiveVehicleIdIndexName, postgres.ConstraintName);
    }

    [Fact]
    public async Task The_existence_query_excludes_the_vehicle_itself_and_other_tenants()
    {
        var (linked, other, motiveId) = await SeedLinkedPairAsync();

        await using var context = fixture.CreateContext(PostgresFixture.TenantA);
        var repository = new VehicleRepository(context);
        Assert.True(await repository.ExistsByMotiveVehicleIdAsync(motiveId, other.Id));
        Assert.False(await repository.ExistsByMotiveVehicleIdAsync(motiveId, linked.Id));

        await using var tenantB = fixture.CreateContext(PostgresFixture.TenantB);
        Assert.False(await new VehicleRepository(tenantB).ExistsByMotiveVehicleIdAsync(motiveId));
    }

    [Fact]
    public async Task Disposing_frees_the_id_for_the_replacement_bus()
    {
        var motiveId = NewMotiveId();
        var old = TestVehicleFactory.Create(PostgresFixture.TenantA);
        var replacement = TestVehicleFactory.Create(PostgresFixture.TenantA);
        Assert.True(old.SetMotiveVehicleId(motiveId).IsSuccess);
        Assert.True(old.ChangeStatus(VehicleStatus.Retired).IsSuccess);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Vehicles.AddRange(old, replacement);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var tracked = await writer.Vehicles.SingleAsync(v => v.Id == old.Id);
            Assert.True(tracked.Dispose(DisposalMethod.Sold, 8_000m).IsSuccess);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var repository = new VehicleRepository(writer);
            var tracked = await repository.GetByIdAsync(replacement.Id);
            Assert.False(await repository.ExistsByMotiveVehicleIdAsync(motiveId, replacement.Id));
            Assert.True(tracked!.SetMotiveVehicleId(motiveId).IsSuccess);
            Assert.True(await repository.TrySaveMotiveVehicleIdAsync());
        }

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        Assert.Null((await reader.Vehicles.AsNoTracking().SingleAsync(v => v.Id == old.Id)).MotiveVehicleId);
        Assert.Equal(motiveId, (await reader.Vehicles.AsNoTracking().SingleAsync(v => v.Id == replacement.Id)).MotiveVehicleId);
    }
}
