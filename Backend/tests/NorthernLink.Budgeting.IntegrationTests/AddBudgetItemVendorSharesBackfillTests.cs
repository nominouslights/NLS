using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Domain.Vendors;
using NorthernLink.Budgeting.Infrastructure.Persistence.Migrations;
using Xunit;

namespace NorthernLink.Budgeting.IntegrationTests;

/// <summary>
/// The AddBudgetItemVendorShares backfill's hand-written SQL, run for real: Up, Down, Up on a
/// throwaway database in the fixture's container (never the shared one). The point is the
/// normalization: the SQL must trim exactly what .NET's <c>string.Trim()</c> trims and land on the
/// same <c>normalized_name</c> as <see cref="Vendor.NormalizeName"/>, or a backfilled vendor and an
/// API-created one could differ only by a pasted non-breaking space.
/// <para>
/// Whitespace characters are built from code points (<see cref="Ch"/>) rather than written as
/// escapes, so this file stays plain ASCII and says exactly which characters it means.
/// </para>
/// </summary>
[Collection("postgres")]
public class AddBudgetItemVendorSharesBackfillTests(PostgresFixture fixture)
{
    private const string BeforeShares = "20261008181153_AddCostCentres";

    private static string Ch(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static readonly string Nbsp = Ch(0x00A0);
    private static readonly string IdeographicSpace = Ch(0x3000);
    private static readonly string EmSpace = Ch(0x2003);
    private static readonly string NarrowNbsp = Ch(0x202F);
    private static readonly string ZeroWidthSpace = Ch(0x200B); // NOT whitespace to .NET: kept.

    [Fact]
    public void The_migrations_whitespace_class_is_exactly_what_dotnet_Trim_strips()
    {
        var sql = string.Concat(new AddBudgetItemVendorShares().UpOperations.OfType<SqlOperation>().Select(o => o.Sql));
        var match = Regex.Match(sql, @"'\^(\[[^\]]+\])\+\|(\[[^\]]+\])\+\$'");
        Assert.True(match.Success, "the trimming regexp_replace pattern was not found in the Up SQL");
        Assert.Equal(match.Groups[1].Value, match.Groups[2].Value);

        var inClass = new HashSet<int>();
        foreach (Match range in Regex.Matches(match.Groups[1].Value, @"\\u([0-9A-Fa-f]{4})(?:-\\u([0-9A-Fa-f]{4}))?"))
        {
            var from = int.Parse(range.Groups[1].Value, NumberStyles.HexNumber);
            var to = range.Groups[2].Success ? int.Parse(range.Groups[2].Value, NumberStyles.HexNumber) : from;
            for (var c = from; c <= to; c++)
            {
                inClass.Add(c);
            }
        }

        var dotnetWhitespace = Enumerable.Range(0, 0x10000).Where(c => char.IsWhiteSpace((char)c)).ToHashSet();
        Assert.Equal(dotnetWhitespace.Order(), inClass.Order());
    }

    [Fact]
    public async Task Up_Down_Up_trims_like_dotnet_and_normalizes_like_Vendor_NormalizeName()
    {
        var connectionString = await fixture.CreateBlankDatabaseAsync();
        var tenant = PostgresFixture.TenantA;

        await using (var migrating = PostgresFixture.CreateContext(connectionString, tenantId: null))
        {
            await migrating.Database.MigrateAsync();
        }

        // Items built through the domain on the current schema (no free text on expense items is
        // possible there), projected, then walked back to before the column existed.
        var period = BudgetPeriod.Create(tenant, PeriodGranularity.Quarter, 2026, 1).Value;
        var expense = BudgetCode.Create(
            tenant, period.Id, "FLEET-MAINT", new BudgetCodeDetails { Name = "Fleet", Category = BudgetCodeCategory.Expense }, null).Value;
        var revenue = BudgetCode.Create(
            tenant, period.Id, "ZBB-CREW-01",
            new BudgetCodeDetails { Name = "Crew", Category = BudgetCodeCategory.Revenue, ServiceLine = BudgetServiceLine.ContractCrew }, null).Value;

        BudgetAllocation Expense(decimal amount) => BudgetAllocation.Create(
            tenant, period.Id, expense.Id, expense.Code, BudgetCodeCategory.Expense,
            new BudgetItemDetails { Title = "Item", AmountCad = amount, Justification = "Because." }, null).Value;

        var first = Expense(100m);
        var second = Expense(50m);
        var zeroDollar = Expense(0m);
        var blankOnly = Expense(10m);
        var zeroWidth = Expense(20m);
        var payer = BudgetAllocation.Create(
            tenant, period.Id, revenue.Id, revenue.Code, BudgetCodeCategory.Revenue,
            new BudgetItemDetails { Title = "Item", AmountCad = 10m, Justification = "Because.", Vendor = "Alamos Gold" }, null).Value;

        await using (var context = PostgresFixture.CreateContext(connectionString, tenant))
        {
            context.BudgetPeriods.Add(period);
            context.BudgetCodes.AddRange(expense, revenue);
            // Saved one by one so created_at_utc orders them: "first" is the earliest spelling.
            foreach (var item in new[] { first, second, zeroDollar, blankOnly, zeroWidth, payer })
            {
                context.BudgetAllocations.Add(item);
                await context.SaveChangesAsync();
            }
        }

        await PostgresFixture.RebuildProjectionsAsync(connectionString);
        await MigrateToAsync(connectionString, BeforeShares);

        // The legacy free text, with whitespace .NET trims and btrim() did not.
        var raw = new Dictionary<Guid, string>
        {
            [first.Id] = Nbsp + "Kal Tire" + IdeographicSpace,
            [second.Id] = " kal tire\t" + NarrowNbsp,
            [zeroDollar.Id] = EmSpace + "Gord's Garage" + Nbsp,
            [blankOnly.Id] = Nbsp + Ch(0x2009) + "\r\n",
            [zeroWidth.Id] = ZeroWidthSpace + "Northern Fuel" + Nbsp,
        };
        await using (var connection = await PostgresFixture.OpenRawConnectionAsync(connectionString, tenant))
        {
            foreach (var (id, text) in raw)
            {
                await using var update = new NpgsqlCommand(
                    "UPDATE budgeting.budget_allocations SET vendor = @v WHERE id = @id;" +
                    "UPDATE budgeting.rm_budget_allocations SET vendor = @v WHERE id = @id;", connection);
                update.Parameters.AddWithValue("v", text);
                update.Parameters.AddWithValue("id", id);
                await update.ExecuteNonQueryAsync();
            }
        }

        await MigrateToAsync(connectionString, target: null);
        await AssertBackfilledAsync();

        // Down restores the text (the vendor's trimmed name) and drops the column; Up again finds
        // the same vendors (ON CONFLICT) and rebuilds the same shares.
        await MigrateToAsync(connectionString, BeforeShares);
        await using (var connection = await PostgresFixture.OpenRawConnectionAsync(connectionString, tenant))
        {
            Assert.Equal("Kal Tire", await ScalarAsync(connection, "SELECT vendor FROM budgeting.budget_allocations WHERE id = @id", first.Id));
            Assert.Equal("Kal Tire", await ScalarAsync(connection, "SELECT vendor FROM budgeting.rm_budget_allocations WHERE id = @id", first.Id));
        }

        await MigrateToAsync(connectionString, target: null);
        await AssertBackfilledAsync();

        async Task AssertBackfilledAsync()
        {
            await using var reader = PostgresFixture.CreateContext(connectionString, tenant);
            var vendors = await reader.Vendors.AsNoTracking().ToListAsync();

            // One vendor per .NET-normalized name, holding the earliest trimmed spelling.
            Assert.Equal(
                new[] { "Gord's Garage", "Kal Tire", ZeroWidthSpace + "Northern Fuel" }.Order(StringComparer.Ordinal),
                vendors.Select(v => v.Name).Order(StringComparer.Ordinal));
            foreach (var text in new[] { raw[first.Id], raw[second.Id], raw[zeroDollar.Id], raw[zeroWidth.Id] })
            {
                Assert.Contains(vendors, v => v.NormalizedName == Vendor.NormalizeName(text));
            }

            var kal = vendors.Single(v => v.Name == "Kal Tire");
            Assert.Equal(Vendor.NormalizeName("Kal Tire"), kal.NormalizedName);

            var items = await reader.BudgetAllocations.AsNoTracking().ToDictionaryAsync(a => a.Id);
            Assert.Equal([(kal.Id, 100m)], items[first.Id].VendorShares.Select(s => (s.VendorId, s.AmountCad)));
            Assert.Null(items[first.Id].Vendor);
            Assert.Equal([(kal.Id, 50m)], items[second.Id].VendorShares.Select(s => (s.VendorId, s.AmountCad)));

            // $0: vendor registered, text kept as it was, no share.
            Assert.Empty(items[zeroDollar.Id].VendorShares);
            Assert.Equal(raw[zeroDollar.Id], items[zeroDollar.Id].Vendor);

            // Whitespace only: blank to .NET, so no vendor and no share; the text is left alone.
            Assert.Empty(items[blankOnly.Id].VendorShares);
            Assert.Equal(raw[blankOnly.Id], items[blankOnly.Id].Vendor);

            // Revenue payer untouched.
            Assert.Empty(items[payer.Id].VendorShares);
            Assert.Equal("Alamos Gold", items[payer.Id].Vendor);

            // The read side was written alongside (raw SQL bypasses the journal).
            var readRows = await reader.BudgetAllocationReadModels.AsNoTracking().ToDictionaryAsync(a => a.Id);
            Assert.Equal(kal.Id, Assert.Single(readRows[first.Id].VendorShares).VendorId);
            Assert.Null(readRows[first.Id].Vendor);
            Assert.Equal(3, await reader.VendorReadModels.AsNoTracking().CountAsync());
        }
    }

    private static async Task MigrateToAsync(string connectionString, string? target)
    {
        await using var context = PostgresFixture.CreateContext(connectionString, tenantId: null);
        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    private static async Task<string?> ScalarAsync(NpgsqlConnection connection, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (await command.ExecuteScalarAsync())?.ToString();
    }
}
