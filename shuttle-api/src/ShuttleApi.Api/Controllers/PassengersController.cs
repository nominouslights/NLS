using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Passengers.Queries;

namespace ShuttleApi.Api.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class PassengersController(ISender sender) : BaseApiController(sender)
{
    /// <summary>
    /// Search passenger profiles for a client. Used for autocomplete when adding
    /// passengers to a charter trip manifest. Scoped to the client to avoid
    /// cross-company name suggestions.
    /// </summary>
    [HttpGet]
    [Route("api/passengers")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid clientId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var results = await Sender.Send(
            new SearchPassengerProfilesQuery(clientId, search ?? string.Empty),
            cancellationToken);
        return Ok(results);
    }
}
