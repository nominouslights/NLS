// Local dev orchestration: this AppHost starts/stops the same processes and containers a
// developer would otherwise start by hand (Postgres, RabbitMQ, the API, and the Dispatcher dev
// server) from one `dotnet run`, with a live dashboard.
//
// Postgres/RabbitMQ and the API's own config stay "orchestration only" — fixed literal values
// matching what Properties/launchSettings.json also sets for a standalone `dotnet run`, not
// dynamically injected, so behavior is identical whichever way the API is started.
//
// The one deliberate exception is api -> dispatcher: .WithReference(api) makes Aspire inject
// the API's resolved URL into the dispatcher process as `services__api__http__0`, which
// Dispatcher/next.config.ts reads to proxy /api/* server-side. That's what makes the browser
// same-origin against the Dispatcher dev server instead of needing CORS.

var builder = DistributedApplication.CreateBuilder(args);

var postgresUser = builder.AddParameter("postgres-user", "northernlink");
var postgresPassword = builder.AddParameter("postgres-password", "northernlink_dev", secret: true);

var postgres = builder.AddPostgres("postgres", postgresUser, postgresPassword, port: 5432)
    .WithDataVolume("northernlink-postgres-data");
postgres.AddDatabase("northernlink");

var rabbitMqUser = builder.AddParameter("rabbitmq-user", "northernlink");
var rabbitMqPassword = builder.AddParameter("rabbitmq-password", "northernlink_dev", secret: true);

var rabbitmq = builder.AddRabbitMQ("rabbitmq", rabbitMqUser, rabbitMqPassword, port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithDataVolume("northernlink-rabbitmq-data");

var api = builder.AddProject<Projects.NorthernLink_Api>("api")
    .WithEnvironment(
        "ConnectionStrings__Postgres",
        "Host=localhost;Port=5432;Database=northernlink;Username=northernlink;Password=northernlink_dev")
    .WaitFor(postgres)
    .WaitFor(rabbitmq);

// AddNextJsApp is marked experimental (ASPIREJAVASCRIPT001) by Aspire itself. Accepted here
// because this project is local-dev tooling only — never shipped, never touches production.
#pragma warning disable ASPIREJAVASCRIPT001
builder.AddNextJsApp("dispatcher", "../../Dispatcher")
    // targetPort pins the actual `next dev -p <port>` invocation; isProxied: false means the
    // browser hits port 3001 directly instead of relying on DCP's reverse-proxy layer, which
    // matches CLAUDE.md's documented "Dispatcher usually lands on 3001" expectation exactly.
    .WithHttpEndpoint(port: 3001, targetPort: 3001, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints()
    .WithReference(api);

// Public marketing site — no .WithReference(api): it makes no API calls yet (prototype forms).
builder.AddNextJsApp("website", "../../Website")
    .WithHttpEndpoint(port: 3002, targetPort: 3002, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIREJAVASCRIPT001

builder.Build().Run();
