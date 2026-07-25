namespace NorthernLink.Identity.Application.Abstractions;

/// <summary>
/// Password hashing for User credentials — deliberately distinct from
/// <see cref="IAccessTokenIssuer"/>'s opaque-token hashing: passwords need a slow,
/// salted KDF (PBKDF2 in Infrastructure); tokens are already high-entropy random values,
/// where a fast hash (SHA-256) is correct and sufficient.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
