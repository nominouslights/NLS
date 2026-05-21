using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShuttleApi.Application.Services;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Infrastructure.Persistence.Seeder;

public static class DevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            var admin = User.Create(
                email: "admin@northernlink.com",
                passwordHash: hasher.Hash("Admin123!"),
                role: UserRole.Admin);
            admin.Activate();

            await db.Users.AddAsync(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("Dev seed: admin user created.");
        }
    }
}
