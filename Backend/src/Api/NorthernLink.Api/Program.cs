using System.Text.Json.Serialization;
using NorthernLink.Api.Tenancy;
using NorthernLink.Shared;
using NorthernLink.Api.HostDefaults;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Billing.Infrastructure;
using NorthernLink.Clients.Infrastructure;
using NorthernLink.Drivers.Infrastructure;
using NorthernLink.Fleet.Infrastructure;
using NorthernLink.Fleet.Infrastructure.Endpoints;
using NorthernLink.Grocery.Infrastructure;
using NorthernLink.Identity.Infrastructure;
using NorthernLink.Incidents.Infrastructure;
using NorthernLink.Notifications.Infrastructure;
using NorthernLink.Trips.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Shared platform services: command/query dispatcher + RabbitMQ integration event bus.
builder.Services.AddNorthernLinkShared(builder.Configuration);

// Local-dev-orchestration hooks: service discovery registration + /health + /alive.
builder.AddHostDefaults();

// Enums (VehicleStatus, DisposalMethod, …) travel as strings over the wire.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

if (builder.Environment.IsDevelopment())
{
    // TEMPORARY: dev-only tenant from the X-Tenant-Id header until Identity/OpenIddict
    // issues real token claims. See DevHeaderTenantContext.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ITenantContext, DevHeaderTenantContext>();
}

// Domain libraries — one registration call per library, nothing else.
builder.Services
    .AddIdentity(builder.Configuration)
    .AddTrips(builder.Configuration)
    .AddDrivers(builder.Configuration)
    .AddFleet(builder.Configuration)
    .AddClients(builder.Configuration)
    .AddBilling(builder.Configuration)
    .AddIncidents(builder.Configuration)
    .AddNotifications(builder.Configuration)
    .AddGrocery(builder.Configuration);

var app = builder.Build();

// No CORS policy needed: the Dispatcher dev server proxies /api/* to this API server-side
// (see Dispatcher/next.config.ts) so the browser only ever talks to its own origin.
app.MapDefaultHostEndpoints();

// Domain libraries map their own endpoint groups; the gateway only composes them.
app.MapFleetEndpoints();

if (app.Environment.IsDevelopment())
{
    // Dev-only: apply Fleet migrations and seed the demo vehicles on boot.
    await app.Services.InitializeFleetDatabaseAsync();
}

await app.RunAsync();
