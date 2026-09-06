using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` (migrations only — never used at runtime).
/// Design-time commands run outside the host's configuration pipeline, so this reads
/// ConnectionStrings__Postgres from the environment directly — same as the runtime
/// registration in BookingServiceCollectionExtensions — rather than hardcoding a value.
/// Export it in your shell before running `dotnet ef` (see CLAUDE.md).
/// </summary>
public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(
                RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BookingServiceCollectionExtensions.SchemaName))
            .Options;

        return new BookingDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
