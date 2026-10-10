using System.Security.Cryptography;
using System.Text;
using NorthernLink.Budgeting.Application.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Qbo;

/// <summary>
/// <see cref="IQboTokenProtector"/> with AES-256-GCM. Output format, every part base64url:
/// <c>v1:{keyId}:{nonce}:{ciphertext}:{tag}</c>.
/// <list type="bullet">
/// <item><description><b>Key</b>: <c>Budgeting__QboTokenKey</c>, base64 of exactly 32 bytes.
/// <c>Budgeting__QboTokenKeyPrevious</c>, optional, is accepted for decryption only, so a key
/// can be rotated without stranding every stored token: set the old key as previous, the new
/// one as current, and rows re-encrypt under the new key on their next write.</description></item>
/// <item><description><b>Key id</b>: the first 8 bytes of SHA-256 of the key, hex. It names
/// which key sealed a value without anyone maintaining a key-id setting.</description></item>
/// <item><description><b>Associated data</b>: <c>"{tenantId}|{realmId}"</c>. Authenticated,
/// not encrypted: a ciphertext moved to another tenant's or another company's row fails the tag
/// check instead of decrypting.</description></item>
/// <item><description><b>Nonce</b>: 12 random bytes per call — the GCM standard size; a key
/// sealing a few tokens a day is nowhere near the random-nonce collision bound.</description></item>
/// </list>
/// Keys are read lazily on first use, never at registration, and a failed read is retried on the
/// next call rather than cached.
/// </summary>
public sealed class AesGcmQboTokenProtector : IQboTokenProtector
{
    private const string Version = "v1";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly Func<string, string?> _readVariable;
    private readonly Lazy<KeyRing> _keys;

    /// <summary>Reads the keys from the process environment.</summary>
    public AesGcmQboTokenProtector()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Reads the keys through <paramref name="readVariable"/> (tests pass a dictionary).</summary>
    public AesGcmQboTokenProtector(Func<string, string?> readVariable)
    {
        _readVariable = readVariable;
        _keys = new Lazy<KeyRing>(LoadKeys, LazyThreadSafetyMode.PublicationOnly);
    }

    public string CurrentKeyId => _keys.Value.Current.Id;

    public string Protect(string plaintext, Guid tenantId, string realmId)
    {
        var key = _keys.Value.Current;
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key.Bytes, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag, AssociatedData(tenantId, realmId));
        }

        return string.Join(':', Version, key.Id, Base64Url(nonce), Base64Url(cipher), Base64Url(tag));
    }

    public string Unprotect(string protectedValue, Guid tenantId, string realmId)
    {
        var parts = protectedValue.Split(':');
        if (parts.Length != 5 || parts[0] != Version)
        {
            throw new CryptographicException("The protected token is not in the v1 format.");
        }

        var key = _keys.Value.Find(parts[1])
            ?? throw new CryptographicException($"The protected token was sealed with key {parts[1]}, which is not configured.");

        byte[] nonce, cipher, tag;
        try
        {
            nonce = FromBase64Url(parts[2]);
            cipher = FromBase64Url(parts[3]);
            tag = FromBase64Url(parts[4]);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("The protected token is not valid base64url.", exception);
        }

        if (nonce.Length != NonceSize || tag.Length != TagSize)
        {
            throw new CryptographicException("The protected token has a malformed nonce or tag.");
        }

        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(key.Bytes, TagSize))
        {
            // AuthenticationTagMismatchException (a CryptographicException) on any tampering or
            // associated-data mismatch.
            aes.Decrypt(nonce, cipher, tag, plain, AssociatedData(tenantId, realmId));
        }

        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>The key id a given key would carry — exposed for tests and for operators checking a rotation.</summary>
    public static string KeyIdOf(byte[] key) => Convert.ToHexStringLower(SHA256.HashData(key).AsSpan(0, 8));

    private static byte[] AssociatedData(Guid tenantId, string realmId) =>
        Encoding.UTF8.GetBytes($"{tenantId:D}|{realmId}");

    private KeyRing LoadKeys()
    {
        var current = ParseKey(QboSecrets.TokenKeyVariable, required: true)!;
        var previous = ParseKey(QboSecrets.PreviousTokenKeyVariable, required: false);
        return new KeyRing(current, previous);
    }

    private Key? ParseKey(string variable, bool required)
    {
        var value = _readVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return required
                ? throw new InvalidOperationException($"The {variable} environment variable is not set.")
                : null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(value.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"The {variable} environment variable must be base64 of {KeySize} bytes.");
        }

        if (bytes.Length != KeySize)
        {
            throw new InvalidOperationException($"The {variable} environment variable must be base64 of {KeySize} bytes.");
        }

        return new Key(KeyIdOf(bytes), bytes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException(),
        };
        return Convert.FromBase64String(padded);
    }

    private sealed record Key(string Id, byte[] Bytes);

    private sealed record KeyRing(Key Current, Key? Previous)
    {
        public Key? Find(string keyId) =>
            Current.Id == keyId ? Current
            : Previous is { } previous && previous.Id == keyId ? previous
            : null;
    }
}
