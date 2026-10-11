using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Qbo;

/// <summary>
/// Every error the QuickBooks connection can produce, from the aggregate and from its handlers.
/// The console shows each message verbatim.
/// <para>
/// Three of them are not the caller's fault: Intuit refused or could not be reached
/// (<see cref="TokenExchangeFailed"/>, <see cref="CompanyLookupFailed"/>) or the API host is
/// missing its Intuit credentials (<see cref="NotConfigured"/>). <see cref="ErrorType"/> has no
/// member for an upstream failure, so they are typed <see cref="ErrorType.None"/> and the
/// module's <c>EndpointResults</c> maps them by code (502 and 503). <see cref="UpstreamCodes"/>
/// is that list.
/// </para>
/// </summary>
public static class QboConnectionErrors
{
    public static readonly Error NotConnected = Error.NotFound(
        "Budgeting.Qbo.NotConnected", "QuickBooks Online is not connected.");

    public static readonly Error UserRequired = Error.Unauthorized(
        "Budgeting.Qbo.UserRequired", "Connecting QuickBooks needs a signed-in user.");

    public static readonly Error CallbackIncomplete = Error.Validation(
        "Budgeting.Qbo.CallbackIncomplete",
        "QuickBooks did not send back everything needed to finish connecting. Start the connection again.");

    public static readonly Error StateInvalid = Error.Validation(
        "Budgeting.Qbo.StateInvalid",
        "This QuickBooks sign-in link is not valid for you, or it has already been used. Start the connection again.");

    public static readonly Error StateExpired = Error.Validation(
        "Budgeting.Qbo.StateExpired",
        "This QuickBooks sign-in took longer than 10 minutes. Start the connection again.");

    public static readonly Error RealmIdInvalid = Error.Validation(
        "Budgeting.Qbo.RealmIdInvalid",
        "QuickBooks sent back a company id this platform does not recognise. Start the connection again.");

    public static readonly Error HomeCurrencyNotCad = Error.Conflict(
        "Budgeting.Qbo.HomeCurrencyNotCad",
        "That QuickBooks company's home currency is not Canadian dollars. Budgets here are in CAD, so it cannot be connected.");

    public static readonly Error DifferentCompany = Error.Conflict(
        "Budgeting.Qbo.DifferentCompany",
        "This workspace has already been connected to a different QuickBooks company. Connect that same company again.");

    public static readonly Error AlreadyDisconnected = Error.Conflict(
        "Budgeting.Qbo.AlreadyDisconnected", "QuickBooks Online is already disconnected.");

    public static readonly Error TokenExchangeFailed = new(
        "Budgeting.Qbo.TokenExchangeFailed",
        "QuickBooks did not accept the sign-in. Start the connection again; if it keeps failing, try later.");

    public static readonly Error CompanyLookupFailed = new(
        "Budgeting.Qbo.CompanyLookupFailed",
        "Signed in to QuickBooks, but its company details could not be read. Try connecting again later.");

    public static readonly Error NotConfigured = new(
        "Budgeting.Qbo.NotConfigured",
        "QuickBooks Online is not set up on this server yet. Ask the platform administrator to add the Intuit app credentials.");

    /// <summary>Codes the endpoint layer answers with 502 (Intuit failed) rather than a 4xx.</summary>
    public static readonly IReadOnlySet<string> UpstreamCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        TokenExchangeFailed.Code,
        CompanyLookupFailed.Code,
    };
}
