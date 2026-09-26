using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NorthernLink.Api.Auth;
using NorthernLink.Api.Diagnostics;
using NorthernLink.Api.Tenancy;
using NorthernLink.Shared;
using NorthernLink.Api.HostDefaults;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Migrations;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Billing.Infrastructure;
using NorthernLink.Billing.Infrastructure.Endpoints;
using NorthernLink.Billing.Infrastructure.Persistence;
using NorthernLink.Booking.Infrastructure;
using NorthernLink.Booking.Infrastructure.Endpoints;
using NorthernLink.Booking.Infrastructure.Persistence;
using NorthernLink.Budgeting.Infrastructure;
using NorthernLink.Budgeting.Infrastructure.Endpoints;
using NorthernLink.Budgeting.Infrastructure.Persistence;
using NorthernLink.Clients.Infrastructure;
using NorthernLink.Clients.Infrastructure.Endpoints;
using NorthernLink.Clients.Infrastructure.Persistence;
using NorthernLink.Drivers.Infrastructure;
using NorthernLink.Drivers.Infrastructure.Endpoints;
using NorthernLink.Drivers.Infrastructure.Persistence;
using NorthernLink.Fleet.Infrastructure;
using NorthernLink.Fleet.Infrastructure.Endpoints;
using NorthernLink.Fleet.Infrastructure.Persistence;
using NorthernLink.Grocery.Infrastructure;
using NorthernLink.Identity.Infrastructure;
using NorthernLink.Identity.Infrastructure.Auth;
using NorthernLink.Identity.Infrastructure.Endpoints;
using NorthernLink.Identity.Infrastructure.Persistence;
using NorthernLink.Incidents.Infrastructure;
using NorthernLink.Notifications.Infrastructure;
using NorthernLink.Notifications.Infrastructure.Endpoints;
using NorthernLink.Notifications.Infrastructure.Persistence;
using NorthernLink.Trips.Infrastructure;
using NorthernLink.Trips.Infrastructure.Endpoints;
using NorthernLink.Trips.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Shared platform services: command/query dispatcher + RabbitMQ integration event bus.
builder.Services.AddNorthernLinkShared(builder.Configuration);

// Local-dev-orchestration hooks: service discovery registration + /health + /alive.
builder.AddHostDefaults();

// Enums (VehicleStatus, DisposalMethod, …) travel as strings over the wire.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Real tenant resolution from JWT claims — replaces the old dev-only X-Tenant-Id header
// (DevHeaderTenantContext) now that Identity issues real tokens. Unconditional: this is the
// real mechanism, not a dev hack.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, JwtTenantContext>();

// The "who" beside the "which tenant" — read from the signed token, never from a request body,
// so an audit column cannot be forged by its own caller. See ICurrentActor.
builder.Services.AddScoped<ICurrentActor, JwtCurrentActor>();

var jwtSigningKey = RequiredEnvironmentVariable.Get("Identity__JwtSigningKey");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim types literal ("role", "sub", "tenant_id") — the default legacy inbound
        // mapping would rename "role" to ClaimTypes.Role's URI before RoleClaimType below is
        // consulted, silently breaking RequireRole("Admin") with a 403.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            // Must match the literal claim type JwtAccessTokenIssuer stamps ("role"), not
            // the ClaimTypes.Role URI default — see that class's doc comment for why.
            RoleClaimType = JwtAccessTokenIssuer.RoleClaimType,
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicyRegistration.Add);

// Unhandled exceptions: logged in full server-side, answered with a traceId and nothing else.
// See UnhandledExceptionHandler for why the response stays vague on a public-reachable API.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

// WHY appsettings.json raises the EF Core log categories to Warning, since the file cannot say so
// itself: every key under Logging:LogLevel is parsed as a category->level mapping, so a "//"
// comment key there throws at startup ("Configuration value ... is not supported") and takes the
// whole API down. Learned the hard way.
//
// EF Core logs every SQL statement at Information. On this API that is thousands of lines an hour
// from the outbox dispatchers alone, and it buried the one stack trace that mattered whenever a
// request failed — a 500 was undiagnosable without reading past the traffic. The log now carries
// problems, not traffic.
//
// To read SQL again while debugging a query, set Microsoft.EntityFrameworkCore.Database.Command
// back to Information in appsettings.Development.json ONLY — never in appsettings.json, because
// EF logs parameter VALUES at that level and those carry personal data (passenger names, phone
// numbers, addresses) into whatever aggregates the production logs.
//
// Migrations stays at Information deliberately: "Applying migration X" is a handful of lines per
// deploy and is exactly what you want in the log when the schema moves under a running API.

// Domain libraries — one registration call per library, nothing else.
builder.Services
    .AddIdentity(builder.Configuration)
    .AddTrips(builder.Configuration)
    .AddDrivers(builder.Configuration)
    .AddFleet(builder.Configuration)
    .AddClients(builder.Configuration)
    .AddBilling(builder.Configuration)
    .AddBooking(builder.Configuration)
    .AddBudgeting(builder.Configuration)
    .AddIncidents(builder.Configuration)
    .AddNotifications(builder.Configuration)
    .AddGrocery(builder.Configuration);

// Schema migrations, applied under one advisory lock before any module's outbox dispatcher
// starts polling. Registered here rather than per-module: which schemas exist and in what order
// they migrate is a host concern, and hosted services start in registration order. Off unless
// Migrations:RunOnStartup says otherwise — see MigrationOptions for why the default is false.
builder.Services.AddModuleMigrations(
    builder.Configuration,
    typeof(IdentityDbContext),
    typeof(TripsDbContext),
    typeof(DriversDbContext),
    typeof(FleetDbContext),
    typeof(ClientsDbContext),
    typeof(BillingDbContext),
    typeof(BookingDbContext),
    typeof(BudgetingDbContext),
    typeof(NotificationsDbContext));

var app = builder.Build();

// FIRST in the pipeline, deliberately: anything that throws after this line is caught, logged
// with its route and its Postgres SqlState, and answered with a traceId. Registered before the
// endpoints so a failure inside authentication or authorization is caught too.
app.UseExceptionHandler();

// No CORS policy needed: the Dispatcher dev server proxies /api/* to this API server-side
// (see Dispatcher/next.config.ts) so the browser only ever talks to its own origin.
app.MapDefaultHostEndpoints();

app.UseAuthentication();
app.UseAuthorization();

// Domain libraries map their own endpoint groups; the gateway only composes them.
app.MapIdentityEndpoints();
app.MapFleetEndpoints();
app.MapTripsEndpoints();
app.MapDriversEndpoints();
app.MapClientsEndpoints();
app.MapBillingEndpoints();
app.MapBookingEndpoints();
app.MapBudgetingEndpoints();
app.MapNotificationsEndpoints();

await app.RunAsync();
