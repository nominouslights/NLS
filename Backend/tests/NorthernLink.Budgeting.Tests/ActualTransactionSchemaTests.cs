using Microsoft.EntityFrameworkCore;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The one guarantee in this slice that no code path can be asked about, so the schema has to be
/// asked instead: <b>a re-import is idempotent because the database refuses the second row.</b>
/// An aggregate cannot see its siblings, so <c>ActualTransaction.Create</c> will happily build two
/// transactions carrying one QuickBooks id (pinned in
/// <see cref="ActualTransactionTests.The_domain_does_not_dedupe_two_rows_carrying_the_same_external_reference"/>);
/// what stops the second one being stored is the unique
/// (tenant_id, source, external_reference) index. If that index is ever dropped or its filter
/// widened, every figure on the report silently doubles on the next pull, with nothing failing.
/// <para>
/// This is the only test in the project that builds an EF model — the rest of the suite is
/// Domain and Application over in-memory fakes, and Infrastructure is otherwise exercised
/// end-to-end against the real database. Building a model opens no connection, so the dummy
/// connection string below is never dialled.
/// </para>
/// </summary>
public class ActualTransactionSchemaTests
{
    private static BudgetingDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<BudgetingDbContext>()
            .UseNpgsql("Host=model-building-only;Database=none;Username=none;Password=none")
            .Options;

        return new BudgetingDbContext(options, new NoTenantContext());
    }

    [Fact]
    public void The_external_reference_is_unique_per_tenant_and_source_but_only_where_it_is_set()
    {
        using var context = BuildContext();
        var entity = context.Model.FindEntityType(typeof(ActualTransaction))!;

        var index = Assert.Single(entity.GetIndexes(), i => i.IsUnique);

        Assert.Equal(
            ["TenantId", "Source", "ExternalReference"],
            index.Properties.Select(p => p.Name));

        // Scoped per source so a reference typed by hand can never collide with a QuickBooks
        // transaction id that happens to read the same.
        Assert.Contains("Source", index.Properties.Select(p => p.Name));

        // The filter is what lets the thousands of hand-entered rows — every one of them with a
        // null reference — coexist under a unique index.
        Assert.Equal("external_reference IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void The_write_table_carries_the_period_read_path_and_the_code_lookup_indexes()
    {
        using var context = BuildContext();
        var entity = context.Model.FindEntityType(typeof(ActualTransaction))!;

        var columns = entity.GetIndexes()
            .Select(i => string.Join(",", i.Properties.Select(p => p.Name)))
            .ToList();

        // "This period's transactions" is a range scan on occurred_on, because nothing stores a
        // period id — so this index is the period read path, not an optimization.
        Assert.Contains("TenantId,OccurredOn", columns);
        Assert.Contains("TenantId,BudgetCodeId", columns);
    }

    [Fact]
    public void There_is_no_period_column_on_the_table_at_all()
    {
        using var context = BuildContext();
        var entity = context.Model.FindEntityType(typeof(ActualTransaction))!;

        Assert.DoesNotContain(
            entity.GetProperties(),
            p => p.GetColumnName().Contains("period", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Model building needs an ITenantContext; it never reads one.</summary>
    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
