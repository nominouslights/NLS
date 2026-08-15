using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NorthernLink.Api.Auth;
using NorthernLink.Api.Tenancy;
using NorthernLink.Shared;
using NorthernLink.Api.HostDefaults;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Migrations;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Billing.Infrastructure;
using NorthernLink.Billing.Infrastructure.Endpoints;
using NorthernLink.Billing.Infrastructure.Persistence;
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

// Domain libraries — one registration call per library, nothing else.
builder.Services
    .AddIdentity(builder.Configuration)
    .AddTrips(builder.Configuration)
    .AddDrivers(builder.Configuration)
    .AddFleet(builder.Configuration)
    .AddClients(builder.Configuration)
    .AddBilling(builder.Configuration)
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
    typeof(BudgetingDbContext),
    typeof(NotificationsDbContext));

var app = builder.Build();

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
app.MapBudgetingEndpoints();
app.MapNotificationsEndpoints();

await app.RunAsync();
