using NorthernLink.Budgeting.Domain.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Qbo;

/// <summary>
/// Non-secret QuickBooks Online settings, bound from <c>appsettings.json</c> section
/// <c>Budgeting:Qbo</c>. Every property has a working default, so an absent section (tests, a
/// fresh host) still yields a usable sandbox configuration. The three secrets are not here:
/// see <see cref="QboSecrets"/>.
/// </summary>
public sealed class QboOptions
{
    public const string SectionName = "Budgeting:Qbo";

    /// <summary>Which Intuit environment to talk to. Decides <see cref="ApiBaseUrl"/>.</summary>
    public QboEnvironment Environment { get; init; } = QboEnvironment.Sandbox;

    public string AuthorizeUrl { get; init; } = "https://appcenter.intuit.com/connect/oauth2";

    public string TokenUrl { get; init; } = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    public string RevokeUrl { get; init; } = "https://developer.api.intuit.com/v2/oauth2/tokens/revoke";

    /// <summary>
    /// The Budgeting console's callback page — a frontend route, never an API route, because the
    /// API is not reachable from the public internet. Must be registered verbatim on the Intuit
    /// app, one entry per environment.
    /// </summary>
    public string RedirectUri { get; init; } = "http://localhost:3003/qbo/callback";

    /// <summary>
    /// The Accounting API minor version sent on every call. Pinned so Intuit cannot change a
    /// response shape under the importer; 75 is the floor Intuit supports from August 2025.
    /// </summary>
    public int MinorVersion { get; init; } = 75;

    /// <summary>How far back the first expense import reaches when no period starts earlier (used by the import).</summary>
    public int BackfillMonths { get; init; } = 13;

    /// <summary>
    /// How often the scheduled import runs (used by the import). Six hours also keeps the
    /// refresh token, which lapses after about 100 days unused, alive.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromHours(6);

    /// <summary>The only OAuth scope requested: read access to accounting data. Never payments, never OpenID.</summary>
    public const string Scope = "com.intuit.quickbooks.accounting";

    /// <summary>Derived from <see cref="Environment"/>, never configured on its own, so the two cannot disagree.</summary>
    public string ApiBaseUrl => Environment == QboEnvironment.Production
        ? "https://quickbooks.api.intuit.com"
        : "https://sandbox-quickbooks.api.intuit.com";
}

/// <summary>
/// The QuickBooks secrets, read from the process environment at the moment they are used —
/// never at DI registration, which must run with no environment at all
/// (<c>HandlerRegistrationRulesTests</c> calls <c>AddBudgeting</c> bare).
/// </summary>
public static class QboSecrets
{
    /// <summary>The Intuit app's client id.</summary>
    public const string ClientIdVariable = "Budgeting__QboClientId";

    /// <summary>The Intuit app's client secret.</summary>
    public const string ClientSecretVariable = "Budgeting__QboClientSecret";

    /// <summary>Base64 of 32 random bytes: the AES-256-GCM key that encrypts the token vault.</summary>
    public const string TokenKeyVariable = "Budgeting__QboTokenKey";

    /// <summary>Optional. The previous vault key, kept during a rotation so old rows still decrypt. Never encrypts.</summary>
    public const string PreviousTokenKeyVariable = "Budgeting__QboTokenKeyPrevious";
}
