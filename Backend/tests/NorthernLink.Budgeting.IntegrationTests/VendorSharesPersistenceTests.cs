using NorthernLink.Budgeting.Application.Allocations;
using NorthernLink.Budgeting.Application.Allocations.CopyFromPeriod;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Domain.Vendors;
using NorthernLink.Budgeting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NorthernLink.Budgeting.IntegrationTests;

/// <summary>
/// Vendor shares through REAL EF Core on Postgres — what the in-memory fakes cannot see. EF tracks
/// an owned JSON entity by reference, so a share instance handed from a tracked item to a second
/// owner (a copied item, a read-model row) is one dependent with two owners and the save throws.
/// These tests run the copy handler on real repositories in one tracked unit of work, then the
/// projection worker's tracked ApplyAsync path, then both read services (the owned-JSON projection
/// of the rollup and the shared vendor resolution).
/// <para>
/// Each test uses its own tenant, so periods (unique per tenant) never collide across tests.
/// </para>
/// </summary>
[Collection("postgres")]
public class VendorSharesPersistenceTests(PostgresFixture fixture)
{
    private const string Justification = "Argued from zero for this period.";

    private static BudgetPeriod Period(Guid tenant, int ordinal) =>
        BudgetPeriod.Create(tenant, PeriodGranularity.Quarter, 2026, ordinal).Value;

    private static BudgetCode Code(Guid tenant, Guid periodId, string code, BudgetCodeCategory category) =>
        BudgetCode.Create(
            tenant,
            periodId,
            code,
            new BudgetCodeDetails
            {
                Name = code,
                Category = category,
                ServiceLine = category == BudgetCodeCategory.Revenue ? BudgetServiceLine.ContractCrew : null,
            },
            actorId: null).Value;

    private static Vendor NewVendor(Guid tenant, string name) =>
        Vendor.Create(tenant, new VendorDetails { Name = name }, actorId: null).Value;

    private static BudgetItemDetails Item(
        decimal amount, string? vendor = null, params BudgetItemVendorShareDetails[] shares) => new()
        {
            Title = "Planned item",
            AmountCad = amount,
            Justification = Justification,
            Vendor = vendor,
            VendorShares = shares,
        };

    private sealed record Seeded(
        Guid Tenant, BudgetPeriod Source, BudgetPeriod Target, Vendor Kal, Vendor Petro,
        BudgetAllocation Split, BudgetAllocation Payer);

    /// <summary>
    /// A source period with an expense item split across two vendors and a revenue item with a
    /// payer, and an empty Draft target whose chart has the same strings.
    /// </summary>
    private async Task<Seeded> SeedAsync()
    {
        var tenant = Guid.NewGuid();
        var source = Period(tenant, 1);
        var target = Period(tenant, 2);
        var fleetSource = Code(tenant, source.Id, "FLEET-MAINT", BudgetCodeCategory.Expense);
        var crewSource = Code(tenant, source.Id, "ZBB-CREW-01", BudgetCodeCategory.Revenue);
        var kal = NewVendor(tenant, "Kal Tire Thompson");
        var petro = NewVendor(tenant, "Petro-Canada");

        var split = BudgetAllocation.Create(
            tenant, source.Id, fleetSource.Id, fleetSource.Code, BudgetCodeCategory.Expense,
            Item(1000.10m, null, new(kal.Id, 600.05m), new(petro.Id, 400.05m)), actorId: null).Value;
        var payer = BudgetAllocation.Create(
            tenant, source.Id, crewSource.Id, crewSource.Code, BudgetCodeCategory.Revenue,
            Item(5000m, "Alamos Gold"), actorId: null).Value;

        await using var context = fixture.CreateContext(tenant);
        context.BudgetPeriods.AddRange(source, target);
        context.BudgetCodes.AddRange(
            fleetSource,
            crewSource,
            Code(tenant, target.Id, "FLEET-MAINT", BudgetCodeCategory.Expense),
            Code(tenant, target.Id, "ZBB-CREW-01", BudgetCodeCategory.Revenue));
        context.Vendors.AddRange(kal, petro);
        context.BudgetAllocations.AddRange(split, payer);
        await context.SaveChangesAsync();

        return new Seeded(tenant, source, target, kal, petro, split, payer);
    }

    private static CopyBudgetAllocationsCommandHandler CopyHandler(BudgetingDbContext context) =>
        new(
            new BudgetAllocationRepository(context),
            new BudgetPeriodRepository(context),
            new BudgetCodeRepository(context),
            new VendorRepository(context));

