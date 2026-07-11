#!/usr/bin/env bash
# Generates one Northern Link domain module (4 projects: Domain, Application,
# Infrastructure, Contracts) with the platform's enforced reference rules, and adds
# the projects to the solution. Usage: ./tools/new-module.sh <ModuleName>
# Run from the Backend/ directory. Idempotent-ish: refuses to overwrite an existing module.
set -euo pipefail

NAME="${1:?Usage: ./tools/new-module.sh <ModuleName> (PascalCase, e.g. Trips)}"
LOWER="$(echo "$NAME" | tr '[:upper:]' '[:lower:]')"
BASE="src/Modules/$NAME"

if [ -d "$BASE" ]; then
  echo "Module $NAME already exists at $BASE — refusing to overwrite." >&2
  exit 1
fi

mkdir -p \
  "$BASE/NorthernLink.Modules.$NAME.Domain" \
  "$BASE/NorthernLink.Modules.$NAME.Application" \
  "$BASE/NorthernLink.Modules.$NAME.Infrastructure" \
  "$BASE/NorthernLink.Modules.$NAME.Contracts"

# --- Contracts: references NOTHING. The module's public surface. ---------------------
cat > "$BASE/NorthernLink.Modules.$NAME.Contracts/NorthernLink.Modules.$NAME.Contracts.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Contracts are this module's public API surface: integration events + DTOs only.
       Deliberately dependency-free — other modules may reference THIS project and
       nothing else from the $NAME module. Enforced by architecture tests. -->

</Project>
EOF

cat > "$BASE/NorthernLink.Modules.$NAME.Contracts/AssemblyInfo.cs" <<EOF
// NorthernLink.Modules.$NAME.Contracts
//
// Public surface of the $NAME module: integration events and cross-module DTOs only.
// Keep types here small, serializable, and stable — every other module is allowed to
// depend on them. Integration event names should end in "IntegrationEvent" so the
// routing key convention ($LOWER.<event-name>) derives cleanly.
EOF

# --- Domain: references SharedKernel ONLY. -------------------------------------------
cat > "$BASE/NorthernLink.Modules.$NAME.Domain/NorthernLink.Modules.$NAME.Domain.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Domain layer: aggregates, entities, value objects, domain events.
       May reference SharedKernel only — no infrastructure, no other modules. -->

  <ItemGroup>
    <ProjectReference Include="..\\..\\..\\BuildingBlocks\\NorthernLink.SharedKernel\\NorthernLink.SharedKernel.csproj" />
  </ItemGroup>

</Project>
EOF

# --- Application: own Domain + BuildingBlocks.Application (+ other modules' Contracts). ---
cat > "$BASE/NorthernLink.Modules.$NAME.Application/NorthernLink.Modules.$NAME.Application.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Application layer: commands, queries, handlers.
       May reference: own Domain, own Contracts, BuildingBlocks.Application,
       and OTHER modules' Contracts projects only (never their internals). -->

  <ItemGroup>
    <ProjectReference Include="..\\NorthernLink.Modules.$NAME.Domain\\NorthernLink.Modules.$NAME.Domain.csproj" />
    <ProjectReference Include="..\\NorthernLink.Modules.$NAME.Contracts\\NorthernLink.Modules.$NAME.Contracts.csproj" />
    <ProjectReference Include="..\\..\\..\\BuildingBlocks\\NorthernLink.BuildingBlocks.Application\\NorthernLink.BuildingBlocks.Application.csproj" />
  </ItemGroup>

</Project>
EOF

# --- Infrastructure: own Application + BuildingBlocks.Infrastructure. -----------------
cat > "$BASE/NorthernLink.Modules.$NAME.Infrastructure/NorthernLink.Modules.$NAME.Infrastructure.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Infrastructure layer: EF Core DbContext (schema "$LOWER"), repositories,
       external service adapters, and the module's DI registration entry point.
       May reference: own Application + BuildingBlocks.Infrastructure. -->

  <ItemGroup>
    <ProjectReference Include="..\\NorthernLink.Modules.$NAME.Application\\NorthernLink.Modules.$NAME.Application.csproj" />
    <ProjectReference Include="..\\..\\..\\BuildingBlocks\\NorthernLink.BuildingBlocks.Infrastructure\\NorthernLink.BuildingBlocks.Infrastructure.csproj" />
  </ItemGroup>

</Project>
EOF

cat > "$BASE/NorthernLink.Modules.$NAME.Infrastructure/${NAME}Module.cs" <<EOF
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NorthernLink.Modules.$NAME.Infrastructure;

/// <summary>
/// DI entry point for the $NAME module — the only thing the API host sees.
/// Handlers, the module DbContext (Postgres schema "$LOWER"), and integration event
/// consumers register here as they are built.
/// </summary>
public static class ${NAME}Module
{
    public const string SchemaName = "$LOWER";

    public static IServiceCollection Add${NAME}Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Structure-only scaffold: no services yet. Registration order inside a module:
        //   1. DbContext (ModuleDbContext base, schema "$LOWER")
        //   2. Command/query handlers
        //   3. Integration event consumers
        return services;
    }
}
EOF

echo "Module $NAME created."

# --- Add to solution if dotnet + slnx are available -----------------------------------
if [ -f "NorthernLink.slnx" ]; then
  dotnet sln NorthernLink.slnx add --solution-folder "Modules/$NAME" \
    "$BASE/NorthernLink.Modules.$NAME.Domain" \
    "$BASE/NorthernLink.Modules.$NAME.Application" \
    "$BASE/NorthernLink.Modules.$NAME.Infrastructure" \
    "$BASE/NorthernLink.Modules.$NAME.Contracts"
fi
