using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Auth;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Api.Controllers;

public sealed class AuthController(ISender sender) : BaseApiController(sender)
{
    [AllowAnonymous]
    [HttpPost]
    [Route("api/auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request) =>
        Ok(await Sender.Send(new LoginCommand(request.Email, request.Password)));

    [AllowAnonymous]
    [HttpPost]
    [Route("api/auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(new { error = $"Invalid role. Valid values: {string.Join(", ", Enum.GetNames<UserRole>())}" });

        if (role == UserRole.Admin && (User.Identity?.IsAuthenticated != true || !User.IsInRole("Admin")))
            return StatusCode(403, new { error = "Only admins can create admin accounts." });

        return Ok(await Sender.Send(new RegisterCommand(request.Email, request.Password, role)));
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("api/auth/refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request) =>
        Ok(await Sender.Send(new RefreshTokenCommand(request.RefreshToken)));

    [Authorize]
    [HttpPost]
    [Route("api/auth/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Sender.Send(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string Email, string Password, string Role = "Client");
public sealed record RefreshRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
