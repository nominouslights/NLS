using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>Factory helpers for User aggregates in tests.</summary>
internal static class TestUsers
{
    public static User Create(
        string email = "admin@northernlink.ca",
        string role = Roles.Owner,
        Guid? tenantId = null)
    {
        var result = User.Create(tenantId ?? SeedTenant.Id, email, $"hashed:pw-{email}", role);

        Assert.True(result.IsSuccess, $"Test user creation failed: {result.Error.Code}");
        return result.Value;
    }
}
