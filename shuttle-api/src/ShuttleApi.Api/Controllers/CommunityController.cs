using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Community.Commands;
using ShuttleApi.Application.Community.Queries;

namespace ShuttleApi.Api.Controllers;

public sealed class CommunityController(ISender sender) : BaseApiController(sender)
{
    [AllowAnonymous]
    [HttpGet("api/community/calendar")]
    public async Task<IActionResult> GetCalendar(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetCommunityCalendarQuery(IsAdmin: false), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("api/community/calendar/admin")]
    public async Task<IActionResult> GetCalendarAdmin(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetCommunityCalendarQuery(IsAdmin: true), cancellationToken));

    [AllowAnonymous]
    [HttpPost("api/community/bookings")]
    public async Task<IActionResult> BookSeat(
        [FromBody] BookSeatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new BookSeatCommand(
            request.Date,
            request.Direction,
            request.TripType,
            request.Destination,
            request.FullName,
            request.Phone,
            request.Email),
            cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("api/community/bookings/{reference}")]
    public async Task<IActionResult> GetBooking(string reference, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetBookingByReferenceQuery(reference), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("api/community/calendar/blocks")]
    public async Task<IActionResult> BlockDay(
        [FromBody] BlockDayRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new BlockCalendarDayCommand(request.Date, request.Reason), cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("api/community/calendar/blocks/{date}")]
    public async Task<IActionResult> UnblockDay(DateOnly date, CancellationToken cancellationToken)
    {
        await Sender.Send(new UnblockCalendarDayCommand(date), cancellationToken);
        return NoContent();
    }
}

public sealed record BookSeatRequest(
    DateOnly Date,
    string Direction,
    string TripType,
    string Destination,
    string FullName,
    string Phone,
    string Email);

public sealed record BlockDayRequest(DateOnly Date, string Reason);
