using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Domain.Vehicles;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The matview's SQL-computed depreciation columns must equal the <see cref="Vehicle"/>
/// aggregate's C# properties exactly — including the banker's-rounding (half-to-even)
/// midpoint cases that force <c>fleet.round_even</c> (plain Postgres <c>round()</c> rounds
/// half-away-from-zero and would diverge). This is the test that guarantees no public
/// contract value changed when the read side moved onto the matview.
/// </summary>
[Collection("postgres")]
public class VehicleDepreciationParityTests(PostgresFixture fixture)
{
    public static TheoryData<int, int, decimal, int> Cases => new()
    {
        // odometerKm, endOfLifeKm, acquisitionCostCad, seatingCapacity
        { 0, 500_000, 100_000m, 24 },        // zero km — full value; seating 24 => periodic
        { 250_000, 500_000, 100_000m, 10 },  // half life; seating 10 => no periodic
        { 500_000, 500_000, 80_000m, 11 },   // at end of life — value 0, remaining 0; seating 11 => periodic
        { 600_000, 500_000, 80_000m, 24 },   // past end of life — value 0, life capped at 100
        { 1_225, 10_000, 100_000m, 24 },     // life_used_pct = 12.25 -> even -> 12.2 (round_even distinguishes)
        { 5_000, 10_000, 200.25m, 24 },      // current_value = 100.125 -> even -> 100.12 (round_even distinguishes)
        { 5_000, 10_000, 200.27m, 24 },      // current_value = 100.135 -> odd floor -> up -> 100.14
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matview_derived_columns_equal_the_aggregate_properties(
        int odometerKm, int endOfLifeKm, decimal acquisitionCostCad, int seatingCapacity)
    {
        var vehicle = TestVehicleFactory.Create(
            PostgresFixture.TenantA, odometerKm, endOfLifeKm, acquisitionCostCad, seatingCapacity);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.Vehicles.Add(vehicle);
            await writer.SaveChangesAsync();
        }

        await fixture.RefreshFleetMatviewsAsync("mv_vehicles");

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var projected = await reader.VehicleReadModels.SingleAsync(v => v.Id == vehicle.Id);

        Assert.Equal(vehicle.CurrentValueCad, projected.CurrentValueCad);
        Assert.Equal(vehicle.RemainingKm, projected.RemainingKm);
        Assert.Equal(vehicle.LifeUsedPct, projected.LifeUsedPct);
        Assert.Equal(vehicle.RequiresPeriodicInspection, projected.RequiresPeriodicInspection);
    }
}
