using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Booking.Application.Bookings;
using NorthernLink.Booking.Application.Bookings.Cancel;
using NorthernLink.Booking.Application.Bookings.Confirm;
using NorthernLink.Booking.Application.Bookings.Create;
using NorthernLink.Booking.Application.Bookings.Update;
using NorthernLink.Booking.Application.BookingDays.Guarantee;
using NorthernLink.Booking.Application.Calendar.GetDay;
using NorthernLink.Booking.Application.Calendar.GetMonth;
using NorthernLink.Booking.Application.Corridors.GetCorridors;
using NorthernLink.Booking.Application.Customers.Create;
using NorthernLink.Booking.Application.Customers.GetById;
using NorthernLink.Booking.Application.Customers.Search;
using NorthernLink.Booking.Application.Customers.Update;
using NorthernLink.Booking.Application.Settings.GetSettings;
using NorthernLink.Booking.Application.Settings.SetDayOverrides;
using NorthernLink.Booking.Application.Settings.UpdatePolicy;
using NorthernLink.Booking.Application.Settings.UpsertCorridorSettings;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Infrastructure.Endpoints;

/// <summary>
/// The Booking module's minimal-API surface under <c>/api/booking</c>. The whole group
/// carries the DispatchAccess policy (Owner/Dispatcher/Supervisor — dispatch operates the
/// booking calendar); settings WRITES additionally require AdminOnly per endpoint (Owner —
/// penalties and thresholds are pricing policy, not dispatch controls). Every endpoint
/// resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var booking = app.MapGroup("/api/booking")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        // Customers — the individual-person roster (not Clients-module organizations).
        booking.MapGet("customers", SearchCustomers);
        booking.MapPost("customers", CreateCustomer);
        booking.MapGet("customers/{id:guid}", GetCustomerById);
        booking.MapPut("customers/{id:guid}", UpdateCustomer);

        // Corridors — the replica of Trips routes (empty until routes are re-saved once).
        booking.MapGet("corridors", GetCorridors);

        // Calendar — month summaries + one day's panel detail.
        booking.MapGet("calendar", GetCalendarMonth);
        booking.MapGet("days/{date}", GetDayDetail);

        // Gift-a-Seat: guarantee the day's minimum (re-confirms a Reverted day). Dispatch-wide
        // (the group's DispatchAccess) — covering seats is an operational save-the-trip action,
        // not pricing policy. Idempotent; {id} is the BookingDay id from the calendar responses.
        booking.MapPost("days/{id:guid}/guarantee", GuaranteeDay);

        // Bookings.
        booking.MapPost("bookings", CreateBooking);
        booking.MapPut("bookings/{id:guid}", UpdateBooking);
        booking.MapPost("bookings/{id:guid}/confirm", ConfirmBooking);
        booking.MapPost("bookings/{id:guid}/cancel", CancelBooking);

        // Settings — read is dispatch-wide (the calendar needs the thresholds); writes are
        // Owner-only. AdminOnly stacks ON TOP of the group's DispatchAccess (both must pass).
        booking.MapGet("settings", GetSettings);
        booking.MapPut("settings/policy", UpdatePolicy)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        booking.MapPut("settings/corridors/{corridorId:guid}", UpsertCorridorSettings)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        booking.MapPut("days/{id:guid}/overrides", SetDayOverrides)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return app;
    }

    private static async Task<IResult> SearchCustomers(
        string? search, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new SearchCustomersQuery(tenantId, search), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateCustomer(
        CustomerRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateCustomerCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.Phone,
            request.Email,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/booking/customers/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetCustomerById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetCustomerByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id, CustomerRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateCustomerCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.Phone,
            request.Email,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetCorridors(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetCorridorsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetCalendarMonth(
        int year, int month, Guid corridorId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetBookingCalendarMonthQuery(tenantId, corridorId, year, month), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetDayDetail(
        DateOnly date, Guid corridorId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetBookingDayDetailQuery(tenantId, corridorId, date), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GuaranteeDay(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new GuaranteeBookingDayCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateBooking(
        CreateBookingRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateBookingCommand(
            tenantId,
            request.CustomerId,
            request.CorridorId,
            request.ServiceDate,
            ToInput(request.Pickup),
            ToInput(request.Dropoff),
            ToInputs(request.Passengers),
            request.PaymentMethod,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/booking/bookings/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateBooking(
        Guid id, UpdateBookingRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateBookingCommand(
            tenantId,
            id,
            ToInput(request.Pickup),
            ToInput(request.Dropoff),
            ToInputs(request.Passengers),
            request.PaymentMethod,
            request.PaymentStatus,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ConfirmBooking(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new ConfirmBookingCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CancelBooking(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new CancelBookingCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetSettings(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBookingSettingsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdatePolicy(
        BookingPolicyRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateBookingPolicyCommand(
            tenantId,
            request.CancellationWindowHours,
            request.EarlyCancellationPenaltyCad,
            request.BookingCutoffHours,
            request.SeatHoldMinutes,
            request.DefaultPassengerMinimum,
            request.DefaultSeatCapacity);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpsertCorridorSettings(
        Guid corridorId, CorridorSettingsRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpsertCorridorBookingSettingsCommand(
            tenantId,
            corridorId,
            request.PassengerMinimum,
            request.SeatCapacity);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SetDayOverrides(
        Guid id, DayOverridesRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new SetBookingDayOverridesCommand(
            tenantId,
            id,
            request.PassengerMinimum,
            request.SeatCapacity);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static BookingLocationInput ToInput(BookingLocationRequest? location) =>
        new(location?.StopId, location?.StopName, location?.AddressDetail);

    private static IReadOnlyList<BookingPassengerInput> ToInputs(List<BookingPassengerRequest>? passengers) =>
        passengers is null
            ? []
            : [.. passengers.Select(p => new BookingPassengerInput(p.Name, p.Phone, p.IsBillingCustomer))];
}

/// <summary>Body of a successful create (201, with Location header).</summary>
public sealed record EntityCreatedResponse(Guid Id);

/// <summary>Request body for POST/PUT /api/booking/customers. Name is required.</summary>
public sealed record CustomerRequest(
    string? Name,
    string? Phone,
    string? Email,
    string? Notes);

/// <summary>
/// A pickup/drop-off location: reference a stop (StopId + its StopName snapshot, picked
/// from the existing Trips stops/routes API frontend-side) and/or free-text AddressDetail.
/// A StopId requires a StopName; at least one of StopName/AddressDetail must be present.
/// </summary>
public sealed record BookingLocationRequest(
    Guid? StopId,
    string? StopName,
    string? AddressDetail);

/// <summary>One passenger row. Name required; IsBillingCustomer marks "customer is travelling".</summary>
public sealed record BookingPassengerRequest(
    string? Name,
    string? Phone,
    bool IsBillingCustomer);

/// <summary>
/// Request body for POST /api/booking/bookings. PaymentMethod is the enum name ("Square",
/// "ETransfer", "Cash"); at least one passenger is required; the booking starts Unconfirmed
/// + Unpaid with its seat hold stamped from the tenant policy.
/// </summary>
public sealed record CreateBookingRequest(
    Guid CustomerId,
    Guid CorridorId,
    DateOnly ServiceDate,
    BookingLocationRequest? Pickup,
    BookingLocationRequest? Dropoff,
    List<BookingPassengerRequest>? Passengers,
    PaymentMethod PaymentMethod,
    string? Notes);

/// <summary>
/// Request body for PUT /api/booking/bookings/{id} — full replacement of the editable
/// details (locations, passengers, payment fields, notes). Corridor/date/status are not
/// editable; a Cancelled booking rejects edits with 409.
/// </summary>
public sealed record UpdateBookingRequest(
    BookingLocationRequest? Pickup,
    BookingLocationRequest? Dropoff,
    List<BookingPassengerRequest>? Passengers,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    string? Notes);

/// <summary>Request body for PUT /api/booking/settings/policy (AdminOnly).</summary>
public sealed record BookingPolicyRequest(
    int CancellationWindowHours,
    decimal EarlyCancellationPenaltyCad,
    int BookingCutoffHours,
    int SeatHoldMinutes,
    int DefaultPassengerMinimum,
    int DefaultSeatCapacity);

/// <summary>
/// Request body for PUT /api/booking/settings/corridors/{corridorId} (AdminOnly).
/// Null fields clear that override back to the policy default.
/// </summary>
public sealed record CorridorSettingsRequest(
    int? PassengerMinimum,
    int? SeatCapacity);

/// <summary>
/// Request body for PUT /api/booking/days/{id}/overrides (AdminOnly). {id} is the
/// BookingDay id from the calendar/day responses. Null fields clear that override.
/// </summary>
public sealed record DayOverridesRequest(
    int? PassengerMinimum,
    int? SeatCapacity);
