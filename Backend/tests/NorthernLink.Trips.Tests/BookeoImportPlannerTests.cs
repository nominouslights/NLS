using NorthernLink.Trips.Application.BookeoImports;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Application.BookeoImports.Planning;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The planner: the per-booking diff against the ledger, the per-departure grouping, and every
/// issue code the contract names — each provoked by the smallest change to the synthetic fixture.
/// </summary>
public class BookeoImportPlannerTests
{
    private static readonly DateOnly Oct5 = new(2026, 10, 5);
    private static readonly DateOnly Oct6 = new(2026, 10, 6);

    private static PlannedGroup GroupOf(BookeoImportPlan plan, string bookingSuffix) =>
        plan.Groups.Single(g => g.BookingNumbers.Any(n => n.EndsWith(bookingSuffix, StringComparison.Ordinal)));

    private static PlannedRow RowOf(BookeoImportPlan plan, string bookingSuffix) =>
        plan.Rows.Single(r => r.BookingNumber.EndsWith(bookingSuffix, StringComparison.Ordinal));

    private static IEnumerable<string> Codes(IEnumerable<ImportIssue> issues) => issues.Select(i => i.Code);

    // ------------------------------------------------------------------ the fixture, first import

    [Fact]
    public async Task The_fixture_plans_three_new_trips_four_new_bookings_and_one_skipped_cancellation()
    {
        var bed = new BookeoTestBed();

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        Assert.Equal(new BookeoImportSummary(
            Rows: 5, New: 4, Changed: 0, Cancelled: 0, Unchanged: 0, Skipped: 1,
            TripsToCreate: 3, TripsToUpdate: 0, TripsToCancel: 0, BlockedGroups: 0,
            Warnings: 2), plan.Summary);
        Assert.Empty(plan.UnmappedProducts);
        Assert.Empty(plan.UnmatchedUnits);
        Assert.All(plan.Groups, g => Assert.Equal(BookeoGroupAction.Create, g.Action));
    }

    [Fact]
    public async Task Bookings_001_and_002_share_one_trip()
    {
        var bed = new BookeoTestBed();

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var group = GroupOf(plan, "001");
        Assert.Same(group, GroupOf(plan, "002"));
        Assert.Equal(["9000000000000001", "9000000000000002"], group.BookingNumbers);
        Assert.Equal(Oct5, group.ServiceDate);
        Assert.Equal(new TimeOnly(8, 0), group.WindowStart);
        Assert.Equal(TripDirection.Outbound, group.Direction);
        Assert.Equal(3, group.PassengersAfter);
        Assert.Equal(BookeoVehicleMatch.Matched, group.VehicleMatch);
        Assert.Equal(BookeoTestBed.VanId, group.AssignVehicle?.VehicleId);
        Assert.Equal(14, group.SeatsCapacity);
    }

    [Fact]
    public async Task Manifest_rows_carry_stops_fares_and_the_bookeo_ref()
    {
        var bed = new BookeoTestBed();

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var rows = GroupOf(plan, "001").PassengersAfterList;
        var alex = rows.Single(p => p.Name == "Alex Sample");
        Assert.Equal("bookeo:9000000000000001", alex.ExternalRef);
        Assert.Equal("Thompson", alex.PickupStopName);
        Assert.Equal(BookeoTestBed.ThompsonStop, alex.PickupStopId);
        Assert.Equal("Lynn Lake", alex.DropoffStopName); // Lynn Lake Residents, resident role Dropoff.
        Assert.Equal(120m, alex.FareAmountCad);
        Assert.Equal(FarePaymentMethod.Online, alex.FarePaymentMethod);

        var casey = rows.Single(p => p.Name == "Casey Demo");
        Assert.Equal("Leaf Rapids", casey.DropoffStopName);
        Assert.Null(casey.FareAmountCad); // Pending payment: recorded as unpaid.
        Assert.Null(casey.FarePaymentMethod);

        // "To Thompson" is Inbound with resident role Pickup: the community is where they board.
        var dana = Assert.Single(GroupOf(plan, "003").PassengersAfterList);
        Assert.Equal("Lynn Lake", dana.PickupStopName);
        Assert.Equal("Thompson", dana.DropoffStopName);

        // An "X to Y" category names both ends.
        var eli = Assert.Single(GroupOf(plan, "004").PassengersAfterList);
        Assert.Equal("Leaf Rapids", eli.PickupStopName);
        Assert.Equal("Lynn Lake", eli.DropoffStopName);
    }

