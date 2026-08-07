using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Trips.Domain.Manifests;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// Proves the one unproven persistence mechanic in the fare feature: an enum inside an
/// <c>OwnsMany(...).ToJson(...)</c> collection serializes as an integer by default, and the
/// explicit <c>HasConversion&lt;string&gt;()</c> is what keeps <c>FarePaymentMethod</c> readable
/// in the jsonb. This round-trips a manifest through a real Postgres and then reads the raw
/// column to pin the on-disk shape, not just the EF materialization.
/// </summary>
[Collection("postgres")]
public class TripManifestFareJsonbTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Fare_payment_method_round_trips_as_a_string_in_the_passengers_jsonb()
    {
        var manifest = TripManifest.Create(
            PostgresFixture.TenantA,
            tripDate: new DateOnly(2026, 8, 7),
            tripNumber: "TR-FARE-0001",
            route: "Black Sturgeon Falls → Thompson",
            direction: TripDirection.Outbound,
            client: null,
            passengers:
            [
                new ManifestPassenger
                {
                    Name = "M. Beardy",
                    BoardedOn = true,
                    BoardedOff = true,
                    FareAmountCad = 45.50m,
                    FarePaymentMethod = FarePaymentMethod.Cash,
                    FarePaidAtUtc = new DateTimeOffset(2026, 8, 7, 16, 0, 0, TimeSpan.Zero),
                },
                new ManifestPassenger
                {
                    Name = "S. Flett",
                    BoardedOn = true,
                    BoardedOff = true,
                    FareAmountCad = 0m,
                    FarePaymentMethod = FarePaymentMethod.Waived,
                },
            ],
            allSeatbeltsVerified: true,
            cargo: [],
            allCargoSecured: CargoSecuredStatus.NotApplicable,
            source: ManifestSource.App,
            enteredBy: null);
        Assert.True(manifest.IsSuccess, manifest.IsFailure ? manifest.Error.Code : "");

        Guid manifestId;
        await using (var context = fixture.CreateTripsContext(PostgresFixture.TenantA))
        {
            var existing = await context.Manifests
                .SingleOrDefaultAsync(m => m.TripNumber == "TR-FARE-0001");
            if (existing is null)
            {
                context.Manifests.Add(manifest.Value);
                await context.SaveChangesAsync();
                manifestId = manifest.Value.Id;
            }
            else
            {
                manifestId = existing.Id;
            }
        }

        // EF materialization round-trips.
        await using (var context = fixture.CreateTripsContext(PostgresFixture.TenantA))
        {
            var loaded = await context.Manifests.AsNoTracking().SingleAsync(m => m.Id == manifestId);
            var cash = Assert.Single(loaded.Passengers, p => p.Name == "M. Beardy");
            Assert.Equal(FarePaymentMethod.Cash, cash.FarePaymentMethod);
            Assert.Equal(45.50m, cash.FareAmountCad);
            Assert.Equal(45.50m, loaded.FaresCollectedCad);
            Assert.Equal(1, loaded.FaresPaidCount);
            Assert.Equal(1, loaded.FaresWaivedCount);
        }

        // And the raw jsonb carries the NAME, not an opaque ordinal.
        await using var connection = new NpgsqlConnection(fixture.AppConnectionString);
        await connection.OpenAsync();
        await using var setTenant = new NpgsqlCommand(
            "SELECT set_config('app.tenant_id', @tenant, false)", connection);
        setTenant.Parameters.AddWithValue("tenant", PostgresFixture.TenantA.ToString());
        await setTenant.ExecuteScalarAsync();

        await using var raw = new NpgsqlCommand(
            "SELECT passengers::text FROM trips.trip_manifests WHERE id = @id", connection);
        raw.Parameters.AddWithValue("id", manifestId);
        var json = (string?)await raw.ExecuteScalarAsync();

        Assert.NotNull(json);
        Assert.Contains("\"Cash\"", json);
        Assert.Contains("\"Waived\"", json);
        Assert.DoesNotContain("\"FarePaymentMethod\":0", json.Replace(" ", ""));
    }
}
