namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Names of the authorization policies the gateway registers (see
/// <c>Api/NorthernLink.Api/Auth/AuthorizationPolicyRegistration.cs</c>). The names live in
/// Shared.Kernel — not the Api project — because domain libraries map their own endpoint groups
/// and cannot reference the gateway, so <c>.RequireAuthorization(...)</c> call sites would
/// otherwise be stuck with magic strings.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Owner-level administration: minting bootstrap invites, and future tenant admin.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Budget and financial control — Owner and Accountant only. Registered now so Stage 6.1's
    /// <c>/api/budgeting/*</c> group attaches an existing policy instead of inventing one; no
    /// endpoint carries it yet.
    /// </summary>
    public const string BudgetAccess = "BudgetAccess";
}
