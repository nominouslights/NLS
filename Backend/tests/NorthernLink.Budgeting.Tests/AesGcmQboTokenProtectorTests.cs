using System.Security.Cryptography;
using NorthernLink.Budgeting.Infrastructure.Qbo;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The vault's encryption: round trip, tamper detection, the (tenant, realm) binding, and key
/// rotation. Keys come from a dictionary, never the process environment.
/// </summary>
public class AesGcmQboTokenProtectorTests
{
    private const string Token = "eyJlbmMiOiJBMTI4Q0JDLUhTMjU2IiwiYWxnIjoiZGlyIn0..refresh-token-value";

    private static AesGcmQboTokenProtector Protector(string? current, string? previous = null)
    {
        var variables = new Dictionary<string, string?>
        {
            [QboSecrets.TokenKeyVariable] = current,
            [QboSecrets.PreviousTokenKeyVariable] = previous,
        };
        return new AesGcmQboTokenProtector(name => variables.GetValueOrDefault(name));
    }

    [Fact]
    public void Round_trips_and_never_contains_the_plaintext()
    {
        var protector = Protector(TestQbo.NewKey());

        var sealedValue = protector.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId);

        Assert.DoesNotContain(Token, sealedValue, StringComparison.Ordinal);
        Assert.StartsWith($"v1:{protector.CurrentKeyId}:", sealedValue, StringComparison.Ordinal);
        Assert.Equal(5, sealedValue.Split(':').Length);
        Assert.Equal(Token, protector.Unprotect(sealedValue, TestBudgeting.TenantId, TestQbo.RealmId));
    }

    [Fact]
    public void Two_seals_of_the_same_token_differ()
    {
        var protector = Protector(TestQbo.NewKey());

        Assert.NotEqual(
            protector.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId),
            protector.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId));
    }

    [Theory]
    [InlineData(2)] // nonce
    [InlineData(3)] // ciphertext
    [InlineData(4)] // tag
    public void Tampering_with_any_part_fails(int part)
    {
        var protector = Protector(TestQbo.NewKey());
        var parts = protector.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId).Split(':');

        var chars = parts[part].ToCharArray();
        chars[0] = chars[0] == 'A' ? 'B' : 'A';
        parts[part] = new string(chars);

        Assert.ThrowsAny<CryptographicException>(
            () => protector.Unprotect(string.Join(':', parts), TestBudgeting.TenantId, TestQbo.RealmId));
    }

    [Fact]
    public void A_cipher_moved_to_another_tenant_fails()
    {
        var protector = Protector(TestQbo.NewKey());
        var sealedValue = protector.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId);

        Assert.ThrowsAny<CryptographicException>(
            () => protector.Unprotect(sealedValue, TestQbo.OtherTenantId, TestQbo.RealmId));
    }

    [Fact]
    public void A_cipher_moved_to_another_realm_fails()
    {
        var protector = Protector(TestQbo.NewKey());
        var sealedValue = protector.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId);

        Assert.ThrowsAny<CryptographicException>(
            () => protector.Unprotect(sealedValue, TestBudgeting.TenantId, TestQbo.OtherRealmId));
    }

    [Fact]
    public void The_previous_key_still_decrypts_after_a_rotation_but_never_encrypts()
    {
        var oldKey = TestQbo.NewKey();
        var newKey = TestQbo.NewKey();
        var sealedUnderOld = Protector(oldKey).Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId);

        var rotated = Protector(newKey, previous: oldKey);

        Assert.Equal(Token, rotated.Unprotect(sealedUnderOld, TestBudgeting.TenantId, TestQbo.RealmId));
        Assert.Equal(AesGcmQboTokenProtector.KeyIdOf(Convert.FromBase64String(newKey)), rotated.CurrentKeyId);
        Assert.Contains($":{rotated.CurrentKeyId}:", rotated.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId), StringComparison.Ordinal);
    }

    [Fact]
    public void A_cipher_from_a_key_no_longer_configured_fails()
    {
        var sealedUnderOld = Protector(TestQbo.NewKey()).Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId);

        Assert.ThrowsAny<CryptographicException>(
            () => Protector(TestQbo.NewKey()).Unprotect(sealedUnderOld, TestBudgeting.TenantId, TestQbo.RealmId));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("v2:a:b:c:d")]
    [InlineData("v1:only:three")]
    public void A_malformed_value_fails_as_a_CryptographicException(string value)
    {
        Assert.ThrowsAny<CryptographicException>(
            () => Protector(TestQbo.NewKey()).Unprotect(value, TestBudgeting.TenantId, TestQbo.RealmId));
    }

    [Fact]
    public void A_missing_key_is_reported_by_variable_name_on_first_use_not_at_construction()
    {
        var protector = Protector(current: null); // constructing must not throw

        var exception = Assert.Throws<InvalidOperationException>(() => protector.CurrentKeyId);
        Assert.Contains(QboSecrets.TokenKeyVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_that_is_not_32_bytes_is_refused()
    {
        var protector = Protector(Convert.ToBase64String(new byte[16]));

        Assert.Throws<InvalidOperationException>(() => protector.Protect(Token, TestBudgeting.TenantId, TestQbo.RealmId));
    }
}
