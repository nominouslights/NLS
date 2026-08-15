using NorthernLink.Notifications.Application;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Dispatches.Events;
using NorthernLink.Notifications.Domain.Templates.Events;
using NorthernLink.Notifications.Domain;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// Notifications publishes nothing today — every domain event stays internal (null),
/// deliberately: no other module keeps a replica of templates or dispatch history.
/// </summary>
public class NotificationsIntegrationEventMapperTests
{
    private readonly NotificationsIntegrationEventMapper _mapper = new();

    [Fact]
    public void Template_lifecycle_events_stay_internal()
    {
        var template = TestNotifications.CreateTemplate();
        var id = template.Id;
        var tenantId = TestNotifications.TenantId;

        Assert.Null(_mapper.Map(new EmailTemplateCreatedDomainEvent(id, tenantId), template));
        Assert.Null(_mapper.Map(new EmailTemplateUpdatedDomainEvent(id, tenantId), template));
        Assert.Null(_mapper.Map(new EmailTemplateActivatedDomainEvent(id, tenantId), template));
        Assert.Null(_mapper.Map(new EmailTemplateDeactivatedDomainEvent(id, tenantId), template));
    }

    [Fact]
    public void Dispatch_recorded_stays_internal()
    {
        var dispatch = EmailDispatch.Record(
            Guid.NewGuid(),
            TestNotifications.TenantId,
            Guid.NewGuid(),
            "NL-1042",
            null,
            Guid.NewGuid(),
            "Community pickup",
            NotificationServiceType.Community,
            null,
            null,
            [new DispatchRecipient { Email = "a@example.com", PassengerName = "A", Status = DispatchRecipientStatus.Sent }]).Value;

        Assert.Null(_mapper.Map(new EmailDispatchRecordedDomainEvent(dispatch.Id, dispatch.TenantId), dispatch));
    }
}
