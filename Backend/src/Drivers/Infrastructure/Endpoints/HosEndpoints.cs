using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Drivers.Application.Drivers.SelfAccess;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.Alerts;
using NorthernLink.Drivers.Application.Hos.DutyDays.Certify;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetOne;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;
using NorthernLink.Drivers.Application.Hos.DutyDays.Transcribe;
using NorthernLink.Drivers.Application.Hos.DutyEvents.Amend;
using NorthernLink.Drivers.Application.Hos.DutyEvents.Record;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Get;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Report;
using NorthernLink.Drivers.Application.Hos.Exceptions.Acknowledge;
using NorthernLink.Drivers.Application.Hos.Exceptions.Declare;
using NorthernLink.Drivers.Application.Hos.Exceptions.Get;
using NorthernLink.Drivers.Application.Hos.Exceptions.Withdraw;
using NorthernLink.Drivers.Application.Hos.GetForDriver;
using NorthernLink.Drivers.Application.Hos.Record;
using NorthernLink.Drivers.Application.Hos.Status;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Infrastructure.Endpoints;

/// <summary>
/// The hours-of-service surface under <c>/api/drivers</c>, mapped by
/// <see cref="DriversEndpoints.MapDriversEndpoints"/>: its own sibling group carrying
/// <see cref="AuthorizationPolicies.HosAccess"/> (DriverAccess widened with ComplianceOfficer).
/// Because that policy admits every Driver, every <c>{driverId}</c> route runs the
/// caller-owns-this-row check first — with <see cref="Roles.HosRecordOverride"/> as the roles
/// allowed onto other people's records, and <b>no</b> override at all on certification.
/// <para>
/// Every figure these routes return is a planning figure from the app's ledger, labelled as
/// such (<see cref="HosDisplay.PlanningLabel"/>): the record of duty status is on the bus's ELD.
/// </para>
/// </summary>
public static class HosEndpoints
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        var hos = app.MapGroup("/api/drivers")
            .RequireAuthorization(AuthorizationPolicies.HosAccess);

        // Legacy daily-totals log (HosLogEntry). Same routes, same bodies, same 201 — moved from
        // the DriverAccess group so a Compliance Officer can read and key them too.
        hos.MapGet("{driverId:guid}/hos", GetDriverHosEntries);
        hos.MapPost("{driverId:guid}/hos", RecordDriverHosEntry);

        // The duty-event ledger.
        hos.MapPost("{driverId:guid}/hos/duty-events", RecordDutyEvent);
        hos.MapPost("{driverId:guid}/hos/duty-events/{eventId:guid}/amend", AmendDutyEvent);
        hos.MapPost("{driverId:guid}/hos/days/{date}/transcribe", TranscribeDutyDay);
        hos.MapGet("{driverId:guid}/hos/days", GetDutyDays);
        hos.MapGet("{driverId:guid}/hos/days/{date}", GetDutyDay);
        hos.MapGet("{driverId:guid}/hos/status", GetHosStatus);

        // Certification is the driver's own act — no dispatch or compliance override. The
        // marker metadata is what DriverSurfaceMetadataTests pins so the override cannot creep
        // back in through a refactor.
        hos.MapPost("{driverId:guid}/hos/days/{date}/certify", CertifyDutyDay)
            .WithMetadata(new OwnDriverOnlyMetadata());

        // Exceptions the driver (or dispatch for them) declares, and ELD malfunctions they
        // report. Acknowledging is dispatch's act and withdrawing an acknowledged one is
        // compliance's — the handlers draw those lines; the own-record check draws the first.
        // Deferral approval, rejection and resolution live on the ComplianceAccess group.
        hos.MapGet("{driverId:guid}/hos/exceptions", GetExceptions);
        hos.MapPost("{driverId:guid}/hos/exceptions", DeclareException);
        hos.MapPost("{driverId:guid}/hos/exceptions/{id:guid}/acknowledge", AcknowledgeException);
        hos.MapPost("{driverId:guid}/hos/exceptions/{id:guid}/withdraw", WithdrawException);
        hos.MapGet("{driverId:guid}/hos/eld-malfunctions", GetEldMalfunctions);
        hos.MapPost("{driverId:guid}/hos/eld-malfunctions", ReportEldMalfunction);
    }

    // ---- Exceptions and ELD malfunctions ----

    private static async Task<IResult> GetExceptions(
        Guid driverId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        // Default: the 14-day window ending today, like the days list.
        var today = HosLocalTime.DateOf(clock.GetUtcNow());
        var rangeTo = to ?? today;
        var rangeFrom = from ?? rangeTo.AddDays(-13);

        var result = await sender.Query(new GetHosExceptionsQuery(tenantId, driverId, rangeFrom, rangeTo), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeclareException(
        Guid driverId,
        DeclareHosExceptionRequest request,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var kind = HosDisplay.ExceptionKindFromWire(request.Kind);
        if (kind.IsFailure)
        {
            return EndpointResults.Problem(kind.Error);
        }

        if (request.EffectiveFromUtc is not { } effectiveFrom)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.EffectiveFromRequired", "effectiveFromUtc is required."));
        }

        var command = new DeclareHosExceptionCommand(
            tenantId,
            driverId,
            kind.Value,
            effectiveFrom,
            request.EffectiveToUtc,
            request.Reason,
            request.Location,
            request.TripId,
            request.NotKnownBeforeDeparture ?? false,
            request.ExtraDrivingMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            request.EldRemarkRecorded ?? false);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/hos/exceptions/{result.Value.Id}", result.Value)
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> AcknowledgeException(
        Guid driverId,
        Guid id,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var result = await sender.Send(new AcknowledgeHosExceptionCommand(tenantId, driverId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> WithdrawException(
        Guid driverId,
        Guid id,
        ExceptionNoteRequest? request,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var result = await sender.Send(new WithdrawHosExceptionCommand(tenantId, driverId, id, request?.Note), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetEldMalfunctions(
        Guid driverId,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var result = await sender.Query(new GetEldMalfunctionsQuery(tenantId, driverId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ReportEldMalfunction(
        Guid driverId,
        ReportEldMalfunctionRequest request,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        if (request.NoticedAtUtc is not { } noticedAt)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.NoticedAtRequired", "noticedAtUtc is required."));
        }

        var command = new ReportEldMalfunctionCommand(
            tenantId, driverId, request.VehicleId, request.Code, noticedAt, request.Description);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/hos/eld-malfunctions/{result.Value.Id}", result.Value)
            : EndpointResults.Problem(result.Error);
    }

    // ---- Ledger writes ----

    private static async Task<IResult> RecordDutyEvent(
        Guid driverId,
        DutyEventRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var dutyResult = HosDisplay.DutyFromWire(request.Status);
        if (dutyResult.IsFailure)
        {
            return EndpointResults.Problem(dutyResult.Error);
        }

        var sourceResult = HosDisplay.SourceFromWire(request.Source);
        if (sourceResult.IsFailure)
        {
            return EndpointResults.Problem(sourceResult.Error);
        }

        if (request.AtUtc is not { } atUtc)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.AtUtcRequired", "atUtc is required."));
        }

        // The body's eventId wins; the Idempotency-Key header stands in when the body has none
        // (a non-Guid key is hashed to a stable id). This is the first endpoint to honour the
        // header — the Driver Field App's sync queue sends its command id there.
        var eventId = request.EventId ?? IdempotencyEventId(idempotencyKey);

        var command = new RecordDutyEventCommand(
            tenantId,
            driverId,
            eventId,
            atUtc,
            dutyResult.Value,
            sourceResult.Value,
            request.EnteredBy,
            request.CarrierName,
            request.Note,
            request.OdometerStartKm,
            request.OdometerEndKm,
            request.PcNoPassengersOrCargo ?? false,
            request.PcNoTrailer ?? false,
            request.PcNoWork ?? false);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return EndpointResults.Problem(result.Error);
        }

        // 201 the first time, 200 with the identical body on a replay — same answer either way.
        return result.Value.Created
            ? Results.Created($"/api/drivers/{driverId}/hos/days/{result.Value.LocalDate:yyyy-MM-dd}", result.Value)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> AmendDutyEvent(
        Guid driverId,
        Guid eventId,
        AmendDutyEventRequest request,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var dutyResult = HosDisplay.DutyFromWire(request.Status);
        if (dutyResult.IsFailure)
        {
            return EndpointResults.Problem(dutyResult.Error);
        }

        var sourceResult = HosDisplay.SourceFromWire(request.Source);
        if (sourceResult.IsFailure)
        {
            return EndpointResults.Problem(sourceResult.Error);
        }

        if (request.AtUtc is not { } atUtc)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.AtUtcRequired", "atUtc is required."));
        }

        var command = new AmendDutyEventCommand(
            tenantId,
            driverId,
            eventId,
            request.ReplacementEventId,
            atUtc,
            dutyResult.Value,
            sourceResult.Value,
            request.EnteredBy,
            request.CarrierName,
            request.Note,
            request.OdometerStartKm,
            request.OdometerEndKm,
            request.PcNoPassengersOrCargo ?? false,
            request.PcNoTrailer ?? false,
            request.PcNoWork ?? false,
            request.Reason);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return EndpointResults.Problem(result.Error);
        }

        return result.Value.Created
            ? Results.Created($"/api/drivers/{driverId}/hos/days/{result.Value.LocalDate:yyyy-MM-dd}", result.Value)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> TranscribeDutyDay(
        Guid driverId,
        DateOnly date,
        TranscribeDutyDayRequest request,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var sourceResult = HosDisplay.SourceFromWire(request.Source);
        if (sourceResult.IsFailure)
        {
            return EndpointResults.Problem(sourceResult.Error);
        }

        var entries = new List<TranscriptionEntry>();
        foreach (var entry in request.Entries ?? [])
        {
            var time = HosLocalTime.ParseTime(entry.Time);
            if (time.IsFailure)
            {
                return EndpointResults.Problem(time.Error);
            }

            var status = HosDisplay.DutyFromWire(entry.Status);
            if (status.IsFailure)
            {
                return EndpointResults.Problem(status.Error);
            }

            entries.Add(new TranscriptionEntry(time.Value, status.Value, entry.Note));
        }

        var command = new TranscribeDutyDayCommand(
            tenantId,
            driverId,
            date,
            request.TranscriptionId ?? Guid.NewGuid(),
            sourceResult.Value,
            request.EnteredBy,
            request.CarrierName,
            entries);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return EndpointResults.Problem(result.Error);
        }

        return result.Value.Appended > 0
            ? Results.Created($"/api/drivers/{driverId}/hos/days/{date:yyyy-MM-dd}", result.Value)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> CertifyDutyDay(
        Guid driverId,
        DateOnly date,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // Driver only. A dispatcher or compliance officer holds HosAccess and so reaches this
        // route, but may not certify someone else's record — the regulation puts the driver's
        // name on it, so the answer is a 403 with the reason, not a silent success.
        if (!await selfAccess.IsThisDriverAsync(driverId, cancellationToken))
        {
            return Results.Json(
                new { code = HosErrors.CertifyDriverOnly.Code, message = HosErrors.CertifyDriverOnly.Message },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await sender.Send(new CertifyDutyDayCommand(tenantId, driverId, date), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    // ---- Ledger reads ----

    private static async Task<IResult> GetDutyDays(
        Guid driverId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        // Default: the regulation's 14-day window ending today (America/Winnipeg).
        var today = HosLocalTime.DateOf(clock.GetUtcNow());
        var rangeTo = to ?? today;
        var rangeFrom = from ?? rangeTo.AddDays(-13);

        var result = await sender.Query(new GetDutyDaysQuery(tenantId, driverId, rangeFrom, rangeTo), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetDutyDay(
        Guid driverId,
        DateOnly date,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var result = await sender.Query(new GetDutyDayQuery(tenantId, driverId, date), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetHosStatus(
        Guid driverId,
        ITenantContext tenantContext,
        IDriverSelfAccess selfAccess,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var result = await sender.Query(new GetHosStatusQuery(tenantId, driverId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>The roster batch — mapped by <see cref="DriversEndpoints"/> onto the DriverRosterAccess group.</summary>
    internal static async Task<IResult> GetRosterHosStatus(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetHosRosterStatusQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Today's compliance alerts for the Dispatch Board tile and the Compliance screen — mapped
    /// by <see cref="DriversEndpoints"/> onto the DriverRosterAccess group beside the roster
    /// batch. Counts and rows for near/over an hours limit, out of service, open ELD
    /// malfunctions, yesterday's uncertified records and unacknowledged exceptions. The
    /// board's "trips at risk" figure is deliberately absent: Trips owns the assignment and the
    /// fitness verdict, so it serves that number itself.
    /// </summary>
    internal static async Task<IResult> GetTodayAlerts(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetHosAlertsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    // ---- Legacy daily totals ----

    private static async Task<IResult> GetDriverHosEntries(
        Guid driverId, ITenantContext tenantContext, IDriverSelfAccess selfAccess, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var result = await sender.Query(new GetHosEntriesQuery(tenantId, driverId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RecordDriverHosEntry(
        Guid driverId, HosEntryRequest request, ITenantContext tenantContext, IDriverSelfAccess selfAccess, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // The one that matters most on this whole surface: a duty log is legally binding
        // compliance data, and HosAccess admits every driver. Without this, driver A files
        // hours under driver B's name.
        if (!await selfAccess.MayActOnDriverHosAsync(driverId, cancellationToken))
        {
            return DriversEndpoints.NotYourDriverRecord();
        }

        var dutyResult = HosDisplay.DutyFromWire(request.Duty);
        if (dutyResult.IsFailure)
        {
            return EndpointResults.Problem(dutyResult.Error);
        }

        // Source defaults to ManualPaperBackup when the body omits it. The Dispatch Console sends
        // no source field and must keep meaning "a dispatcher typed this" — flipping the default
        // would silently relabel every console entry as a driver submission and corrupt the HOS
        // record. The Driver Field App sends "Driver App" explicitly.
        var sourceResult = HosDisplay.LegacySourceFromWire(request.Source);
        if (sourceResult.IsFailure)
        {
            return EndpointResults.Problem(sourceResult.Error);
        }

        var command = new RecordHosEntryCommand(
            tenantId,
            driverId,
            request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            dutyResult.Value,
            request.OnDutyH,
            request.DrivingH,
            request.OffDutyH,
            sourceResult.Value,
            request.EnteredBy,
            request.Note);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/hos/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static Guid? IdempotencyEventId(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        return Guid.TryParse(idempotencyKey.Trim(), out var parsed)
            ? parsed
            : TranscriptionEventIds.ForKey(idempotencyKey);
    }
}

/// <summary>
/// Endpoint metadata marking a route that admits only the driver whose record it names —
/// no dispatch or compliance override. Pinned by the gateway's endpoint metadata tests.
/// </summary>
public sealed class OwnDriverOnlyMetadata;

/// <summary>
/// Request body for <c>POST /api/drivers/{driverId}/hos/duty-events</c>.
/// <see cref="Status"/> is the wire duty string ("Off Duty" / "On Duty" / "Driving" /
/// "Personal Conveyance"); <see cref="Source"/> the wire source string ("Driver App",
/// "ELD transcription", "Manual (paper backup)", "Other carrier (declared)"), defaulting to
/// Manual (paper backup) when omitted. <see cref="EventId"/> is the client's idempotency key
/// (the <c>Idempotency-Key</c> header is the alias). Odometers are km to one decimal; the three
/// <c>pc*</c> attestations are required true to start Personal Conveyance.
/// </summary>
public sealed record DutyEventRequest(
    Guid? EventId,
    DateTimeOffset? AtUtc,
    string? Status,
    string? Source = null,
    string? EnteredBy = null,
    string? CarrierName = null,
    string? Note = null,
    decimal? OdometerStartKm = null,
    decimal? OdometerEndKm = null,
    bool? PcNoPassengersOrCargo = null,
    bool? PcNoTrailer = null,
    bool? PcNoWork = null);

/// <summary>
/// Request body for <c>POST /api/drivers/{driverId}/hos/duty-events/{eventId}/amend</c>: the
/// replacement event (same fields as <see cref="DutyEventRequest"/>) plus the mandatory
/// <see cref="Reason"/>. The replacement must fall on the same local day as the original.
/// </summary>
public sealed record AmendDutyEventRequest(
    Guid? ReplacementEventId,
    DateTimeOffset? AtUtc,
    string? Status,
    string? Reason,
    string? Source = null,
    string? EnteredBy = null,
    string? CarrierName = null,
    string? Note = null,
    decimal? OdometerStartKm = null,
    decimal? OdometerEndKm = null,
    bool? PcNoPassengersOrCargo = null,
    bool? PcNoTrailer = null,
    bool? PcNoWork = null);

/// <summary>
/// Request body for <c>POST /api/drivers/{driverId}/hos/days/{date}/transcribe</c>.
/// <see cref="Source"/> must be "ELD transcription" or "Manual (paper backup)";
/// <see cref="EnteredBy"/> is the dispatcher's name (required); <see cref="Entries"/> are local
/// wall-clock times on that date in increasing order. <see cref="TranscriptionId"/> keys the
/// entries' ids, so resending the same body is a no-op.
/// </summary>
public sealed record TranscribeDutyDayRequest(
    Guid? TranscriptionId,
    string? Source,
    string? EnteredBy,
    IReadOnlyList<TranscriptionEntryRequest>? Entries,
    string? CarrierName = null);

/// <summary>One transcribed line: <see cref="Time"/> is "HH:mm" local, <see cref="Status"/> the wire duty string.</summary>
public sealed record TranscriptionEntryRequest(string? Time, string? Status, string? Note = null);

/// <summary>
/// Request body for <c>POST /api/drivers/{driverId}/hos/exceptions</c>. <see cref="Kind"/> is the
/// wire kind string ("Adverse driving conditions" or "Emergency" — a deferral goes through the
/// compliance route). <see cref="EffectiveFromUtc"/> is when the conditions arose (within the
/// last 24 hours); <see cref="EffectiveToUtc"/> when they ended, if they have. For adverse
/// conditions <see cref="NotKnownBeforeDeparture"/> must be true and
/// <see cref="ExtraDrivingMinutes"/> is required (1–120).
/// </summary>
public sealed record DeclareHosExceptionRequest(
    string? Kind,
    DateTimeOffset? EffectiveFromUtc,
    string? Reason,
    DateTimeOffset? EffectiveToUtc = null,
    string? Location = null,
    Guid? TripId = null,
    bool? NotKnownBeforeDeparture = null,
    int? ExtraDrivingMinutes = null,
    bool? EldRemarkRecorded = null);

/// <summary>Optional body for the withdraw / reject / resolve / lift routes: a note for the record.</summary>
public sealed record ExceptionNoteRequest(string? Note = null);

/// <summary>Request body for <c>POST /api/drivers/{driverId}/hos/eld-malfunctions</c>. <see cref="Code"/> is what the device displayed (≤ 32 chars).</summary>
public sealed record ReportEldMalfunctionRequest(
    string? Code,
    DateTimeOffset? NoticedAtUtc,
    string? Description,
    Guid? VehicleId = null);

/// <summary>
/// Request body for the legacy <c>POST /api/drivers/{driverId}/hos</c> daily totals.
/// <see cref="Duty"/> is the wire duty string. <see cref="Date"/> defaults to today (UTC).
/// <para>
/// <see cref="Source"/> is the wire source string — <c>"Driver App"</c> or
/// <c>"Manual (paper backup)"</c> on this route. <b>Omitting it means Manual (paper backup)</b>,
/// which is what the console sends today; anything else is a 400, never a 500.
/// </para>
/// <para>
/// <see cref="EnteredBy"/> is the dispatcher's name and is <b>required for a Manual entry and
/// ignored for a Driver App one</b> — see <c>HosLogEntry</c>'s remarks.
/// </para>
/// </summary>
public sealed record HosEntryRequest(
    DateOnly? Date,
    string? Duty,
    decimal OnDutyH,
    decimal DrivingH,
    decimal OffDutyH,
    string? EnteredBy,
    string? Note,
    string? Source = null);
