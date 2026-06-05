using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Clients;

public sealed class ClientNotificationEmail : Entity<Guid>
{
    public Guid ClientId { get; private set; }
    public ClientNotificationCategory Category { get; private set; }
    public string Email { get; private set; } = string.Empty;

    private ClientNotificationEmail() { }

    public static ClientNotificationEmail Create(
        Guid clientId,
        ClientNotificationCategory category,
        string email)
    {
        return new ClientNotificationEmail
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Category = category,
            Email = email.Trim()
        };
    }
}
