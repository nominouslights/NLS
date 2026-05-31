using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Users;

public sealed class User : AggregateRoot<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsActive { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiry { get; private set; }
    public bool MustChangePassword { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, UserRole role)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            IsActive = false,
            MustChangePassword = false,
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void RequirePasswordChange() => MustChangePassword = true;

    public void ClearPasswordChangeRequired() => MustChangePassword = false;

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
        MustChangePassword = false;
    }

    public void SetRefreshToken(string token, DateTime expiry)
    {
        RefreshToken = token;
        RefreshTokenExpiry = expiry;
    }

    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiry = null;
    }
}