    // ------------------------------------------------------------------ the diff

    [Fact]
    public async Task Rows_diff_into_new_changed_cancelled_unchanged_and_skipped()
    {
        var bed = new BookeoTestBed();
        var fixture = BookeoTestBed.FixtureRows();
        await bed.ImportAsync(fixture);

        var rows = fixture.Select(r => r.BookingNumber![^3..] switch
        {
            "001" => r, // unchanged
            "002" => r with { TotalPaidCad = 100m, TotalDueCad = 0m, Status = "normal" }, // changed
            "003" => r with { Status = "canceled", IsCanceled = true }, // cancelled
            "005" => r, // still canceled, never imported
            _ => r,
        }).Append(BookeoTestBed.FixtureRow("004") with { BookingNumber = "9000000000000006" }) // new
            .ToList();

        var plan = await bed.PlanAsync(rows);

        Assert.Equal(BookeoRowAction.Unchanged, RowOf(plan, "001").Action);
        Assert.Equal(BookeoRowAction.Changed, RowOf(plan, "002").Action);
        Assert.Equal(["status", "totals"], RowOf(plan, "002").ChangedFields);
        Assert.Equal(BookeoRowAction.Cancelled, RowOf(plan, "003").Action);
        Assert.Equal(BookeoRowAction.Unchanged, RowOf(plan, "004").Action);
        Assert.Equal(BookeoRowAction.Skipped, RowOf(plan, "005").Action);
        Assert.Equal(BookeoRowAction.New, RowOf(plan, "006").Action);
    }

