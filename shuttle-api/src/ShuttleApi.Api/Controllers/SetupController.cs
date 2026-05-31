using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Setup;

namespace ShuttleApi.Api.Controllers;

[AllowAnonymous]
public sealed class SetupController(ISender sender) : BaseApiController(sender)
{
    [HttpGet("api/setup/status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct) =>
        Ok(await Sender.Send(new GetSetupStatusQuery(), ct));

    [HttpPost("api/setup/initialize")]
    public async Task<IActionResult> Initialize([FromBody] InitializeRequest request, CancellationToken ct)
    {
        await Sender.Send(new InitializeSystemCommand(request.Email, request.Password), ct);
        return Ok(new { message = "System initialized successfully. Please log in and change your password." });
    }
}

public sealed record InitializeRequest(string Email, string Password);