    [Fact]
    public async Task Copying_a_plan_with_vendor_shares_saves_through_real_EF_and_leaves_the_source_intact()
    {
        var seeded = await SeedAsync();

        // One tracked unit of work holds the source items AND the copies — exactly the request
        // scope the API runs the handler in. Sharing a share instance here threw on SaveChanges.
        await using (var context = fixture.CreateContext(seeded.Tenant))
        {
            var result = await CopyHandler(context).Handle(
                new CopyBudgetAllocationsCommand(seeded.Tenant, seeded.Target.Id, seeded.Source.Id, ActorId: null),
                CancellationToken.None);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            Assert.Equal(new BudgetAllocationCopyResult(2, 0, 0, 2), result.Value);

            var tracked = context.ChangeTracker.Entries<BudgetAllocation>().Select(e => e.Entity).ToList();
            var sourceShares = tracked.Single(a => a.Id == seeded.Split.Id).VendorShares;
            var copyShares = tracked.Single(a => a.PeriodId == seeded.Target.Id && a.Code == "FLEET-MAINT").VendorShares;
            Assert.All(copyShares, share => Assert.DoesNotContain(sourceShares, s => ReferenceEquals(s, share)));
        }

        await using var reader = fixture.CreateContext(seeded.Tenant);
        var copies = await reader.BudgetAllocations.AsNoTracking()
            .Where(a => a.PeriodId == seeded.Target.Id).ToListAsync();
        var fleet = Assert.Single(copies, a => a.Code == "FLEET-MAINT");
        Assert.Equal(
            [(seeded.Kal.Id, 600.05m), (seeded.Petro.Id, 400.05m)],
            fleet.VendorShares.Select(s => (s.VendorId, s.AmountCad)).ToArray());
        Assert.True(fleet.NeedsJustification);
        Assert.Equal("Alamos Gold", Assert.Single(copies, a => a.Code == "ZBB-CREW-01").Vendor);

        var source = await reader.BudgetAllocations.AsNoTracking().SingleAsync(a => a.Id == seeded.Split.Id);
        Assert.Equal(
            [(seeded.Kal.Id, 600.05m), (seeded.Petro.Id, 400.05m)],
            source.VendorShares.Select(s => (s.VendorId, s.AmountCad)).ToArray());
    }

    [Fact]
    public async Task The_projection_worker_maps_shares_onto_read_rows_and_both_read_services_resolve_vendors()
    {
        var seeded = await SeedAsync();

        await using (var context = fixture.CreateContext(seeded.Tenant))
        {
            Assert.True((await CopyHandler(context).Handle(
                new CopyBudgetAllocationsCommand(seeded.Tenant, seeded.Target.Id, seeded.Source.Id, ActorId: null),
                CancellationToken.None)).IsSuccess);
        }

        // ApplyAsync: the write-side item is loaded TRACKED and mapped onto a new read row in the
        // same context, so this is the second place a shared share instance would throw.
        await PostgresFixture.BuildProjectionWorker(fixture.AppConnectionString).ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(seeded.Tenant);

        var items = await new BudgetAllocationReadService(reader).GetForPeriodAsync(seeded.Target.Id);
        var fleet = Assert.Single(items, i => i.Code == "FLEET-MAINT");
        Assert.Equal(
            [("Kal Tire Thompson", true, 600.05m), ("Petro-Canada", true, 400.05m)],
            fleet.VendorShares.Select(s => (s.VendorName, s.IsActive, s.AmountCad)).ToArray());

        // The rollup's narrow projection (BudgetCodeId, AmountCad, VendorShares) translates and
        // reads the owned JSON back.
        var rollup = await new VendorReadService(reader).GetPlannedRollupAsync(seeded.Target.Id);
        Assert.Equal(1000.10m, rollup.TotalPlannedExpenseCad);
        Assert.Equal(
            [("Kal Tire Thompson", 1, 600.05m), ("Petro-Canada", 1, 400.05m)],
            rollup.Vendors.OrderBy(v => v.Name).Select(v => (v.Name, v.ItemCount, v.PlannedCad)).ToArray());
        Assert.Equal(0, rollup.NoVendor.ItemCount);
    }

    [Fact]
    public async Task A_period_with_no_shares_reads_an_empty_rollup_without_a_vendor_query()
    {
        var tenant = Guid.NewGuid();
        var period = Period(tenant, 3);
        var code = Code(tenant, period.Id, "FLEET-MAINT", BudgetCodeCategory.Expense);
        await using (var context = fixture.CreateContext(tenant))
        {
            context.BudgetPeriods.Add(period);
            context.BudgetCodes.Add(code);
            context.BudgetAllocations.Add(BudgetAllocation.Create(
                tenant, period.Id, code.Id, code.Code, BudgetCodeCategory.Expense, Item(75m), actorId: null).Value);
            await context.SaveChangesAsync();
        }

        await PostgresFixture.BuildProjectionWorker(fixture.AppConnectionString).ProcessOnceAsync(CancellationToken.None);

        await using var reader = fixture.CreateContext(tenant);
        var rollup = await new VendorReadService(reader).GetPlannedRollupAsync(period.Id);
        Assert.Empty(rollup.Vendors);
        Assert.Equal((1, 75m), (rollup.NoVendor.ItemCount, rollup.NoVendor.PlannedCad));
        Assert.Empty(Assert.Single(await new BudgetAllocationReadService(reader).GetForPeriodAsync(period.Id)).VendorShares);
    }

    [Fact]
    public async Task The_untracked_shares_check_translates_and_answers_per_period()
    {
        var seeded = await SeedAsync();

        await using var context = fixture.CreateContext(seeded.Tenant);
        var repository = new BudgetAllocationRepository(context);

        Assert.True(await repository.AnyWithVendorSharesInPeriodAsync(seeded.Source.Id));
        Assert.False(await repository.AnyWithVendorSharesInPeriodAsync(seeded.Target.Id));
        Assert.Empty(context.ChangeTracker.Entries());

        // Another tenant's plan is invisible to it (query filter + RLS).
        await using var other = fixture.CreateContext(PostgresFixture.TenantB);
        Assert.False(await new BudgetAllocationRepository(other).AnyWithVendorSharesInPeriodAsync(seeded.Source.Id));
    }
}
