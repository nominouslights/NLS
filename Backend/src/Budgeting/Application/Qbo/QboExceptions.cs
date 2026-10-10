namespace NorthernLink.Budgeting.Application.Qbo;

/// <summary>Why a call to Intuit's OAuth endpoints failed.</summary>
public enum QboAuthFailure
{
    /// <summary>Intuit answered <c>invalid_grant</c>: the code or refresh token is spent, expired or revoked.</summary>
    InvalidGrant,

    /// <summary>Intuit answered with any other error.</summary>
    Rejected,

    /// <summary>Network failure, timeout, or a response that could not be read.</summary>
    Unreachable,

    /// <summary>The API host has no Intuit client id / secret in its environment.</summary>
    NotConfigured,
}

/// <summary>
/// Thrown by <see cref="IQboAuthClient"/>. The message is built only from the HTTP status and
/// Intuit's short <c>error</c> code — never from a request or response body — so it can be
/// logged without leaking an authorization code or a token.
/// </summary>
public sealed class QboAuthException(QboAuthFailure failure, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public QboAuthFailure Failure { get; } = failure;
}

/// <summary>Why a call to the QuickBooks Accounting API failed.</summary>
public enum QboApiFailure
{
    /// <summary>HTTP 401: the access token was refused.</summary>
    Unauthorized,

    /// <summary>HTTP 429: Intuit's rate limit. Retryable later.</summary>
    Throttled,

    /// <summary>Any other non-success status, or a body without the expected fields.</summary>
    Rejected,

    /// <summary>Network failure or timeout.</summary>
    Unreachable,
}

/// <summary>
/// Thrown by <see cref="IQboAccountingClient"/>. Like <see cref="QboAuthException"/>, the message
/// carries only the status and the endpoint's name — never the bearer token or a response body.
/// </summary>
public sealed class QboApiException(QboApiFailure failure, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public QboApiFailure Failure { get; } = failure;
}

/// <summary>
/// Thrown by <see cref="IQboTokenStore.GetAccessTokenAsync"/> when no usable token exists: none
/// stored, Intuit refused the refresh token, or the stored tokens no longer decrypt. By the time
/// it is thrown the live connection has already been marked NeedsReconnect.
/// </summary>
public sealed class QboReconnectRequiredException(string errorCode)
    : Exception($"QuickBooks Online needs reconnecting ({errorCode}).")
{
    public string ErrorCode { get; } = errorCode;
}
