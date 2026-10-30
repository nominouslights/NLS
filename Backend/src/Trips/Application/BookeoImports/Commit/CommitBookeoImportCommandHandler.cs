using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.BookeoImports.Planning;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.BookeoImports.Commit;

/// <summary>
/// The commit: guards (exists → not yet committed → hash still matches), then applies every
/// non-blocked group in one unit of work — create trips through <see cref="Trip.ScheduleFromImport"/>,
/// add/replace/remove only the manifest rows carrying this import's <c>bookeo:&lt;n&gt;</c> refs
/// (manual passengers are never touched), cancel an import-created trip left empty, write the
/// ledger, stamp the batch — and saves once. Trip numbers are minted before the save, outside its
/// transaction, so a failed commit can burn numbers (unique, not gapless — as everywhere else).
/// </summary>
public sealed class CommitBookeoImportCommandHandler(
    IBookeoImportRepository repository,
    BookeoImportPlanLoader planLoader,
    ITripNumberGenerator tripNumbers,
    TimeProvider clock)
    : ICommandHandler<CommitBookeoImportCommand, BookeoImportCommitResult>
{
    private const string CancelReason = "Every Bookeo booking on this trip was cancelled (Bookeo import).";

    public async Task<Result<BookeoImportCommitResult>> Handle(
        CommitBookeoImportCommand command, CancellationToken cancellationToken)
    {
        var batch = await repository.GetBatchAsync(command.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result.Failure<BookeoImportCommitResult>(BookeoImportErrors.BatchNotFound);
        }

        if (batch.IsCommitted)
        {
            return Result.Failure<BookeoImportCommitResult>(BookeoImportErrors.AlreadyCommitted);
        }

        if (string.IsNullOrWhiteSpace(command.PlanHash))
        {
            return Result.Failure<BookeoImportCommitResult>(BookeoImportErrors.PlanHashRequired);
        }

        var rows = BookeoImportJson.DeserializeRows(batch.ParsedRowsJson);
        var plan = await planLoader.PlanAsync(rows, cancellationToken);
        if (!string.Equals(plan.Hash, command.PlanHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<BookeoImportCommitResult>(BookeoImportErrors.PreviewStale);
        }

        var now = clock.GetUtcNow();
        var enteredBy = Truncate($"Bookeo import · {command.CommittedBy}", 128);
        var tripIdByGroup = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var created = new List<string>();
        int updated = 0, cancelled = 0;

        // Belt and braces behind the planner (which never targets a deadhead and blocks a group
        // whose trip has become one): if a plan would still write passengers onto an empty leg,
        // the database has moved past anything a dispatcher previewed.
        if (plan.Groups.Any(g => g.Action is BookeoGroupAction.Update or BookeoGroupAction.Cancel
            && g.Target is { IsEmptyLeg: true }))
        {
            return Result.Failure<BookeoImportCommitResult>(BookeoImportErrors.PreviewStale);
        }

        foreach (var group in plan.Groups)
        {
            Result applied;
            switch (group.Action)
            {
                case BookeoGroupAction.Create:
                    var tripNumber = await tripNumbers.NextAsync(command.TenantId, cancellationToken);
                    var creation = CreateTrip(command.TenantId, tripNumber, group, enteredBy);
                    if (creation.IsFailure)
                    {
                        return Result.Failure<BookeoImportCommitResult>(creation.Error);
                    }

                    tripIdByGroup[group.Key] = creation.Value;
                    created.Add(tripNumber);
                    continue;

                case BookeoGroupAction.Update:
                    applied = UpdateTrip(command.TenantId, group, enteredBy, cancel: false);
                    updated++;
                    break;

                case BookeoGroupAction.Cancel:
                    applied = UpdateTrip(command.TenantId, group, enteredBy, cancel: true);
                    cancelled++;
                    break;

                default:
                    if (group.Target is not null)
                    {
                        tripIdByGroup[group.Key] = group.Target.Id;
                    }

                    continue;
            }

            if (applied.IsFailure)
            {
                return Result.Failure<BookeoImportCommitResult>(applied.Error);
            }

            tripIdByGroup[group.Key] = group.Target!.Id;
        }

        int imported = 0, bookingsCancelled = 0;
        foreach (var row in plan.Rows.Where(r => !r.Blocked && r.Snapshot is not null))
        {
            Guid? TargetTrip() => row.GroupKey is { } key && tripIdByGroup.TryGetValue(key, out var id) ? id : null;

            switch (row.Action)
            {
                case BookeoRowAction.New:
                    repository.AddBooking(BookeoBooking.Record(command.TenantId, row.Snapshot!, TargetTrip(), batch.Id, now));
                    imported++;
                    break;
                case BookeoRowAction.Changed:
                    row.Ledger!.Apply(row.Snapshot!, TargetTrip(), batch.Id, now);
                    imported++;
                    break;
                case BookeoRowAction.Cancelled:
                    row.Ledger!.Apply(row.Snapshot!, tripId: null, batch.Id, now);
                    bookingsCancelled++;
                    break;
                case BookeoRowAction.Unchanged:
                    row.Ledger?.Touch(batch.Id, now);
                    break;
            }
        }

        var stamped = batch.MarkCommitted(command.CommittedBy, now);
        if (stamped.IsFailure)
        {
            return Result.Failure<BookeoImportCommitResult>(stamped.Error);
        }

        var outcome = await repository.SaveAsync(cancellationToken);
        return outcome switch
        {
            BookeoSaveOutcome.BatchAlreadyCommitted => Result.Failure<BookeoImportCommitResult>(BookeoImportErrors.AlreadyCommitted),
            BookeoSaveOutcome.Conflict => Result.Failure<BookeoImportCommitResult>(BookeoImportErrors.PreviewStale),
            _ => Result.Success(new BookeoImportCommitResult(
                batch.Id,
                TripsCreated: created.Count,
                TripsUpdated: updated,
                TripsCancelled: cancelled,
                BookingsImported: imported,
                BookingsCancelled: bookingsCancelled,
                GroupsSkippedBlocked: plan.Groups.Count(g => g.Action == BookeoGroupAction.Blocked),
                CreatedTripNumbers: created)),
        };
    }

    private Result<Guid> CreateTrip(Guid tenantId, string tripNumber, PlannedGroup group, string enteredBy)
    {
        var route = group.Route!;
        var stops = BookeoImportPlanner.OrientedStops(route, group.Direction);
        var vehicle = group.AssignVehicle;

        var trip = Trip.ScheduleFromImport(
            tenantId,
            tripNumber,
            group.ServiceDate,
            group.WindowStart,
            group.WindowEnd,
            route.Id,
            route.Name,
            stops[0].Name,
            stops[^1].Name,
            stops,
            route.DistanceKm,
            group.Direction,
            seatsConfirmed: group.PassengersAfter,
            vehicle?.VehicleId,
            vehicle?.UnitNumber,
            vehicle?.SeatingCapacity);
        if (trip.IsFailure)
        {
            return Result.Failure<Guid>(trip.Error);
        }

        var manifest = TripManifest.Create(
            tenantId,
            group.ServiceDate,
            tripNumber,
            route.Name,
            group.Direction,
            client: null,
            group.PassengersAfterList,
            allSeatbeltsVerified: false,
            cargo: [],
            allCargoSecured: null,
            ManifestSource.Dispatcher,
            enteredBy);
        if (manifest.IsFailure)
        {
            return Result.Failure<Guid>(manifest.Error);
        }

        // Linked here, in the same save, rather than left to the async attach reaction (which
        // then finds it already linked and no-ops).
        var linked = trip.Value.AttachManifest(manifest.Value.Id);
        if (linked.IsFailure)
        {
            return Result.Failure<Guid>(linked.Error);
        }

        repository.AddTrip(trip.Value);
        repository.AddManifest(manifest.Value);
        return Result.Success(trip.Value.Id);
    }

    private Result UpdateTrip(Guid tenantId, PlannedGroup group, string enteredBy, bool cancel)
    {
        var trip = group.Target!;

        var manifestResult = WriteManifest(tenantId, group, trip, enteredBy);
        if (manifestResult.IsFailure)
        {
            return manifestResult;
        }

        // Vehicle and seats: assign first when seats grow (the vehicle's capacity then bounds the
        // new count), record first when they shrink (so the assignment's guard sees the new count).
        var assign = group.AssignVehicle is { } vehicle && trip.VehicleId is null && !cancel
            ? () => trip.AssignVehicle(vehicle.VehicleId, vehicle.UnitNumber, vehicle.SeatingCapacity)
            : (Func<Result>?)null;
        var seats = !trip.ServiceType.IsCargoService() && group.SeatsConfirmedAfter != trip.SeatsConfirmed
            ? () => trip.RecordDemand(group.SeatsConfirmedAfter, trip.DemandGuaranteed)
            : (Func<Result>?)null;

        var steps = group.SeatsConfirmedAfter >= trip.SeatsConfirmed ? new[] { assign, seats } : [seats, assign];
        foreach (var step in steps)
        {
            if (step?.Invoke() is { IsFailure: true } failed)
            {
                return failed;
            }
        }

        return cancel ? trip.Cancel(CancelReason) : Result.Success();
    }

    private Result WriteManifest(Guid tenantId, PlannedGroup group, Trip trip, string enteredBy)
    {
        if (group.Manifest is { } manifest)
        {
            return manifest.Update(
                manifest.TripDate,
                manifest.TripNumber,
                manifest.Route,
                manifest.Direction,
                manifest.Client,
                group.PassengersAfterList,
                manifest.AllSeatbeltsVerified,
                manifest.Cargo,
                manifest.AllCargoSecured,
                ManifestSource.Dispatcher,
                enteredBy);
        }

        if (group.PassengersAfterList.Count == 0)
        {
            return Result.Success();
        }

        var created = TripManifest.Create(
            tenantId,
            trip.ServiceDate,
            trip.TripNumber,
            trip.RouteName,
            trip.Direction,
            trip.ClientName,
            group.PassengersAfterList,
            allSeatbeltsVerified: false,
            cargo: [],
            allCargoSecured: null,
            ManifestSource.Dispatcher,
            enteredBy);
        if (created.IsFailure)
        {
            return created;
        }

        repository.AddManifest(created.Value);
        return trip.ManifestId is null ? trip.AttachManifest(created.Value.Id) : Result.Success();
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
