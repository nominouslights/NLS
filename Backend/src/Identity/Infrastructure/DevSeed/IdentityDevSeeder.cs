using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Identity.Infrastructure.Auth;
using NorthernLink.Identity.Infrastructure.Persistence;

namespace NorthernLink.Identity.Infrastructure.DevSeed;

/// <summary>
/// The real (not demo-data) seed that gives every environment exactly one login-capable
/// account on a fresh database: a single Admin with a randomly generated password, printed
/// once to the log so whoever brought the database up can log in. Idempotent — skips once any
/// user exists. Unlike FleetDevSeeder/TripsDevSeeder this always runs (see
/// <c>InitializeIdentityDatabaseAsync</c>), because a platform with no way to log in isn't a
/// usable platform in any environment, dev or otherwise.
/// </summary>
public static class IdentityDevSeeder
{
    private const string SeedAdminEmail = "admin@northernlink.local";

    public static async Task SeedAsync(IdentityDbContext context, Guid tenantId, ILogger? logger = null, CancellationToken cancellationToken = default)
    {
        if (await context.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        var password = GenerateReadablePassword();
        var passwordHasher = new PasswordHasher();

        var userResult = User.Create(tenantId, SeedAdminEmail, passwordHasher.Hash(password), "Admin");
        if (userResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to create the seed admin user: {userResult.Error.Message}");
        }

        context.Users.Add(userResult.Value);
        await context.SaveChangesAsync(cancellationToken);

        // The ONLY place this plaintext password ever appears. Once logged, it's gone —
        // there is no other way to retrieve it; the admin must log in and, if they want a
        // second account, mint a bootstrap token via POST /api/identity/admin/bootstrap-token.
        var message =
            $"Identity seed: created the initial admin account — email '{SeedAdminEmail}', " +
            $"password '{password}'. This password is shown here once and is not recoverable.";

        if (logger is not null)
        {
            logger.LogInformation("{Message}", message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// 16 random bytes, base64url-encoded — high entropy but still fine to read off a
    /// console and type once.
    /// </summary>
    private static string GenerateReadablePassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
