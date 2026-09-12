using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Identity.Infrastructure.Auth;
using NorthernLink.Identity.Application.Auth.BootstrapAdmin;
using NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;
using NorthernLink.Identity.Application.Auth.Login;
using NorthernLink.Identity.Application.Auth.Logout;
using NorthernLink.Identity.Application.Auth.Refresh;
using NorthernLink.Identity.Application.Auth.Setup;
using NorthernLink.Identity.Application.Profile.GetProfile;
using NorthernLink.Identity.Application.Profile.UpdateProfile;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Identity.Infrastructure.Endpoints;

/// <summary>
/// The Identity module's minimal-API surface under <c>/api/identity</c>. Login, refresh,
/// logout, and admin bootstrap are anonymous by design (that's the whole point — they run
/// before a caller has a token); minting a new bootstrap token requires an existing Owner
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

        // Any authenticated caller, whatever their role — a client needs to be able to read its
        // own role in order to render the right thing, including "you may not be here".
        auth.MapGet("me", Me).RequireAuthorization();

        // Self-service, so authenticated-but-unpolicied: every role owns a profile, and gating
        // this on BudgetAccess would refuse the Dispatch Console the day it grows the same
        // screen, for no security gain. What bounds the caller is that the user id comes from
        // the token's sub claim and is never accepted from the route or body.
        auth.MapGet("profile", GetProfile).RequireAuthorization();
        auth.MapPut("profile", UpdateProfile).RequireAuthorization();

        // First-run setup — anonymous, and self-closing once any user exists. The status check
        // drives whether the console shows the create-admin screen; setup creates the first admin.
        auth.MapGet("setup-status", SetupStatus);
        auth.MapPost("setup", CreateFirstAdmin);

        var admin = app.MapGroup("/api/identity/admin");
        admin.MapPost("bootstrap", BootstrapAdminAccount);
        admin.MapPost("bootstrap-token", GenerateBootstrapToken)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

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

    /// <summary>
    /// The signed-in caller's own claims, straight off the validated principal — no database
    /// read. Clients decode the JWT themselves for immediate rendering, but that decode is
    /// unverified by construction; this is the server-confirmed answer.
    /// </summary>
    private static IResult Me(ClaimsPrincipal principal) =>
        Results.Ok(new MeResponse(
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty,
            principal.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty,
            principal.FindFirstValue(JwtAccessTokenIssuer.RoleClaimType) ?? string.Empty,
            principal.FindFirstValue(JwtAccessTokenIssuer.TenantIdClaimType) ?? string.Empty,
            principal.FindFirstValue(JwtAccessTokenIssuer.TenantTypeClaimType) ?? string.Empty));

    /// <summary>
    /// The caller's own profile, read from the database — unlike <see cref="Me"/>, which answers
    /// from claims alone. A screen about account facts should not render a snapshot that can be
    /// fifteen minutes old.
    /// </summary>
    private static async Task<IResult> GetProfile(
        ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        // Not ceremony despite RequireAuthorization: JwtCurrentActor yields null when the sub
        // claim is absent or unparseable, which authentication alone does not rule out.
        if (currentActor.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetMyProfileQuery(userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileRequest request,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (currentActor.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new UpdateMyProfileCommand(userId, request.FullName, request.JobTitle),
            cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GenerateBootstrapToken(
        ITenantContext tenantContext, ISender sender, string? role, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // A query parameter, deliberately, not a request body: Dispatcher's generateAdminInvite()
        // POSTs here with no body but a Content-Type of application/json, so a record body
        // parameter would fail to bind and 400 the existing client. Defaulting to Owner keeps
        // that call meaning exactly what it meant before roles existed.
        var result = await sender.Send(
            new GenerateBootstrapTokenCommand(tenantId, role ?? Roles.Owner), cancellationToken);

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

/// <summary>
/// Request body for PUT /api/identity/auth/profile. Both members are the complete new value:
/// null clears the field rather than leaving it alone, which is what makes PUT the honest verb
/// here. Nullable (rather than required) so a client omitting a member binds to null instead of
/// failing at the model binder, matching the other request records in this file.
/// </summary>
public sealed record UpdateProfileRequest(string? FullName, string? JobTitle);

/// <summary>Response body for GET /api/identity/auth/me — the caller's own token claims.</summary>
public sealed record MeResponse(
    string UserId, string Email, string Role, string TenantId, string TenantType);
