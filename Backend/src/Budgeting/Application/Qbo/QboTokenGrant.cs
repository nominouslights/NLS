namespace NorthernLink.Budgeting.Application.Qbo;

/// <summary>
/// A token pair from Intuit's token endpoint, with absolute expiries computed when it arrived.
/// Lives only in memory between the HTTP response and the encrypted vault. <see cref="ToString"/>
/// is overridden so a stray log line or exception message can never print a token.
/// </summary>
public sealed record QboTokenGrant(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc)
{
    public override string ToString() =>
        $"QboTokenGrant {{ AccessTokenExpiresAtUtc = {AccessTokenExpiresAtUtc:O}, RefreshTokenExpiresAtUtc = {RefreshTokenExpiresAtUtc:O} }}";
}
