using NorthernLink.Identity.Infrastructure.Auth;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// The real PBKDF2 hasher — the thing standing between a stolen database dump and every
/// account on the platform. It had no test at all: <c>Verify</c> has three branches that
/// return <c>false</c> without saying why, and nothing anywhere proved that what
/// <c>Hash</c> writes is something <c>Verify</c> accepts.
/// <para>
/// These are deliberately slow-ish (210,000 PBKDF2 iterations per call, ~50ms). That cost is
/// the point of the algorithm; keep the number of hashing calls small rather than lowering it.
/// </para>
/// </summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_Verify_round_trips()
    {
        const string password = "correct horse battery staple";

        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
    }

    [Fact]
    public void A_wrong_password_does_not_verify()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("Correct horse battery staple", hash));
    }

    [Fact]
    public void The_same_password_hashed_twice_produces_different_hashes()
    {
        // Per-hash random salt: two accounts with the same password must not share a stored
        // value, or a dump reveals which accounts to attack together.
        const string password = "shared-password";

        var first = _hasher.Hash(password);
        var second = _hasher.Hash(password);

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(password, first));
        Assert.True(_hasher.Verify(password, second));
    }

    [Fact]
    public void The_stored_format_carries_iterations_salt_and_hash()
    {
        // {iterations}.{saltBase64}.{hashBase64} — the format IS the upgrade path: the
        // iteration count and salt travel with the hash so raising the constant later still
        // verifies every hash written before the bump. A format change silently invalidates
        // every stored password.
        var parts = _hasher.Hash("anything").Split('.');

        Assert.Equal(3, parts.Length);
        Assert.Equal(210_000, int.Parse(parts[0]));
        Assert.Equal(16, Convert.FromBase64String(parts[1]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[2]).Length);
    }

    [Fact]
    public void A_hash_written_at_a_different_iteration_count_still_verifies()
    {
        // The reason iterations are stored rather than assumed. Hand-built at a lower count so
        // the test stays fast; the assertion is that Verify honours the stored number instead
        // of the current constant.
        const string password = "legacy-account";
        var salt = new byte[16];
        Random.Shared.NextBytes(salt);
        var derived = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password, salt, 1_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var stored = $"1000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derived)}";

        Assert.True(_hasher.Verify(password, stored));
    }

    [Theory]
    // parts.Length != 3 — the first silent-false branch.
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("210000.onlytwoparts")]
    // int.TryParse fails on the iteration count — same branch, other half.
    [InlineData("many.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData(".AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    // Convert.FromBase64String throws FormatException — the second silent-false branch.
    [InlineData("210000.not-base64!.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("210000.AAAAAAAAAAAAAAAAAAAAAA==.not-base64!")]
    public void A_malformed_stored_hash_verifies_as_false_rather_than_throwing(string stored)
    {
        // Each of these is a branch in Verify that returns false with no diagnostic. A stored
        // hash is written by Hash, so reaching one means the row is corrupt — the contract that
        // matters is that login fails closed instead of throwing a 500 that leaks the shape of
        // the credential store.
        Assert.False(_hasher.Verify("any-password", stored));
    }

    [Fact]
    public void A_structurally_valid_hash_of_the_wrong_length_verifies_as_false()
    {
        // FixedTimeEquals over different-length spans is false, not an exception — and Pbkdf2
        // is asked for expectedHash.Length bytes, so a truncated stored hash stays comparable.
        var salt = Convert.ToBase64String(new byte[16]);
        var shortHash = Convert.ToBase64String(new byte[8]);

        Assert.False(_hasher.Verify("any-password", $"1000.{salt}.{shortHash}"));
    }

    [Fact]
    public void An_empty_password_round_trips()
    {
        // The hasher is not the layer that rejects empty passwords (User creation is), so it
        // must handle one rather than throw partway through login.
        var hash = _hasher.Hash(string.Empty);

        Assert.True(_hasher.Verify(string.Empty, hash));
        Assert.False(_hasher.Verify(" ", hash));
    }
}
