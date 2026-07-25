using NorthernLink.Identity.Application.Abstractions;

namespace NorthernLink.Identity.Tests;

/// <summary>Deterministic fake of the password hasher — a reversible prefix, no real KDF.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string password, string hash) => hash == Hash(password);
}
