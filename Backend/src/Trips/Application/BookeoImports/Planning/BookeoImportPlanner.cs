using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.BookeoImports.Planning;

/// <summary>
/// The Bookeo import's one decision-maker: parsed rows + the current database state in, a plan
/// out — the same plan for the preview and, recomputed, for the commit. Pure (no I/O); the clock
/// arrives in <see cref="BookeoPlanInput.Now"/>.
/// <para>
/// <b>Per booking</b> (diffed against the ledger): not in the ledger → New, or Skipped when it is
/// already canceled; in the ledger and canceled now → Cancelled (its manifest rows come off);
/// content hash differs → Changed; same hash → Unchanged. A ledger booking missing from the file is
/// untouched — Bookeo reports are date-filtered, and absence is not a cancellation.
/// </para>
/// <para>
/// <b>Per group</b> — one trip per (route, direction, service date, departure): the target is the
/// trip the group's unmoved bookings are already on, else an existing Scheduled Community trip at
/// exactly that departure, else a new one. A Block on a group stops that whole group, and every
/// booking that touches a blocked group (where it is going, or where it is leaving) is held back
/// from every other group too, so nothing is half-applied; blocked bookings never reach the ledger,
/// so they come back on the next upload.
/// </para>
/// </summary>
public static partial class BookeoImportPlanner
{
    /// <summary>A trip with no end time is treated as running this long, for overlap checks.</summary>
    private static readonly TimeSpan OpenEndedDuration = TimeSpan.FromHours(4);

    /// <summary>How close another trip on the route must be to count as a possible duplicate.</summary>
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(90);

    public static BookeoImportPlan Plan(BookeoPlanInput input)
    {
        var rows = input.Rows.Select(row => ClassifyRow(row, input)).ToList();
        var groups = new Dictionary<string, PlannedGroup>(StringComparer.Ordinal);
        var tripsById = input.Trips.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());

        BuildAddGroups(rows, groups, input);
        ResolveTargets(groups, tripsById, input);
        BuildRemovals(rows, groups, tripsById, input);
        BuildManifestRows(rows, groups);

        ResolveBlocking(groups, input);
        RaiseWarnings(groups, rows, input);
        RaisePassengerDoubleBookings(rows);

        foreach (var row in rows)
        {
            row.Blocked = row.Issues.Any(i => i.Severity == BookeoIssueCodes.Block)
                || Touches(row).Any(key => groups.TryGetValue(key, out var g) && g.IsBlocked);
        }

        var ordered = groups.Values
            .OrderBy(g => g.ServiceDate)
            .ThenBy(g => g.WindowStart)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var summary = new BookeoImportSummary(
            Rows: rows.Count,
            New: rows.Count(r => r.Action == BookeoRowAction.New),
            Changed: rows.Count(r => r.Action == BookeoRowAction.Changed),
            Cancelled: rows.Count(r => r.Action == BookeoRowAction.Cancelled),
            Unchanged: rows.Count(r => r.Action == BookeoRowAction.Unchanged),
            Skipped: rows.Count(r => r.Action == BookeoRowAction.Skipped),
            TripsToCreate: ordered.Count(g => g.Action == BookeoGroupAction.Create),
            TripsToUpdate: ordered.Count(g => g.Action == BookeoGroupAction.Update),
            TripsToCancel: ordered.Count(g => g.Action == BookeoGroupAction.Cancel),
            BlockedGroups: ordered.Count(g => g.Action == BookeoGroupAction.Blocked),
            Warnings: ordered.Sum(g => g.Issues.Count(i => i.Severity == BookeoIssueCodes.Warning))
                + rows.Sum(r => r.Issues.Count(i => i.Severity == BookeoIssueCodes.Warning)));

        var unmapped = rows
            .Where(r => r.GroupKey is not null && groups[r.GroupKey].IsUnmapped)
            .GroupBy(r => (Code: r.Parsed.ProductCode!, Destination: r.Parsed.Destination?.ToUpperInvariant()))
            .Select(g => new BookeoUnmappedProduct(
                g.Key.Code,
                g.First().Parsed.ProductName ?? g.Key.Code,
                g.First().Parsed.Destination,
                g.Count()))
            .OrderBy(p => p.ProductName, StringComparer.Ordinal)
            .ThenBy(p => p.Destination, StringComparer.Ordinal)
            .ToList();

        var unmatchedUnits = rows
            .Where(r => r.Action is BookeoRowAction.New or BookeoRowAction.Changed && !r.Parsed.IsCanceled)
            .Select(r => BookeoText.NormalizeUnit(r.Parsed.UnitText))
            .OfType<string>()
            .Where(unit => MatchUnit(unit, input).Match is BookeoVehicleMatch.Unmatched or BookeoVehicleMatch.Ambiguous)
            .GroupBy(unit => unit, StringComparer.Ordinal)
            .Select(g => new BookeoUnmatchedUnit(g.Key, g.Count()))
            .OrderBy(u => u.UnitText, StringComparer.Ordinal)
            .ToList();

