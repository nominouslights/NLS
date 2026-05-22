using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Users;

namespace ShuttleApi.Api.Controllers;

[Authorize]
public sealed class UsersController(ISender sender) : BaseApiController(sender)
{
    [HttpGet]
    [Route("api/users/me")]
    public IActionResult GetMe()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);

        return Ok(new { userId, email, role });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [Route("api/users/pending")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetPendingUsersQuery(), cancellationToken));

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/users/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new ApproveUserCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [Route("api/users/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        await Sender.Send(new RejectUserCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [Route("api/users/admin-only")]
    public IActionResult AdminOnly() =>
        Ok(new { message = "You have Admin access." });
}
