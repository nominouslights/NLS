using Microsoft.EntityFrameworkCore;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Identity.Infrastructure.Persistence.Configurations;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Identity.Infrastructure.Persistence;

/// <summary>
/// The Identity module's DbContext (Postgres schema "identity"). Tenant stamping, the audit
/// pipeline, and aggregate conventions all come from <see cref="ModuleDbContext"/>; this
/// class only maps Identity's own entities and their query filters. The database half of
/// tenant enforcement (RLS) is enabled in the migrations, keyed on the session variable set
/// by <see cref="TenantSessionInterceptor"/> — with an <c>app.is_system</c> escape hatch
/// (see the migration) for the anonymous login/refresh/bootstrap flows, which by definition
/// run before any tenant is known from a token.
/// </summary>
public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    ITenantContext tenantContext)
    : ModuleDbContext(options, IdentityServiceCollectionExtensions.SchemaName, tenantContext)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AdminBootstrapToken> AdminBootstrapTokens => Set<AdminBootstrapToken>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new AdminBootstrapTokenConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        // RefreshToken is deliberately not filtered — it isn't ITenantScoped (see its doc
        // comment): scoping flows through the User it belongs to, not duplicated here.
        modelBuilder.Entity<User>().HasQueryFilter(u => u.TenantId == TenantId);
        modelBuilder.Entity<AdminBootstrapToken>().HasQueryFilter(t => t.TenantId == TenantId);
    }
}
