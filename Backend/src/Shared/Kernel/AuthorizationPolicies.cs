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
    /// Budget and financial control — Owner and Accountant only. Carried by the whole
    /// <c>/api/budgeting</c> group (periods and codes today); every endpoint added there must
    /// join that group or attach this policy itself.
    /// </summary>
    public const string BudgetAccess = "BudgetAccess";

    /// <summary>
    /// Dispatch operations — Owner, Dispatcher, and Supervisor only. Carried by the whole
    /// <c>/api/notifications</c> group (templates, sends, and history today); every endpoint
    /// added there must join that group or attach this policy itself.
    /// </summary>
    public const string DispatchAccess = "DispatchAccess";
}
