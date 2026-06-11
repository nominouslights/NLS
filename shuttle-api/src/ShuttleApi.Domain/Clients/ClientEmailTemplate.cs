using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Clients;

public sealed class ClientEmailTemplate : AggregateRoot<Guid>
{
    public Guid ClientId { get; private set; }
    public ClientEmailTemplateType Type { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; }

    private ClientEmailTemplate() { }

    public static ClientEmailTemplate Create(
        Guid clientId,
        ClientEmailTemplateType type,
        string subject,
        string body)
    {
        return new ClientEmailTemplate
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Type = type,
            Subject = subject,
            Body = body,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string subject, string body)
    {
        Subject = subject;
        Body = body;
        UpdatedAt = DateTime.UtcNow;
    }
}
