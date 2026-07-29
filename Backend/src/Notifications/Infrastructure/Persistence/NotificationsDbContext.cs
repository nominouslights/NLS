using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Persistence;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Notifications.Application;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Templates;
using NorthernLink.Notifications.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>
/// The Notifications module's DbContext (Postgres schema "notifications"). Tenant stamping,
/// the audit pipeline (event journal + aggregate snapshots + outbox), and aggregate
/// conventions all come from <see cref="ModuleDbContext"/>; this class only maps
/// Notifications' own entities and their query filters. The database half of tenant
/// enforcement (RLS) is enabled in the migrations and keyed on the session variable set by
/// <see cref="TenantSessionInterceptor"/>.
/// </summary>
public sealed class NotificationsDbContext(
    DbContextOptions<NotificationsDbContext> options,
    ITenantContext tenantContext,
    NotificationsIntegrationEventMapper? integrationEventMapper = null)
    : ModuleDbContext(options, NotificationsServiceCollectionExtensions.SchemaName, tenantContext, integrationEventMapper)
{
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    public DbSet<EmailDispatch> EmailDispatches => Set<EmailDispatch>();

    // Read-side projections — ordinary rm_* tables the projection worker upserts into,
    // secured by the same native RLS policy as every other table.
    public DbSet<EmailTemplateReadModel> EmailTemplateReadModels => Set<EmailTemplateReadModel>();

    public DbSet<EmailDispatchReadModel> EmailDispatchReadModels => Set<EmailDispatchReadModel>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EmailTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new EmailDispatchConfiguration());

        modelBuilder.ApplyConfiguration(new EmailTemplateReadModelConfiguration());
        modelBuilder.ApplyConfiguration(new EmailDispatchReadModelConfiguration());

        // Tenant isolation, API half. Never remove: RLS is the backstop, not the substitute.
        modelBuilder.Entity<EmailTemplate>().HasQueryFilter(t => t.TenantId == TenantId);
        modelBuilder.Entity<EmailDispatch>().HasQueryFilter(d => d.TenantId == TenantId);

        // Same tenant filter on the read models — the retained API half of dual enforcement.
        modelBuilder.Entity<EmailTemplateReadModel>().HasQueryFilter(t => t.TenantId == TenantId);
        modelBuilder.Entity<EmailDispatchReadModel>().HasQueryFilter(d => d.TenantId == TenantId);
    }
}
