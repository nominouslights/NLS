using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Templates;
using NorthernLink.Notifications.Domain.Templates.Events;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>EmailTemplate factory validation, client pinning, and lifecycle events.</summary>
public class EmailTemplateTests
{
    [Fact]
    public void Name_is_required()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "  ", NotificationServiceType.Community, null, null, "Subject", "<p>Body</p>");

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Subject_is_required()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "Pickup", NotificationServiceType.Community, null, null, " ", "<p>Body</p>");

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.SubjectRequired, result.Error);
    }

    [Fact]
    public void HtmlBody_is_required()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "Pickup", NotificationServiceType.Community, null, null, "Subject", "");

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.HtmlBodyRequired, result.Error);
    }

    [Fact]
    public void HtmlBody_over_20000_characters_is_rejected()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "Pickup", NotificationServiceType.Community, null, null,
            "Subject", new string('x', EmailTemplate.HtmlBodyMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.HtmlBodyTooLong, result.Error);
    }

    [Fact]
    public void ClientName_is_required_when_ClientId_is_set()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "Hudbay pickup", NotificationServiceType.ContractCrew,
            Guid.NewGuid(), "  ", "Subject", "<p>Body</p>");

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.ClientNameRequired, result.Error);
    }

    [Fact]
    public void ClientName_without_ClientId_is_rejected()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "Hudbay pickup", NotificationServiceType.ContractCrew,
            null, "Hudbay Minerals", "Subject", "<p>Body</p>");

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.ClientNameWithoutClientId, result.Error);
    }

    [Fact]
    public void Unknown_merge_token_is_rejected_with_the_token_named()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "Pickup", NotificationServiceType.Community, null, null,
            "Subject", "<p>Hi {{PasengerName}}</p>");

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Template.UnknownMergeField", result.Error.Code);
        Assert.Contains("PasengerName", result.Error.Message);
    }

    [Fact]
    public void Unknown_merge_token_in_subject_is_rejected_too()
    {
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "Pickup", NotificationServiceType.Community, null, null,
            "Pickup {{Typo}}", "<p>Body</p>");

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Template.UnknownMergeField", result.Error.Code);
    }

    [Fact]
    public void Create_trims_fields_starts_active_and_raises_created_event()
    {
        var clientId = Guid.NewGuid();
        var result = EmailTemplate.Create(
            TestNotifications.TenantId, "  Hudbay pickup  ", NotificationServiceType.ContractCrew,
            clientId, "  Hudbay Minerals  ", "  Pickup {{TripDate}}  ", "<p>Hi {{PassengerName}}</p>");

        Assert.True(result.IsSuccess);
        var template = result.Value;
        Assert.Equal("Hudbay pickup", template.Name);
        Assert.Equal(clientId, template.ClientId);
        Assert.Equal("Hudbay Minerals", template.ClientName);
        Assert.Equal("Pickup {{TripDate}}", template.Subject);
        Assert.True(template.IsActive);
        var created = Assert.IsType<EmailTemplateCreatedDomainEvent>(Assert.Single(template.DomainEvents));
        Assert.Equal(template.Id, created.TemplateId);
        Assert.Equal(TestNotifications.TenantId, created.TenantId);
    }

    [Fact]
    public void Update_applies_changes_and_raises_updated_event()
    {
        var template = TestNotifications.CreateTemplate();
        template.ClearDomainEvents();

        var result = template.Update(
            "Charter pickup", NotificationServiceType.Charter, null, null,
            "New subject {{Route}}", "<p>{{DropoffStop}}</p>");

        Assert.True(result.IsSuccess);
        Assert.Equal("Charter pickup", template.Name);
        Assert.Equal(NotificationServiceType.Charter, template.ServiceType);
        Assert.Equal("New subject {{Route}}", template.Subject);
        Assert.IsType<EmailTemplateUpdatedDomainEvent>(Assert.Single(template.DomainEvents));
    }

    [Fact]
    public void Update_with_unknown_token_is_rejected_and_state_unchanged()
    {
        var template = TestNotifications.CreateTemplate();
        template.ClearDomainEvents();
        var originalBody = template.HtmlBody;

        var result = template.Update(
            template.Name, template.ServiceType, null, null, template.Subject, "<p>{{Nope}}</p>");

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Template.UnknownMergeField", result.Error.Code);
        Assert.Equal(originalBody, template.HtmlBody);
        Assert.Empty(template.DomainEvents);
    }

    [Fact]
    public void Deactivate_and_activate_flip_the_flag_and_raise_events()
    {
        var template = TestNotifications.CreateTemplate();
        template.ClearDomainEvents();

        Assert.True(template.Deactivate().IsSuccess);
        Assert.False(template.IsActive);
        Assert.IsType<EmailTemplateDeactivatedDomainEvent>(Assert.Single(template.DomainEvents));

        template.ClearDomainEvents();
        Assert.True(template.Activate().IsSuccess);
        Assert.True(template.IsActive);
        Assert.IsType<EmailTemplateActivatedDomainEvent>(Assert.Single(template.DomainEvents));
    }
}
