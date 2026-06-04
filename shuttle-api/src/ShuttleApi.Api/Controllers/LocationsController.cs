using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Locations;

namespace ShuttleApi.Api.Controllers;

[Authorize]
public sealed class LocationsController(ISender sender) : BaseApiController(sender)
{
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [Route("api/locations/archived")]
    public async Task<IActionResult> GetArchived(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetArchivedLocationsQuery(), cancellationToken));

    [HttpGet]
    [Route("api/locations")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetLocationsQuery(), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/locations")]
    public async Task<IActionResult> Create(
        [FromBody] CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new CreateLocationCommand(request.Name, request.Address, request.Latitude, request.Longitude),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    [Route("api/locations/{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLocationRequest request,
        CancellationToken cancellationToken)
    {
        await Sender.Send(
            new UpdateLocationCommand(id, request.Name, request.Address, request.Latitude, request.Longitude),
            cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete]
    [Route("api/locations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteLocationCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/locations/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new RestoreLocationCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateLocationRequest(
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude);

public sealed record UpdateLocationRequest(
    string Name,
    string? Address,
    double? Latitude,
    double? Longitude);
