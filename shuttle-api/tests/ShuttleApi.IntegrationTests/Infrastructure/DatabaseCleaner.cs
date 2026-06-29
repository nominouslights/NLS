using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShuttleApi.Infrastructure.Persistence;

namespace ShuttleApi.IntegrationTests.Infrastructure;

public sealed class DatabaseCleaner(ShuttleWebFactory factory)
{
    public async Task CleanAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            TRUNCATE TABLE
                trip_inspection_items,
                trip_pre_inspections,
                trip_post_reports,
                trip_passenger_email_logs,
                trip_passengers,
                trip_cargo_items,
                trip_stops,
                trips,
                driver_documents,
                driver_roster_entries,
                drivers,
                vehicle_service_records,
                vehicle_inspection_records,
                vehicle_fuel_entries,
                vehicles
            RESTART IDENTITY CASCADE
            """;
        await cmd.ExecuteNonQueryAsync();

        await conn.CloseAsync();
    }
}
