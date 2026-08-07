using NorthernLink.Trips.Domain.Manifests;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Per-passenger fare capture on community-run manifests. Fares are optional (a contract run
/// has none), but a half-recorded one is refused: an amount with no method can't be
/// reconciled, and a method with no amount reads as unpaid.
/// </summary>
public class TripManifestFareTests
{
    private static ManifestPassenger Passenger(
        decimal? amount = null,
        FarePaymentMethod? method = null,
        DateTimeOffset? paidAt = null) => new()
    {
        Name = "M. Beardy",
        BoardedOn = true,
        BoardedOff = true,
        FareAmountCad = amount,
        FarePaymentMethod = method,
        FarePaidAtUtc = paidAt,
    };

    [Fact]
    public void A_manifest_with_no_fare_data_is_still_valid()
    {
        var result = TestManifests.Create(passengers: [Passenger()]);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.FaresCollectedCad);
        Assert.Equal(0, result.Value.FaresPaidCount);
        Assert.Equal(0, result.Value.FaresWaivedCount);
    }

    [Theory]
    [InlineData(-1.00)]
    [InlineData(45.999)] // more than cents — jsonb enforces no scale, so the domain must
    public void Invalid_amounts_are_refused(decimal amount)
    {
        var result = TestManifests.Create(
            passengers: [Passenger(amount, FarePaymentMethod.Cash)]);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.InvalidFareAmount, result.Error);
    }

    [Fact]
    public void An_amount_without_a_method_is_refused()
    {
        var result = TestManifests.Create(passengers: [Passenger(amount: 45m)]);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.FarePaymentMethodRequired, result.Error);
    }

    [Fact]
    public void A_paid_timestamp_without_a_method_is_refused()
    {
        var result = TestManifests.Create(
            passengers: [Passenger(paidAt: DateTimeOffset.UtcNow)]);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.FarePaymentMethodRequired, result.Error);
    }

    [Theory]
    [InlineData(FarePaymentMethod.Cash)]
    [InlineData(FarePaymentMethod.Online)]
    public void Cash_and_online_need_a_positive_amount(FarePaymentMethod method)
    {
        Assert.Equal(
            TripManifestErrors.FareAmountRequired,
            TestManifests.Create(passengers: [Passenger(method: method)]).Error);
        Assert.Equal(
            TripManifestErrors.FareAmountRequired,
            TestManifests.Create(passengers: [Passenger(0m, method)]).Error);
    }

    [Fact]
    public void A_waived_fare_cannot_carry_an_amount()
    {
        var result = TestManifests.Create(
            passengers: [Passenger(10m, FarePaymentMethod.Waived)]);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.WaivedFareMustBeZero, result.Error);
    }

    [Fact]
    public void The_rollup_sums_cash_and_online_and_excludes_waived_seats()
    {
        var manifest = TestManifests.Create(passengers:
        [
            Passenger(45.50m, FarePaymentMethod.Cash, DateTimeOffset.UtcNow),
            Passenger(45.50m, FarePaymentMethod.Online, DateTimeOffset.UtcNow),
            Passenger(0m, FarePaymentMethod.Waived),
            Passenger(), // contract-style row with no fare at all
        ]).Value;

        Assert.Equal(91.00m, manifest.FaresCollectedCad);
        Assert.Equal(2, manifest.FaresPaidCount);
        Assert.Equal(1, manifest.FaresWaivedCount);
    }
}