    [Fact]
    public async Task A_booking_in_the_ledger_but_missing_from_the_file_is_untouched()
    {
        var bed = new BookeoTestBed();
        await bed.ImportAsync(BookeoTestBed.FixtureRows());
        var trip003 = bed.Repo.Trips.Single(t => t.ServiceDate == Oct6 && t.WindowStart == new TimeOnly(13, 30));
        var ledger003 = bed.Repo.Bookings.Single(b => b.BookingNumber.EndsWith("003", StringComparison.Ordinal));
        var stampBefore = ledger003.LastImportedAtUtc;

        // A date-filtered report that only covers Oct 5.
        var oct5Only = BookeoTestBed.FixtureRows().Where(r => r.ServiceDate == Oct5).ToList();
        bed.Clock.UtcNow = bed.Clock.UtcNow.AddHours(1);
        var plan = await bed.PlanAsync(oct5Only);

        Assert.DoesNotContain(plan.Rows, r => r.BookingNumber.EndsWith("003", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Groups, g => g.Target?.Id == trip003.Id);
        Assert.Equal(0, plan.Summary.TripsToCancel);

        await bed.ImportAsync(oct5Only);
        Assert.Equal(TripStatus.Scheduled, trip003.Status);
        Assert.Single(bed.ManifestOf(trip003).Passengers);
        Assert.Equal(stampBefore, ledger003.LastImportedAtUtc);
        Assert.Equal(trip003.Id, ledger003.TripId);
    }

    [Fact]
    public async Task A_time_change_moves_the_passengers_to_the_new_departure()
    {
        var bed = new BookeoTestBed();
        await bed.ImportAsync(BookeoTestBed.FixtureRows());
        var oldTrip = bed.Repo.Trips.Single(t => t.ServiceDate == Oct6 && t.WindowStart == new TimeOnly(13, 30));

        var moved = BookeoTestBed.FixtureRow("003") with { WindowStart = new TimeOnly(15, 0), WindowEnd = new TimeOnly(19, 30) };
        var plan = await bed.PlanAsync([moved]);

        var row = RowOf(plan, "003");
        Assert.Equal(BookeoRowAction.Changed, row.Action);
        Assert.Contains("time", row.ChangedFields);
        Assert.Equal(BookeoGroupAction.Create, plan.Groups.Single(g => g.Key == row.GroupKey).Action);
        var leaving = plan.Groups.Single(g => g.Key == row.RemovalGroupKey);
        Assert.Equal(oldTrip.Id, leaving.Target!.Id);
        Assert.Equal(BookeoGroupAction.Cancel, leaving.Action); // Import-created, now empty.
    }

    // ------------------------------------------------------------------ Block issues

    [Fact]
    public async Task ProductNotMapped_blocks_its_group_and_lists_the_product_while_other_groups_still_apply()
    {
        var bed = new BookeoTestBed(mapProducts: false);
        bed.Map(BookeoTestBed.FromThompson, "Shuttle from Thompson", null, TripDirection.Outbound, ResidentStopRole.Dropoff);

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        Assert.Contains(BookeoIssueCodes.ProductNotMapped, Codes(RowOf(plan, "003").Issues));
        Assert.Contains(BookeoIssueCodes.ProductNotMapped, Codes(GroupOf(plan, "004").Issues));
        Assert.Equal(BookeoGroupAction.Blocked, GroupOf(plan, "003").Action);
        Assert.Equal(BookeoGroupAction.Create, GroupOf(plan, "001").Action);
        Assert.Equal(2, plan.Summary.BlockedGroups);
        Assert.Equal(
            [BookeoTestBed.LakeToRapids, BookeoTestBed.ToThompson],
            plan.UnmappedProducts.Select(p => p.ProductCode).Order());
        Assert.Equal("Leaf Rapids to Lynn Lake", plan.UnmappedProducts.Single(p => p.ProductCode == BookeoTestBed.LakeToRapids).Destination);
        Assert.True(RowOf(plan, "003").Blocked);
    }

    [Fact]
    public async Task An_unmapped_group_still_counts_the_passengers_the_file_would_bring_in()
    {
        // Regression: with no product mappings every group was Blocked and showed "0 → 0",
        // because an unmapped booking builds no manifest rows (no route, no stops).
        var bed = new BookeoTestBed(mapProducts: false);

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        Assert.Equal(3, plan.Groups.Count);
        Assert.All(plan.Groups, g =>
        {
            Assert.Equal(BookeoGroupAction.Blocked, g.Action);
            Assert.True(g.IsUnmapped);
            Assert.Equal(0, g.PassengersBefore);
            Assert.Equal(g.AddRows.Sum(r => r.Parsed.Participants), g.PassengersAfter);
            Assert.Empty(g.PassengersAfterList); // Nothing a commit could write.
        });

        var fromThompson = GroupOf(plan, "001");
        Assert.Equal(Oct5, fromThompson.ServiceDate);
        Assert.Equal(new TimeOnly(8, 0), fromThompson.WindowStart);
        Assert.Equal(3, fromThompson.PassengersAfter);

        Assert.Equal(0, plan.Summary.TripsToCreate);
        Assert.Equal(3, plan.Summary.BlockedGroups);

        // The wire shape carries the counts, and the booking rows carry their money.
        var preview = plan.ToPreview(Guid.NewGuid(), "bookeo_sample.xls", bed.Clock.GetUtcNow());
        var wire = preview.Groups.Single(g => g.Key == fromThompson.Key);
        Assert.Equal(0, wire.PassengersBefore);
        Assert.Equal(3, wire.PassengersAfter);
        Assert.All(preview.Rows.Where(r => r.GroupKey is not null), r =>
        {
            var parsed = RowOf(plan, r.BookingNumber).Parsed;
            Assert.Equal(parsed.TotalGrossCad, r.TotalGrossCad);
            Assert.Equal(parsed.TotalPaidCad, r.TotalPaidCad);
            Assert.Equal(parsed.TotalDueCad, r.TotalDueCad);
        });
        Assert.Contains(preview.Rows, r => r.GroupKey is not null && r.TotalGrossCad > 0m);
    }

    [Fact]
    public async Task ManifestCapExceeded_is_raised_on_an_unmapped_group_too()
    {
        var bed = new BookeoTestBed(mapProducts: false);
        var big = BookeoTestBed.FixtureRow("001") with
        {
            Participants = 9,
            Categories = [new BookeoCategoryCount("Lynn Lake Residents", 9)],
        };

        var plan = await bed.PlanAsync([big]);

        var group = GroupOf(plan, "001");
        Assert.Equal(9, group.PassengersAfter);
        Assert.Contains(BookeoIssueCodes.ProductNotMapped, Codes(group.Issues));
        Assert.Contains(BookeoIssueCodes.ManifestCapExceeded, Codes(group.Issues));
    }

    [Fact]
    public async Task A_destination_specific_mapping_does_not_cover_the_other_direction()
    {
        var bed = new BookeoTestBed();
        var otherWay = BookeoTestBed.FixtureRow("004") with { Destination = "Lynn Lake to Leaf Rapids" };

        var plan = await bed.PlanAsync([otherWay]);

        Assert.Contains(BookeoIssueCodes.ProductNotMapped, Codes(RowOf(plan, "004").Issues));
    }

    [Fact]
    public async Task RowUnreadable_is_a_per_row_block()
    {
        var bed = new BookeoTestBed();
        var broken = BookeoTestBed.FixtureRow("001") with { Problems = ["The start date/time is not a date."] };

        var plan = await bed.PlanAsync([broken, BookeoTestBed.FixtureRow("003")]);

        var row = RowOf(plan, "001");
        Assert.Contains(BookeoIssueCodes.RowUnreadable, Codes(row.Issues));
        Assert.True(row.Blocked);
        Assert.Null(row.GroupKey);
        Assert.Equal(BookeoGroupAction.Create, GroupOf(plan, "003").Action);
    }

    [Fact]
    public async Task ManifestCapExceeded_blocks_more_than_eight_passengers()
    {
        var bed = new BookeoTestBed();
        var big = BookeoTestBed.FixtureRow("001") with
        {
            Participants = 9,
            Categories = [new BookeoCategoryCount("Lynn Lake Residents", 9)],
        };

        var plan = await bed.PlanAsync([big]);

        Assert.Contains(BookeoIssueCodes.ManifestCapExceeded, Codes(GroupOf(plan, "001").Issues));
        Assert.Equal(BookeoGroupAction.Blocked, GroupOf(plan, "001").Action);
    }

    [Fact]
    public async Task OverVehicleCapacity_blocks_a_group_the_matched_vehicle_cannot_seat()
    {
        var bed = new BookeoTestBed();
        bed.Van.SeatingCapacity = 2;

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        Assert.Contains(BookeoIssueCodes.OverVehicleCapacity, Codes(GroupOf(plan, "001").Issues));
        Assert.Equal(BookeoGroupAction.Blocked, GroupOf(plan, "001").Action);
    }

    [Fact]
    public async Task TripNotEditable_blocks_a_change_to_a_trip_that_has_left()
    {
        var bed = new BookeoTestBed();
        await bed.ImportAsync(BookeoTestBed.FixtureRows());
        var trip = bed.Repo.Trips.Single(t => t.ServiceDate == Oct6 && t.WindowStart == new TimeOnly(13, 30));
        Assert.True(trip.Start().IsSuccess);

        var plan = await bed.PlanAsync([BookeoTestBed.FixtureRow("003") with { CustomerPhone = "2045559999" }]);

        var group = GroupOf(plan, "003");
        Assert.Equal(trip.Id, group.Target!.Id);
        Assert.Contains(BookeoIssueCodes.TripNotEditable, Codes(group.Issues));
        Assert.True(RowOf(plan, "003").Blocked);
    }

    // ------------------------------------------------------------------ Warning issues

    [Fact]
    public async Task VehicleUnmatched_when_no_mapping_and_no_unit_number_matches()
    {
        var bed = new BookeoTestBed(mapUnit: false);

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var group = GroupOf(plan, "001");
        Assert.Equal(BookeoVehicleMatch.Unmatched, group.VehicleMatch);
        Assert.Contains(BookeoIssueCodes.VehicleUnmatched, Codes(group.Issues));
        Assert.Null(group.AssignVehicle);
        Assert.Equal([new BookeoUnmatchedUnit("FORD TRANSIT 150", 1)], plan.UnmatchedUnits);
        Assert.Equal(BookeoVehicleMatch.Blank, GroupOf(plan, "003").VehicleMatch);
    }

    [Fact]
    public async Task A_unit_text_equal_to_a_fleet_unit_number_matches_without_a_mapping()
    {
        var bed = new BookeoTestBed(mapUnit: false);

        var plan = await bed.PlanAsync([BookeoTestBed.FixtureRow("001") with { UnitText = "nl-02" }]);

        Assert.Equal(BookeoVehicleMatch.Matched, GroupOf(plan, "001").VehicleMatch);
        Assert.Equal(BookeoTestBed.VanId, GroupOf(plan, "001").AssignVehicle?.VehicleId);
    }

    [Fact]
    public async Task VehicleAmbiguous_when_the_unit_text_matches_two_vehicles()
    {
        var bed = new BookeoTestBed(mapUnit: false);
        bed.Repo.Vehicles.Add(new VehicleLookup
        {
            VehicleId = Guid.NewGuid(), TenantId = BookeoTestBed.TenantId, UnitNumber = "NL-02 ",
            Status = VehicleLookup.ActiveStatus, RequiredLicenceClass = "Class 4", SeatingCapacity = 14,
        });

        var plan = await bed.PlanAsync([BookeoTestBed.FixtureRow("001") with { UnitText = "NL-02" }]);

        Assert.Equal(BookeoVehicleMatch.Ambiguous, GroupOf(plan, "001").VehicleMatch);
        Assert.Contains(BookeoIssueCodes.VehicleAmbiguous, Codes(GroupOf(plan, "001").Issues));
    }

    [Fact]
    public async Task VehicleNotActive_leaves_the_trip_unassigned()
    {
        var bed = new BookeoTestBed();
        bed.Van.Status = "InMaintenance";

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var group = GroupOf(plan, "001");
        Assert.Contains(BookeoIssueCodes.VehicleNotActive, Codes(group.Issues));
        Assert.Null(group.AssignVehicle);
        Assert.Equal(BookeoGroupAction.Create, group.Action);
    }

    [Fact]
    public async Task VehicleDoubleBooked_when_the_vehicle_is_on_an_overlapping_trip()
    {
        var bed = new BookeoTestBed();
        bed.AddExistingTrip(Oct5, new TimeOnly(10, 0), TripDirection.Inbound, vehicleId: BookeoTestBed.VanId, tripNumber: "TR-0200",
            serviceType: TripServiceType.Charter);

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        Assert.Contains(BookeoIssueCodes.VehicleDoubleBooked, Codes(GroupOf(plan, "001").Issues));
    }

    [Fact]
    public async Task DriverDoubleBooked_when_the_existing_trips_driver_drives_an_overlapping_trip()
    {
        var bed = new BookeoTestBed();
        var target = bed.AddExistingTrip(Oct5, new TimeOnly(8, 0), TripDirection.Outbound, tripNumber: "TR-0300");
        bed.AddExistingTrip(Oct5, new TimeOnly(9, 0), TripDirection.Inbound, tripNumber: "TR-0301", serviceType: TripServiceType.Charter);

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var group = GroupOf(plan, "001");
        Assert.Equal(target.Id, group.Target!.Id);
        Assert.Equal(BookeoGroupAction.Update, group.Action);
        Assert.Equal(BookeoVehicleMatch.KeptExisting, group.VehicleMatch); // Never overrides the dispatcher.
        Assert.Contains(BookeoIssueCodes.DriverDoubleBooked, Codes(group.Issues));
    }

    [Fact]
    public async Task PossibleDuplicateTrip_when_a_scheduled_trip_on_the_route_departs_within_90_minutes()
    {
        var bed = new BookeoTestBed();
        bed.AddExistingTrip(Oct5, new TimeOnly(9, 15), TripDirection.Outbound, tripNumber: "TR-0400");

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var group = GroupOf(plan, "001");
        Assert.Equal(BookeoGroupAction.Create, group.Action);
        Assert.Contains(BookeoIssueCodes.PossibleDuplicateTrip, Codes(group.Issues));
    }

    [Fact]
    public async Task PaymentDue_names_the_amount()
    {
        var bed = new BookeoTestBed();

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var issue = Assert.Single(RowOf(plan, "002").Issues, i => i.Code == BookeoIssueCodes.PaymentDue);
        Assert.Equal(BookeoIssueCodes.Warning, issue.Severity);
        Assert.Contains("100.00", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BookingCancelled_and_TripWillBeCancelled_when_an_imported_booking_is_cancelled()
    {
        var bed = new BookeoTestBed();
        await bed.ImportAsync(BookeoTestBed.FixtureRows());

        var plan = await bed.PlanAsync([BookeoTestBed.FixtureRow("003") with { Status = "canceled", IsCanceled = true }]);

        Assert.Contains(BookeoIssueCodes.BookingCancelled, Codes(RowOf(plan, "003").Issues));
        var group = GroupOf(plan, "003");
        Assert.Equal(BookeoGroupAction.Cancel, group.Action);
        Assert.Contains(BookeoIssueCodes.TripWillBeCancelled, Codes(group.Issues));
    }

    [Fact]
    public async Task PastDeparture_when_the_departure_is_before_now_in_Winnipeg()
    {
        var bed = new BookeoTestBed();
        // 08:00 CDT on Oct 5 is 13:00 UTC; at 13:30 UTC the shuttle has gone.
        bed.Clock.UtcNow = new DateTimeOffset(2026, 10, 5, 13, 30, 0, TimeSpan.Zero);

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        Assert.Contains(BookeoIssueCodes.PastDeparture, Codes(GroupOf(plan, "001").Issues));
        Assert.DoesNotContain(BookeoIssueCodes.PastDeparture, Codes(GroupOf(plan, "003").Issues));
    }

    [Fact]
    public async Task PastDeparture_does_not_change_the_plan_hash()
    {
        var bed = new BookeoTestBed();
        var before = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        bed.Clock.UtcNow = new DateTimeOffset(2026, 10, 5, 13, 30, 0, TimeSpan.Zero);
        var after = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        Assert.Equal(before.Hash, after.Hash);
    }

    [Fact]
    public async Task PassengerDoubleBooked_when_the_same_name_and_phone_ride_overlapping_bookings()
    {
        var bed = new BookeoTestBed();
        var twin = BookeoTestBed.FixtureRow("003") with
        {
            BookingNumber = "9000000000000007",
            ServiceDate = Oct5,
            WindowStart = new TimeOnly(9, 0),
            WindowEnd = new TimeOnly(13, 0),
            Passengers = [new BookeoParsedPassenger("Lynn Lake Residents", 1, "alex  sample", null, "(204) 555-0101")],
        };

        var plan = await bed.PlanAsync([BookeoTestBed.FixtureRow("001"), twin]);

        Assert.Contains(BookeoIssueCodes.PassengerDoubleBooked, Codes(RowOf(plan, "001").Issues));
        Assert.Contains(BookeoIssueCodes.PassengerDoubleBooked, Codes(RowOf(plan, "007").Issues));
    }

    [Fact]
    public async Task PassengerPlaceholder_when_details_name_fewer_passengers_than_booked()
    {
        var bed = new BookeoTestBed();
        var row = BookeoTestBed.FixtureRow("003") with
        {
            Participants = 2,
            Categories = [new BookeoCategoryCount("Lynn Lake Residents", 2)],
        };

        var plan = await bed.PlanAsync([row]);

        Assert.Contains(BookeoIssueCodes.PassengerPlaceholder, Codes(RowOf(plan, "003").Issues));
        Assert.Contains(GroupOf(plan, "003").PassengersAfterList, p => p.Name == "Dana Fake guest 1");
    }

    [Fact]
    public async Task StopUnmatched_keeps_the_community_name_without_a_stop()
    {
        var bed = new BookeoTestBed();
        var row = BookeoTestBed.FixtureRow("003") with
        {
            Passengers = [new BookeoParsedPassenger("Pukatawagan Residents", 1, "Dana Fake", null, null)],
            Categories = [new BookeoCategoryCount("Pukatawagan Residents", 1)],
        };

        var plan = await bed.PlanAsync([row]);

        Assert.Contains(BookeoIssueCodes.StopUnmatched, Codes(RowOf(plan, "003").Issues));
        var passenger = Assert.Single(GroupOf(plan, "003").PassengersAfterList);
        Assert.Equal("Pukatawagan", passenger.PickupStopName);
        Assert.Null(passenger.PickupStopId);
    }

    // ------------------------------------------------------------------ Info issues

    [Fact]
    public async Task BookingChanged_lists_the_changed_fields()
    {
        var bed = new BookeoTestBed();
        await bed.ImportAsync(BookeoTestBed.FixtureRows());

        var plan = await bed.PlanAsync([BookeoTestBed.FixtureRow("004") with { UnitText = "NL-02" }]);

        var issue = Assert.Single(RowOf(plan, "004").Issues, i => i.Code == BookeoIssueCodes.BookingChanged);
        Assert.Equal(BookeoIssueCodes.Info, issue.Severity);
        Assert.Contains("unit", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CanceledNeverImported_is_info_on_a_skipped_row()
    {
        var bed = new BookeoTestBed();

        var plan = await bed.PlanAsync(BookeoTestBed.FixtureRows());

        var row = RowOf(plan, "005");
        Assert.Equal(BookeoRowAction.Skipped, row.Action);
        var issue = Assert.Single(row.Issues);
        Assert.Equal(BookeoIssueCodes.CanceledNeverImported, issue.Code);
        Assert.Equal(BookeoIssueCodes.Info, issue.Severity);
        Assert.Null(row.GroupKey);
    }

    [Fact]
    public void Every_contract_issue_code_is_covered_by_a_test_in_this_class()
    {
        var codes = typeof(BookeoIssueCodes).GetFields()
            .Where(f => f.IsLiteral && f.Name is not ("Block" or "Warning" or "Info"))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "BookeoImportPlannerTests.cs"));

        Assert.Equal(20, codes.Count);
        Assert.All(codes, code => Assert.Contains($"BookeoIssueCodes.{code}", source, StringComparison.Ordinal));
    }
}
