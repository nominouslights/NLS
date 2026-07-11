using Microsoft.EntityFrameworkCore;

namespace NorthernLink.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for a domain module. Each module owns a dedicated Postgres schema
/// (the module name, lowercase) inside the single shared database — the modular-monolith
/// equivalent of database-per-service. Modules never join across schemas; cross-module
/// data flows through Contracts + integration events instead.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    /// <summary>The Postgres schema this module's tables live in.</summary>
    protected string Schema { get; } = schema;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
