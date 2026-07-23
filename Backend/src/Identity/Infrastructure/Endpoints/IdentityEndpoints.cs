using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Identity.Application.Auth.BootstrapAdmin;
using NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;
using NorthernLink.Identity.Application.Auth.Login;
using NorthernLink.Identity.Application.Auth.Logout;
using NorthernLink.Identity.Application.Auth.Refresh;
using NorthernLink.Identity.Application.Auth.Setup;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Identity.Infrastructure.Endpoints;

/// <summary>
/// The Identity module's minimal-API surface under <c>/api/identity</c>. Login, refresh,
/// logout, and admin bootstrap are anonymous by design (that's the whole point — they run
/// before a caller has a token); minting a new bootstrap token requires an existing Admin
/// session.
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/identity/auth");
        auth.MapPost("login", Login);
        auth.MapPost("refresh", Refresh);
        auth.MapPost("logout", Logout);

        // First-run setup — anonymous, and self-closing once any user exists. The status check
        // drives whether the console shows the create-admin screen; setup creates the first admin.
        auth.MapGet("setup-status", SetupStatus);
        auth.MapPost("setup", CreateFirstAdmin);

        var admin = app.MapGroup("/api/identity/admin");
        admin.MapPost("bootstrap", BootstrapAdminAccount);
        admin.MapPost("bootstrap-token", GenerateBootstrapToken).RequireAuthorization("AdminOnly");

        return app;
    }

    private static async Task<IResult> Login(LoginRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> Refresh(RefreshRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RefreshTokenCommand(request.RefreshToken ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> Logout(RefreshRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new LogoutCommand(request.RefreshToken ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SetupStatus(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Query(new GetSetupStatusQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateFirstAdmin(SetupRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateFirstAdminCommand(request.Email ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> BootstrapAdminAccount(
        BootstrapAdminRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new BootstrapAdminCommand(
                request.Token ?? string.Empty,
                request.Email ?? string.Empty,
                request.Password ?? string.Empty),
            cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/identity/admin/users/{result.Value}", new BootstrapAdminResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GenerateBootstrapToken(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new GenerateBootstrapTokenCommand(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Request body for POST /api/identity/auth/login.</summary>
public sealed record LoginRequest(string? Email, string? Password);

/// <summary>Request body for POST /api/identity/auth/refresh and /api/identity/auth/logout.</summary>
public sealed record RefreshRequest(string? RefreshToken);

/// <summary>Request body for POST /api/identity/auth/setup (first-run create-admin).</summary>
public sealed record SetupRequest(string? Email, string? Password);

/// <summary>Request body for POST /api/identity/admin/bootstrap.</summary>
public sealed record BootstrapAdminRequest(string? Token, string? Email, string? Password);

/// <summary>Body of a successful admin bootstrap (201, with Location header).</summary>
public sealed record BootstrapAdminResponse(Guid UserId);
