using Npgsql;
using NorthernLink.Fleet.Domain.Shops;
using NorthernLink.Fleet.Domain.WorkOrders;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Row-Level Security for the maintenance tables (shops, work_orders, and by extension
/// service_records / vehicle_documents which use the identical policy). Seeds a row for
/// Tenant A via EF, then verifies a raw connection as the non-superuser app role scoped to
/// Tenant B sees nothing — the DB half of dual enforcement that local superuser dev can't prove.
/// </summary>
[Collection("postgres")]
public class MaintenanceRowLevelSecurityTests(PostgresFixture fixture)
{
    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Shops_are_invisible_to_other_tenants()
    {
        var shop = Shop.Register(
            PostgresFixture.TenantA, "SHOP-01", "Thompson Certified", null, null, null, null, null,
            mpiAccredited: true, "MB-1187", suppliesParts: false, null).Value;

        await using (var context = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            context.Shops.Add(shop);
            await context.SaveChangesAsync();
        }

        await using (var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.Equal(1, await CountAsync(tenantA, "SELECT count(*) FROM fleet.shops WHERE id = @id", shop.Id));
        }

        await using var tenantB = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantB);
        Assert.Equal(0, await CountAsync(tenantB, "SELECT count(*) FROM fleet.shops WHERE id = @id", shop.Id));
    }

    [Fact]
    public async Task Work_orders_are_invisible_to_other_tenants()
    {
        var workOrder = WorkOrder.Create(
            PostgresFixture.TenantA, Guid.NewGuid(), "WO-1001", "Front brake inspection", "Measure pads.",
            WorkOrderPriority.High, WorkOrderSource.Manual, null, "Dispatch", null, null,
            ["Inspect front brakes"], null, null, null, null).Value;

        await using (var context = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            context.WorkOrders.Add(workOrder);
            await context.SaveChangesAsync();
        }

        await using (var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.Equal(1, await CountAsync(tenantA, "SELECT count(*) FROM fleet.work_orders WHERE id = @id", workOrder.Id));
        }

        await using var tenantB = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantB);
        Assert.Equal(0, await CountAsync(tenantB, "SELECT count(*) FROM fleet.work_orders WHERE id = @id", workOrder.Id));
    }
}
