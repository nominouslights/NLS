using NorthernLink.BuildingBlocks.Infrastructure;
using NorthernLink.Modules.Billing.Infrastructure;
using NorthernLink.Modules.Clients.Infrastructure;
using NorthernLink.Modules.Drivers.Infrastructure;
using NorthernLink.Modules.Fleet.Infrastructure;
using NorthernLink.Modules.Grocery.Infrastructure;
using NorthernLink.Modules.Identity.Infrastructure;
using NorthernLink.Modules.Incidents.Infrastructure;
using NorthernLink.Modules.Notifications.Infrastructure;
using NorthernLink.Modules.Trips.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Platform building blocks: command/query dispatcher + RabbitMQ integration event bus.
builder.Services.AddNorthernLinkBuildingBlocks(builder.Configuration);

// Domain modules — one registration call per module, nothing else.
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddTripsModule(builder.Configuration)
    .AddDriversModule(builder.Configuration)
    .AddFleetModule(builder.Configuration)
    .AddClientsModule(builder.Configuration)
    .AddBillingModule(builder.Configuration)
    .AddIncidentsModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddGroceryModule(builder.Configuration);

var app = builder.Build();

// Structure-only scaffold: no endpoints yet. Modules will map their own endpoint
// groups here (e.g. app.MapTripsEndpoints()) when the first real feature lands.

app.Run();
