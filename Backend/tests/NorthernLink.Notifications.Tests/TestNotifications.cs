using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Tests;

/// <summary>Shared builders for Notifications tests.</summary>
internal static class TestNotifications
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static EmailTemplate CreateTemplate(
        string name = "Community pickup",
        NotificationServiceType serviceType = NotificationServiceType.Community,
        Guid? clientId = null,
        string? clientName = null,
        string subject = "Pickup {{TripDate}} — {{TripNumber}}",
        string htmlBody = "<p>Hi {{PassengerName}},</p><p>Pickup at {{PickupStop}} at {{PickupTime}}.</p>")
    {
        var result = EmailTemplate.Create(TenantId, name, serviceType, clientId, clientName, subject, htmlBody);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test template invalid: {result.Error.Code}");
        }

        return result.Value;
    }
}