        return new BookeoImportPlan
        {
            Rows = rows,
            Groups = ordered,
            Summary = summary,
            UnmappedProducts = unmapped,
            UnmatchedUnits = unmatchedUnits,
            Hash = ComputeHash(rows, ordered),
        };
    }

    // ---------------------------------------------------------------- rows

    private static PlannedRow ClassifyRow(BookeoParsedRow parsed, BookeoPlanInput input)
    {
        if (!parsed.IsReadable)
        {
            var unreadable = new PlannedRow { Parsed = parsed, Action = BookeoRowAction.Skipped };
            unreadable.Issues.Add(Block(
                BookeoIssueCodes.RowUnreadable,
                $"Row {parsed.RowNumber} cannot be imported: {string.Join(" ", parsed.Problems)}"));
            return unreadable;
        }

        var built = BookeoBookingBuilder.Build(parsed);
        input.Ledger.TryGetValue(parsed.BookingNumber!, out var ledger);

        BookeoRowAction action;
        IReadOnlyList<string> changed = [];
        if (ledger is null)
        {
            action = parsed.IsCanceled ? BookeoRowAction.Skipped : BookeoRowAction.New;
        }
        else if (parsed.IsCanceled)
        {
            // Already cancelled in the ledger: nothing left to take off.
            action = IsCanceledStatus(ledger.BookeoStatus) ? BookeoRowAction.Unchanged : BookeoRowAction.Cancelled;
        }
        else if (ledger.ContentHash != built.Snapshot.ContentHash)
        {
            action = BookeoRowAction.Changed;
            changed = BookeoBookingBuilder.ChangedFields(ledger.ToSnapshot(), built.Snapshot);
        }
        else
        {
            action = BookeoRowAction.Unchanged;
        }

        var row = new PlannedRow
        {
            Parsed = parsed,
            Action = action,
            ChangedFields = changed,
            Snapshot = built.Snapshot,
            PlaceholderCount = built.PlaceholderCount,
            Ledger = ledger,
        };

        var mapping = ResolveMapping(parsed.ProductCode!, parsed.Destination, input.ProductMappings);
        if (mapping is not null && input.Routes.TryGetValue(mapping.RouteId, out var route) && route.Stops.Count >= 2)
        {
            row.Mapping = mapping;
            row.Route = route;
        }

        switch (action)
        {
            case BookeoRowAction.Skipped:
                row.Issues.Add(Info(
                    BookeoIssueCodes.CanceledNeverImported,
                    $"Booking {parsed.BookingNumber} is canceled in Bookeo and was never imported — nothing to do."));
                break;
            case BookeoRowAction.Cancelled:
                row.Issues.Add(Warning(
                    BookeoIssueCodes.BookingCancelled,
                    $"Booking {parsed.BookingNumber} was canceled in Bookeo; its passengers come off the manifest."));
                break;
            case BookeoRowAction.Changed:
                row.Issues.Add(Info(
                    BookeoIssueCodes.BookingChanged,
                    $"Booking {parsed.BookingNumber} changed in Bookeo: {string.Join(", ", changed)}."));
                break;
        }

        if (action is BookeoRowAction.New or BookeoRowAction.Changed)
        {
            if (row.Route is null)
            {
                row.Issues.Add(Block(
                    BookeoIssueCodes.ProductNotMapped,
                    mapping is null
                        ? $"Bookeo product \"{parsed.ProductName}\" ({parsed.ProductCode}{DestinationSuffix(parsed.Destination)}) is not mapped to a route."
                        : $"Bookeo product \"{parsed.ProductName}\" is mapped to a route that no longer exists."));
            }

            if (parsed.TotalDueCad > 0m)
            {
                row.Issues.Add(Warning(
                    BookeoIssueCodes.PaymentDue,
                    $"Booking {parsed.BookingNumber} still owes {parsed.TotalDueCad.ToString("C2", CultureInfo.GetCultureInfo("en-CA"))}."));
            }

            if (built.PlaceholderCount > 0)
            {
                row.Issues.Add(Warning(
                    BookeoIssueCodes.PassengerPlaceholder,
                    $"Booking {parsed.BookingNumber} names {parsed.Passengers.Count} of {parsed.Participants} passengers; " +
                    $"{built.PlaceholderCount} added as \"{parsed.CustomerName} guest n\"."));
            }
        }

        return row;
    }

    private static bool IsCanceledStatus(string status) => status.Contains("cancel", StringComparison.OrdinalIgnoreCase);

    /// <summary>Exact (code, destination) wins; otherwise the product's any-destination mapping.</summary>
    public static BookeoProductMapping? ResolveMapping(
        string productCode, string? destination, IReadOnlyList<BookeoProductMapping> mappings)
    {
        var forProduct = mappings.Where(m => string.Equals(m.ProductCode, productCode, StringComparison.Ordinal)).ToList();
        return forProduct.FirstOrDefault(m => m.Destination is not null && BookeoText.SameDestination(m.Destination, destination))
            ?? forProduct.FirstOrDefault(m => m.Destination is null);
    }

    // ---------------------------------------------------------------- groups

    public static string GroupKey(Guid routeId, TripDirection? direction, DateOnly date, TimeOnly start) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{routeId:N}|{direction?.ToString() ?? "Any"}|{date:yyyy-MM-dd}|{start:HH\\:mm}");

    private static void BuildAddGroups(List<PlannedRow> rows, Dictionary<string, PlannedGroup> groups, BookeoPlanInput input)
    {
        foreach (var row in rows)
        {
            if (row.Snapshot is null || row.Parsed.IsCanceled)
            {
                continue;
            }

            var isWrite = row.Action is BookeoRowAction.New or BookeoRowAction.Changed;
            if (row.Route is null)
            {
                if (!isWrite)
                {
                    continue; // An unchanged booking whose mapping was since removed: nothing to do.
                }

                var unmappedKey = string.Create(CultureInfo.InvariantCulture,
                    $"unmapped|{row.Snapshot.ProductCode}|{row.Snapshot.Destination?.ToUpperInvariant() ?? "-"}|{row.Snapshot.ServiceDate:yyyy-MM-dd}|{row.Snapshot.WindowStart:HH\\:mm}");
                var bucket = GetOrAdd(groups, unmappedKey, () => new PlannedGroup
                {
                    Key = unmappedKey,
                    IsUnmapped = true,
                    UnmappedProductCode = row.Snapshot.ProductCode,
                    RouteName = null,
                    ServiceDate = row.Snapshot.ServiceDate,
                    WindowStart = row.Snapshot.WindowStart,
                    WindowEnd = row.Snapshot.WindowEnd,
                });
                bucket.AddRows.Add(row);
                row.GroupKey = unmappedKey;
                continue;
            }

            var key = GroupKey(row.Route.Id, row.Mapping!.Direction, row.Snapshot.ServiceDate, row.Snapshot.WindowStart);
            var group = GetOrAdd(groups, key, () => new PlannedGroup
            {
                Key = key,
                RouteId = row.Route.Id,
                Route = row.Route,
                RouteName = row.Route.Name,
                Direction = row.Mapping.Direction,
                ServiceDate = row.Snapshot.ServiceDate,
                WindowStart = row.Snapshot.WindowStart,
                WindowEnd = row.Snapshot.WindowEnd,
            });
            group.WindowEnd ??= row.Snapshot.WindowEnd;

            (isWrite ? group.AddRows : group.MemberRows).Add(row);
            row.GroupKey = key;
        }
    }

    private static void ResolveTargets(
        Dictionary<string, PlannedGroup> groups, IReadOnlyDictionary<Guid, Trip> tripsById, BookeoPlanInput input)
    {
        var claimed = new HashSet<Guid>();
        foreach (var group in groups.Values.Where(g => !g.IsUnmapped).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            // 1. The trip this departure's unmoved bookings already sit on — keeps a dispatcher's
            //    edits to that trip (a retimed departure, a swapped van) from splitting the group.
            var ledgerTrip = group.AddRows.Concat(group.MemberRows)
                .Where(r => r.KeepsLedgerDeparture && r.Ledger!.TripId is not null)
                .Select(r => tripsById.GetValueOrDefault(r.Ledger!.TripId!.Value))
                .OfType<Trip>()
                .Where(t => t.Status != TripStatus.Cancelled && !claimed.Contains(t.Id))
                .OrderBy(t => t.Status == TripStatus.Scheduled ? 0 : 1)
                .ThenBy(t => t.TripNumber, StringComparer.Ordinal)
                .FirstOrDefault();

            // 2. Otherwise an existing Scheduled Community trip at exactly this departure.
            var target = ledgerTrip ?? input.Trips
                .Where(t => t.Status == TripStatus.Scheduled
                    && t.ServiceType == TripServiceType.Community
                    && t.RouteId == group.RouteId
                    && t.Direction == group.Direction
                    && t.ServiceDate == group.ServiceDate
                    && t.WindowStart == group.WindowStart
                    && !claimed.Contains(t.Id))
                .OrderBy(t => t.TripNumber, StringComparer.Ordinal)
                .FirstOrDefault();

            if (target is not null)
            {
                claimed.Add(target.Id);
                group.Target = target;
                group.Manifest = input.ManifestsByTripId.GetValueOrDefault(target.Id);
            }
        }
    }

    private static void BuildRemovals(
        List<PlannedRow> rows,
        Dictionary<string, PlannedGroup> groups,
        IReadOnlyDictionary<Guid, Trip> tripsById,
        BookeoPlanInput input)
    {
        foreach (var row in rows.Where(r => r.Action is BookeoRowAction.Cancelled or BookeoRowAction.Changed))
        {
            if (row.Issues.Any(i => i.Severity == BookeoIssueCodes.Block))
            {
                continue; // Held back entirely (unmapped): its old trip is not touched either.
            }

            if (row.Ledger?.TripId is not { } tripId
                || !tripsById.TryGetValue(tripId, out var trip)
                || trip.Status == TripStatus.Cancelled)
            {
                continue;
            }

            var group = groups.Values.FirstOrDefault(g => g.Target?.Id == trip.Id);
            if (group is not null && group.Key == row.GroupKey)
            {
                continue; // Staying on the same trip: the add replaces its rows in place.
            }

            if (group is null)
            {
                var key = GroupKey(trip.RouteId ?? Guid.Empty, trip.Direction, trip.ServiceDate, trip.WindowStart);
                if (groups.ContainsKey(key))
                {
                    key = $"{key}|{trip.TripNumber}";
                }

                group = new PlannedGroup
                {
                    Key = key,
                    RouteId = trip.RouteId,
                    Route = trip.RouteId is { } routeId ? input.Routes.GetValueOrDefault(routeId) : null,
                    RouteName = trip.RouteName,
                    Direction = trip.Direction,
                    ServiceDate = trip.ServiceDate,
                    WindowStart = trip.WindowStart,
                    WindowEnd = trip.WindowEnd,
                    Target = trip,
                    Manifest = input.ManifestsByTripId.GetValueOrDefault(trip.Id),
                };
                groups[key] = group;
            }

            group.RemoveRows.Add(row);
            row.RemovalGroupKey = group.Key;
        }
    }

    /// <summary>
    /// Builds each adding booking's manifest rows against its group's stop list — the target
    /// trip's own snapshot when there is one, else the route oriented for the direction.
    /// </summary>
    private static void BuildManifestRows(List<PlannedRow> rows, Dictionary<string, PlannedGroup> groups)
    {
        foreach (var row in rows)
        {
            if (row.Snapshot is null || row.GroupKey is null || row.Route is null || row.Parsed.IsCanceled)
            {
                continue;
            }

            var group = groups[row.GroupKey];
            var stops = group.Target is { Stops.Count: >= 2 } target
                ? target.Stops.OrderBy(s => s.Order).ToList()
                : OrientedStops(row.Route, group.Direction);

            var passengers = row.Snapshot.Passengers;
            var fares = BookeoBookingBuilder.SplitFare(row.Snapshot.TotalGrossCad, passengers.Count);
            var paidOnline = row.Snapshot.TotalDueCad == 0m && row.Snapshot.TotalPaidCad > 0m;
            var unmatched = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            var manifestRows = new List<ManifestPassenger>(passengers.Count);
            for (var i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                var (pickup, dropoff) = ResolveStops(passenger.Category, stops, row.Mapping!.ResidentStopRole, unmatched);
                var fare = fares[i];
                var online = paidOnline && fare > 0m;

                manifestRows.Add(new ManifestPassenger
                {
                    Name = passenger.Name,
                    Email = passenger.Email,
                    Phone = passenger.Phone,
                    PickupStopId = pickup.StopId,
                    PickupStopName = pickup.Name,
                    DropoffStopId = dropoff.StopId,
                    DropoffStopName = dropoff.Name,
                    FareAmountCad = online ? fare : null,
                    FarePaymentMethod = online ? FarePaymentMethod.Online : null,
                    ExternalRef = row.ExternalRef,
                });
            }

            row.ManifestRows = manifestRows;
            if (unmatched.Count > 0)
            {
                row.Issues.Add(Warning(
                    BookeoIssueCodes.StopUnmatched,
                    $"Booking {row.BookingNumber}: no stop on {group.RouteName} matches {string.Join(", ", unmatched.Select(u => $"\"{u}\""))}; " +
                    "the name is kept without a stop."));
            }
        }
    }

    public static List<RouteStop> OrientedStops(Route route, TripDirection? direction)
    {
        var outbound = route.Stops.OrderBy(s => s.Order).ToList();
        if (direction != TripDirection.Inbound)
        {
            return outbound;
        }

        // Same reversal as generation and deadhead returns: only Order is re-sequenced, both
        // timetable offsets stay attached to their own stop.
        return outbound
            .AsEnumerable()
            .Reverse()
            .Select((stop, index) => stop with { Order = index })
            .ToList();
    }

    private readonly record struct StopRef(Guid? StopId, string? Name);

    /// <summary>
    /// A passenger category → (pickup, dropoff). "X to Y" names both ends; "&lt;Community&gt;
    /// Residents" puts the community at the mapping's resident role and the route endpoint at the
    /// other end; anything else (e.g. "Adults") rides end to end. A community is matched to a stop
    /// by case-insensitive name containment; no match keeps the name alone and is reported.
    /// </summary>
    private static (StopRef Pickup, StopRef Dropoff) ResolveStops(
        string? category, List<RouteStop> stops, ResidentStopRole role, ISet<string> unmatched)
    {
        var first = new StopRef(stops[0].StopId, stops[0].Name);
        var last = new StopRef(stops[^1].StopId, stops[^1].Name);
        var text = BookeoText.Clean(category);
        if (text is null)
        {
            return (first, last);
        }

        StopRef Find(string community)
        {
            var stop = stops.FirstOrDefault(s =>
                s.Name.Contains(community, StringComparison.OrdinalIgnoreCase)
                || community.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
            if (stop is null)
            {
                unmatched.Add(community);
                return new StopRef(null, community);
            }

            return new StopRef(stop.StopId, stop.Name);
        }

        var between = BetweenCategory().Match(text);
        if (between.Success)
        {
            return (Find(between.Groups["from"].Value.Trim()), Find(between.Groups["to"].Value.Trim()));
        }

        var residents = ResidentsCategory().Match(text);
        if (residents.Success)
        {
            var community = Find(residents.Groups["community"].Value.Trim());
            return role == ResidentStopRole.Pickup ? (community, last) : (first, community);
        }

        return (first, last);
    }

    [GeneratedRegex(@"^(?<from>.+?)\s+to\s+(?<to>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex BetweenCategory();

    [GeneratedRegex(@"^(?<community>.+?)\s+residents?$", RegexOptions.IgnoreCase)]
    private static partial Regex ResidentsCategory();

    // ---------------------------------------------------------------- blocking

    private static IEnumerable<string> Touches(PlannedRow row)
    {
        if (row.GroupKey is not null && row.Action is BookeoRowAction.New or BookeoRowAction.Changed)
        {
            yield return row.GroupKey;
        }

        if (row.RemovalGroupKey is not null)
        {
            yield return row.RemovalGroupKey;
        }
    }

    /// <summary>
    /// Iterates to a fixpoint: compute every group with the bookings that touch an already-blocked
    /// group held back, raise the blocking issues, repeat until no new group blocks. Blocking only
    /// ever grows, so this terminates in at most one pass per group.
    /// </summary>
    private static void ResolveBlocking(Dictionary<string, PlannedGroup> groups, BookeoPlanInput input)
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        for (var pass = 0; pass <= groups.Count; pass++)
        {
            foreach (var group in groups.Values)
            {
                ComputeGroup(group, blocked, input);
            }

            var newly = groups.Values.Where(g => g.IsBlocked && !blocked.Contains(g.Key)).Select(g => g.Key).ToList();
            if (newly.Count == 0)
            {
                break;
            }

            blocked.UnionWith(newly);
        }

        foreach (var group in groups.Values)
        {
            group.Action = DecideAction(group);
        }
    }

    private static void ComputeGroup(PlannedGroup group, IReadOnlySet<string> blocked, BookeoPlanInput input)
    {
        bool Held(PlannedRow row) =>
            row.Issues.Any(i => i.Severity == BookeoIssueCodes.Block)
            || Touches(row).Any(key => key != group.Key && blocked.Contains(key));

        group.Issues.Clear();
        group.EffectiveAdds.Clear();
        group.EffectiveAdds.AddRange(group.AddRows.Where(r => !Held(r) || group.IsUnmapped));
        group.EffectiveRemoves.Clear();
        group.EffectiveRemoves.AddRange(group.RemoveRows.Where(r => !Held(r)));

        var existing = group.Manifest?.Passengers ?? [];
        var replacedRefs = group.EffectiveAdds.Concat(group.EffectiveRemoves)
            .Select(r => r.ExternalRef)
            .ToHashSet(StringComparer.Ordinal);
        var kept = existing.Where(p => p.ExternalRef is null || !replacedRefs.Contains(p.ExternalRef)).ToList();
        var added = group.EffectiveAdds.SelectMany(r => r.ManifestRows).ToList();

        group.PassengersBefore = existing.Count;
        group.ImportedRowsRemoved = existing.Count - kept.Count;
        group.ImportedRowsAdded = added.Count;
        group.PassengersAfterList = [.. kept, .. added];
        group.PassengersAfter = group.PassengersAfterList.Count;
        group.SeatsConfirmedAfter = group.Target is { } trip
            ? Math.Max(0, trip.SeatsConfirmed - group.ImportedRowsRemoved + group.ImportedRowsAdded)
            : group.ImportedRowsAdded;

        if (group.IsUnmapped)
        {
            var first = group.AddRows[0].Parsed;
            group.Issues.Add(Block(
                BookeoIssueCodes.ProductNotMapped,
                $"Map Bookeo product \"{first.ProductName}\"{DestinationSuffix(first.Destination)} to a route to import these bookings."));
            group.VehicleMatch = BookeoVehicleMatch.Blank;
            return;
        }

        ResolveVehicle(group, input);

        if (!group.HasOps)
        {
            return;
        }

        if (group.Target is { Status: not TripStatus.Scheduled } closed)
        {
            group.Issues.Add(Block(
                BookeoIssueCodes.TripNotEditable,
                $"Trip {closed.TripNumber} is {closed.Status}, so its manifest can no longer be changed by an import."));
        }

        if (group.PassengersAfter > ManifestChecklist.MaxPassengers)
        {
            group.Issues.Add(Block(
                BookeoIssueCodes.ManifestCapExceeded,
                $"{group.PassengersAfter} passengers would exceed the manifest's {ManifestChecklist.MaxPassengers}-row limit."));
        }

        var needed = Math.Max(group.PassengersAfter, group.SeatsConfirmedAfter);
        if (group.SeatsCapacity is { } capacity && group.ImportedRowsAdded > 0 && needed > capacity)
        {
            group.Issues.Add(Block(
                BookeoIssueCodes.OverVehicleCapacity,
                $"{needed} passengers would not fit the {capacity} seats on {group.AssignVehicle?.UnitNumber ?? group.Target?.VehicleUnit ?? "the trip"}."));
        }
    }

    /// <summary>
    /// The vehicle question for one group. An existing trip that already has a vehicle keeps it —
    /// the import never overrides the dispatcher. Otherwise the group's Bookeo unit text(s) are
    /// matched; a match is assigned only when Active (capacity is checked as a Block).
    /// </summary>
    private static void ResolveVehicle(PlannedGroup group, BookeoPlanInput input)
    {
        group.AssignVehicle = null;
        group.MatchedVehicle = null;

        var units = group.EffectiveAdds.Concat(group.MemberRows)
            .Select(r => BookeoText.NormalizeUnit(r.Parsed.UnitText))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        group.UnitText = units.FirstOrDefault();

        if (group.Target is { VehicleId: not null } withVehicle)
        {
            group.VehicleMatch = BookeoVehicleMatch.KeptExisting;
            group.SeatsCapacity = withVehicle.SeatsCapacity;
            return;
        }

        group.SeatsCapacity = group.Target?.SeatsCapacity;
        if (units.Count == 0)
        {
            group.VehicleMatch = BookeoVehicleMatch.Blank;
            return;
        }

        var matches = units.Select(u => MatchUnit(u, input)).ToList();
        var vehicles = matches.Where(m => m.Match == BookeoVehicleMatch.Matched).Select(m => m.Vehicle!).DistinctBy(v => v.VehicleId).ToList();
        if (matches.Any(m => m.Match == BookeoVehicleMatch.Ambiguous) || vehicles.Count > 1)
        {
            group.VehicleMatch = BookeoVehicleMatch.Ambiguous;
            return;
        }

        if (vehicles.Count == 0)
        {
            group.VehicleMatch = BookeoVehicleMatch.Unmatched;
            return;
        }

        group.VehicleMatch = BookeoVehicleMatch.Matched;
        group.MatchedVehicle = vehicles[0];
        if (vehicles[0].IsActive)
        {
            group.AssignVehicle = vehicles[0];
            group.SeatsCapacity = vehicles[0].SeatingCapacity;
        }
    }

    private static BookeoGroupAction DecideAction(PlannedGroup group)
    {
        if (group.IsBlocked)
        {
            return BookeoGroupAction.Blocked;
        }

        if (!group.HasOps)
        {
            return BookeoGroupAction.Unchanged;
        }

        if (group.Target is null)
        {
            return group.EffectiveAdds.Count > 0 ? BookeoGroupAction.Create : BookeoGroupAction.Unchanged;
        }

        return group.Target.IsCreatedByBookeoImport
            && group.Target.Status == TripStatus.Scheduled
            && group.PassengersAfter == 0
            ? BookeoGroupAction.Cancel
            : BookeoGroupAction.Update;
    }

    // ---------------------------------------------------------------- vehicles

    /// <summary>
    /// Bookeo unit text → fleet vehicle: an explicit unit mapping first, else a normalized text
    /// equal to exactly one vehicle's unit number (the replica carries no make/model to try).
    /// </summary>
    public static (BookeoVehicleMatch Match, VehicleLookup? Vehicle) MatchUnit(string? unitText, BookeoPlanInput input)
    {
        var normalized = BookeoText.NormalizeUnit(unitText);
        if (normalized is null)
        {
            return (BookeoVehicleMatch.Blank, null);
        }

        var mapping = input.UnitMappings.FirstOrDefault(m => m.UnitText == normalized);
        if (mapping is not null)
        {
            var mapped = input.Vehicles.FirstOrDefault(v => v.VehicleId == mapping.VehicleId);
            return mapped is null ? (BookeoVehicleMatch.Unmatched, null) : (BookeoVehicleMatch.Matched, mapped);
        }

        var hits = input.Vehicles.Where(v => BookeoText.NormalizeUnit(v.UnitNumber) == normalized).ToList();
        return hits.Count switch
        {
            0 => (BookeoVehicleMatch.Unmatched, null),
            1 => (BookeoVehicleMatch.Matched, hits[0]),
            _ => (BookeoVehicleMatch.Ambiguous, null),
        };
    }

    // ---------------------------------------------------------------- warnings

    private static void RaiseWarnings(Dictionary<string, PlannedGroup> groups, List<PlannedRow> rows, BookeoPlanInput input)
    {
        var localNow = WinnipegNow(input.Now);
        var plannedAssignments = groups.Values
            .Where(g => g.Action is BookeoGroupAction.Create or BookeoGroupAction.Update && g.AssignVehicle is not null)
            .Select(g => (Group: g, VehicleId: g.AssignVehicle!.VehicleId))
            .ToList();

        foreach (var group in groups.Values.Where(g => g.Action is BookeoGroupAction.Create or BookeoGroupAction.Update or BookeoGroupAction.Cancel))
        {
            var targetId = group.Target?.Id;
            var window = Window(group.ServiceDate, group.Target?.WindowStart ?? group.WindowStart, group.Target?.WindowEnd ?? group.WindowEnd);
            var others = input.Trips
                .Where(t => t.Id != targetId && t.Status != TripStatus.Cancelled && t.ServiceDate == group.ServiceDate)
                .ToList();

            if (group.Action == BookeoGroupAction.Cancel)
            {
                group.Issues.Add(Warning(
                    BookeoIssueCodes.TripWillBeCancelled,
                    $"Every booking on trip {group.Target!.TripNumber} is cancelled; the import created it, so it will be cancelled."));
                continue;
            }

            if (group.ImportedRowsAdded > 0 && group.VehicleMatch != BookeoVehicleMatch.KeptExisting)
            {
                switch (group.VehicleMatch)
                {
                    case BookeoVehicleMatch.Unmatched:
                        group.Issues.Add(Warning(BookeoIssueCodes.VehicleUnmatched,
                            $"No fleet vehicle matches Bookeo unit \"{group.UnitText}\"; the trip stays unassigned. Map the unit to a vehicle to fix."));
                        break;
                    case BookeoVehicleMatch.Ambiguous:
                        group.Issues.Add(Warning(BookeoIssueCodes.VehicleAmbiguous,
                            $"Bookeo unit \"{group.UnitText}\" matches more than one fleet vehicle; the trip stays unassigned. Map the unit to one vehicle to fix."));
                        break;
                    case BookeoVehicleMatch.Matched when group.AssignVehicle is null:
                        group.Issues.Add(Warning(BookeoIssueCodes.VehicleNotActive,
                            $"Vehicle {group.MatchedVehicle!.UnitNumber} is {group.MatchedVehicle.Status}, not Active; the trip stays unassigned."));
                        break;
                }
            }

            var vehicleId = group.VehicleMatch == BookeoVehicleMatch.KeptExisting ? group.Target!.VehicleId : group.AssignVehicle?.VehicleId;
            if (vehicleId is { } vid)
            {
                var clash = others.FirstOrDefault(t => t.VehicleId == vid && Overlaps(window, Window(t.ServiceDate, t.WindowStart, t.WindowEnd)))?.TripNumber
                    ?? plannedAssignments
                        .Where(p => p.Group != group && p.VehicleId == vid && p.Group.ServiceDate == group.ServiceDate
                            && Overlaps(window, Window(p.Group.ServiceDate, p.Group.WindowStart, p.Group.WindowEnd)))
                        .Select(p => $"the new {p.Group.WindowStart:HH\\:mm} trip")
                        .FirstOrDefault();
                if (clash is not null)
                {
                    group.Issues.Add(Warning(BookeoIssueCodes.VehicleDoubleBooked,
                        $"The vehicle is also on {clash}, which overlaps this departure."));
                }
            }

            if (group.Target?.DriverId is { } driverId)
            {
                var clash = others.FirstOrDefault(t => t.DriverId == driverId && Overlaps(window, Window(t.ServiceDate, t.WindowStart, t.WindowEnd)));
                if (clash is not null)
                {
                    group.Issues.Add(Warning(BookeoIssueCodes.DriverDoubleBooked,
                        $"{group.Target.DriverName} also drives {clash.TripNumber}, which overlaps this departure."));
                }
            }

            if (group.Action == BookeoGroupAction.Create)
            {
                var start = group.ServiceDate.ToDateTime(group.WindowStart);
                var near = input.Trips.FirstOrDefault(t =>
                    t.Status == TripStatus.Scheduled
                    && t.ServiceType == TripServiceType.Community
                    && t.RouteId == group.RouteId
                    && t.ServiceDate == group.ServiceDate
                    && (t.ServiceDate.ToDateTime(t.WindowStart) - start).Duration() <= DuplicateWindow);
                if (near is not null)
                {
                    group.Issues.Add(Warning(BookeoIssueCodes.PossibleDuplicateTrip,
                        $"Trip {near.TripNumber} on the same route departs at {near.WindowStart:HH\\:mm}; check this is not the same run."));
                }
            }

            if (group.ServiceDate.ToDateTime(group.WindowStart) < localNow)
            {
                group.Issues.Add(Warning(BookeoIssueCodes.PastDeparture,
                    $"The {group.ServiceDate:yyyy-MM-dd} {group.WindowStart:HH\\:mm} departure is already in the past."));
            }
        }

        // Re-decide after the warnings: none of them blocks, but keep the action honest.
        foreach (var group in groups.Values)
        {
            group.Action = DecideAction(group);
        }
    }

    private static void RaisePassengerDoubleBookings(List<PlannedRow> rows)
    {
        var riding = rows
            .Where(r => r.Snapshot is not null && !r.Parsed.IsCanceled && r.Action != BookeoRowAction.Skipped)
            .SelectMany(r => r.Snapshot!.Passengers.Select(p => (Row: r, Key: $"{BookeoText.Clean(p.Name)?.ToUpperInvariant()}|{BookeoText.Digits(p.Phone)}")))
            .ToList();

        foreach (var row in rows.Where(r => r.Snapshot is not null))
        {
            var mine = Window(row.Snapshot!.ServiceDate, row.Snapshot.WindowStart, row.Snapshot.WindowEnd);
            var clashes = riding
                .Where(a => a.Row == row)
                .SelectMany(a => riding.Where(b => b.Row != row
                    && b.Key == a.Key
                    && Overlaps(mine, Window(b.Row.Snapshot!.ServiceDate, b.Row.Snapshot.WindowStart, b.Row.Snapshot.WindowEnd))))
                .Select(b => b.Row.BookingNumber)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (clashes.Count > 0)
            {
                row.Issues.Add(Warning(BookeoIssueCodes.PassengerDoubleBooked,
                    $"A passenger on booking {row.BookingNumber} is also on overlapping booking(s) {string.Join(", ", clashes)}."));
            }
        }
    }

    private static (DateTime Start, DateTime End) Window(DateOnly date, TimeOnly start, TimeOnly? end)
    {
        var from = date.ToDateTime(start);
        var to = end is { } e ? date.ToDateTime(e) : from + OpenEndedDuration;
        if (to <= from)
        {
            to = to.AddDays(1); // Runs past midnight.
        }

        return (from, to);
    }

    private static bool Overlaps((DateTime Start, DateTime End) a, (DateTime Start, DateTime End) b) =>
        a.Start < b.End && b.Start < a.End;

    /// <summary>"Now" as a wall clock in Manitoba — Bookeo times are local and carry no zone.</summary>
    private static DateTime WinnipegNow(DateTimeOffset now)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("America/Winnipeg");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }

        return TimeZoneInfo.ConvertTime(now, zone).DateTime;
    }

    // ---------------------------------------------------------------- hash + helpers

    private static string ComputeHash(IReadOnlyList<PlannedRow> rows, IReadOnlyList<PlannedGroup> groups)
    {
        var builder = new StringBuilder();
        foreach (var row in rows.OrderBy(r => r.Parsed.RowNumber))
        {
            builder.Append("R|").Append(row.BookingNumber).Append('|').Append(row.Action)
                .Append('|').Append(row.Snapshot?.ContentHash).Append('|').Append(row.GroupKey)
                .Append('|').Append(row.RemovalGroupKey).Append('|').Append(row.Blocked)
                .Append('|').Append(string.Join(',', row.Issues.Select(i => i.Code))).Append('\n');
        }

        foreach (var group in groups)
        {
            builder.Append("G|").Append(group.Key).Append('|').Append(group.Action)
                .Append('|').Append(group.Target?.Id).Append('|').Append(group.Target?.Version)
                .Append('|').Append(group.Manifest?.Id).Append('|').Append(group.Manifest?.Version)
                .Append('|').Append(group.PassengersBefore).Append('|').Append(group.PassengersAfter)
                .Append('|').Append(group.SeatsConfirmedAfter).Append('|').Append(group.VehicleMatch)
                .Append('|').Append(group.AssignVehicle?.VehicleId).Append('|').Append(group.SeatsCapacity)
                .Append('|').Append(string.Join(',', group.Issues
                    .Where(i => i.Code != BookeoIssueCodes.PastDeparture)
                    .Select(i => i.Code)))
                .Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static PlannedGroup GetOrAdd(Dictionary<string, PlannedGroup> groups, string key, Func<PlannedGroup> create)
    {
        if (!groups.TryGetValue(key, out var group))
        {
            group = create();
            groups[key] = group;
        }

        return group;
    }

    private static string DestinationSuffix(string? destination) =>
        destination is null ? string.Empty : $", {destination}";

    private static ImportIssue Block(string code, string message) => new(code, BookeoIssueCodes.Block, message);

    private static ImportIssue Warning(string code, string message) => new(code, BookeoIssueCodes.Warning, message);

    private static ImportIssue Info(string code, string message) => new(code, BookeoIssueCodes.Info, message);
}
