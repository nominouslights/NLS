namespace NorthernLink.Shared.Kernel;

/// <summary>
/// The single Internal (Northern Link) tenant seeded into every environment today — one
/// tenant exists until multi-tenant onboarding lands. Shared by Fleet/Trips demo seeding
/// and the Identity admin seed so all three agree on the same id without a runtime lookup.
/// </summary>
public static class SeedTenant
{
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
