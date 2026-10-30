using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Integration;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The hours-of-service compliance alerts: who gets which email (by replicated role, active
/// only, own tenant only), the built-in subjects and bodies with Winnipeg local times, and the
/// deterministic dispatch ids that make a redelivered event send nothing twice.
/// </summary>
public class ComplianceAlertHandlerTests
{
    private static readonly Guid DriverId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>2026-09-14 20:00Z = Monday, September 14, 2026 at 15:00 CDT (UTC−5).</summary>
    private static readonly DateTimeOffset NoticedAt = new(2026, 9, 14, 20, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUserLookupRepository _users = new();
    private readonly InMemoryEmailDispatchRepository _dispatches = new();
    private readonly FakeEmailSender _sender = new();

    private ComplianceAlertMailer Mailer => new(_users, _dispatches, _sender, NullLogger<ComplianceAlertMailer>.Instance);

    private EldMalfunctionChangedIntegrationEventHandler EldHandler =>
        new(Mailer, NullLogger<EldMalfunctionChangedIntegrationEventHandler>.Instance);

    private DriverOutOfServiceChangedIntegrationEventHandler OosHandler =>
        new(Mailer, NullLogger<DriverOutOfServiceChangedIntegrationEventHandler>.Instance);

    /// <summary>The whole internal cast, one of each role, plus the two that must never be addressed.</summary>
    private void SeedRoster()
    {
        _users.Seed(Roles.Owner, "owner@northernlink.ca", fullName: "Owner Person");
        _users.Seed(Roles.ComplianceOfficer, "co@northernlink.ca");
        _users.Seed(Roles.Dispatcher, "dispatch@northernlink.ca");
        _users.Seed(Roles.Supervisor, "super@northernlink.ca");
        _users.Seed(Roles.Accountant, "books@northernlink.ca");
        _users.Seed(Roles.Driver, "driver@northernlink.ca");
        _users.Seed(Roles.ComplianceOfficer, "gone@northernlink.ca", isActive: false);
        _users.Seed(Roles.Owner, "other@example.org", tenantId: OtherTenantId);
    }

    private static EldMalfunctionChangedIntegrationEvent Eld(string status = "Open", Guid? eventId = null)
    {
        var e = new EldMalfunctionChangedIntegrationEvent(
            Guid.NewGuid(), TestNotifications.TenantId, DriverId, "Doris Spence", "P", status, NoticedAt,
            "Positioning light stayed red after the restart & no GPS fix.");
        return eventId is { } id ? e with { EventId = id } : e;
    }

    private static DriverOutOfServiceChangedIntegrationEvent Oos(
        string status = "Active", DateTimeOffset? untilUtc = null, Guid? eventId = null, string reason = "Roadside inspection: brake defect")
    {
        var e = new DriverOutOfServiceChangedIntegrationEvent(
            Guid.NewGuid(), TestNotifications.TenantId, DriverId, status, NoticedAt, untilUtc, reason);
        return eventId is { } id ? e with { EventId = id } : e;
    }

    private static IReadOnlyList<string> Addressees(FakeEmailSender sender) =>
        sender.Batches.SelectMany(b => b).Select(m => m.To).Order(StringComparer.Ordinal).ToList();

    // ---- Audience ----

    [Fact]
    public async Task An_ELD_malfunction_goes_to_compliance_access_only()
    {
        SeedRoster();

        await EldHandler.Handle(Eld(), CancellationToken.None);

        // Owner and ComplianceOfficer; never Dispatcher, Supervisor, Accountant or Driver.
        Assert.Equal(["co@northernlink.ca", "owner@northernlink.ca"], Addressees(_sender));
    }

    [Fact]
    public async Task An_out_of_service_declaration_goes_to_compliance_and_dispatch()
    {
        SeedRoster();

        await OosHandler.Handle(Oos(), CancellationToken.None);

        Assert.Equal(
            ["co@northernlink.ca", "dispatch@northernlink.ca", "owner@northernlink.ca", "super@northernlink.ca"],
            Addressees(_sender));
    }

    [Fact]
    public void The_out_of_service_audience_names_the_owner_once()
    {
        // Owner is in both ComplianceAccess and DispatchAccess; a naive concat would email twice.
        Assert.Equal(
            DriverOutOfServiceChangedIntegrationEventHandler.Audience.Length,
            DriverOutOfServiceChangedIntegrationEventHandler.Audience.Distinct().Count());
        Assert.Contains(Roles.Owner, DriverOutOfServiceChangedIntegrationEventHandler.Audience);
        Assert.Contains(Roles.ComplianceOfficer, DriverOutOfServiceChangedIntegrationEventHandler.Audience);
        Assert.Contains(Roles.Dispatcher, DriverOutOfServiceChangedIntegrationEventHandler.Audience);
        Assert.Contains(Roles.Supervisor, DriverOutOfServiceChangedIntegrationEventHandler.Audience);
        Assert.DoesNotContain(Roles.Driver, DriverOutOfServiceChangedIntegrationEventHandler.Audience);
        Assert.DoesNotContain(Roles.Accountant, DriverOutOfServiceChangedIntegrationEventHandler.Audience);
    }

    [Fact]
    public async Task Inactive_users_and_other_tenants_are_never_addressed()
    {
        SeedRoster();

        await EldHandler.Handle(Eld(), CancellationToken.None);
        await OosHandler.Handle(Oos(), CancellationToken.None);

        var all = Addressees(_sender);
        Assert.DoesNotContain("gone@northernlink.ca", all);
        Assert.DoesNotContain("other@example.org", all);
    }

    [Fact]
    public async Task No_recipients_means_nothing_is_sent_and_nothing_is_recorded()
    {
        // A tenant with no compliance officer yet is a configuration fact, not a dead letter:
        // the handler returns normally so the consumer marks the row delivered.
        _users.Seed(Roles.Dispatcher, "dispatch@northernlink.ca");

        await EldHandler.Handle(Eld(), CancellationToken.None);

        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    // ---- Idempotency ----

    [Fact]
    public async Task Handling_the_same_ELD_event_twice_sends_once()
    {
        SeedRoster();
        var integrationEvent = Eld();

        await EldHandler.Handle(integrationEvent, CancellationToken.None);
        await EldHandler.Handle(integrationEvent, CancellationToken.None);

        Assert.Single(_sender.Batches);
        Assert.Single(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Handling_the_same_out_of_service_event_twice_sends_once()
    {
        SeedRoster();
        var integrationEvent = Oos();

        await OosHandler.Handle(integrationEvent, CancellationToken.None);
        await OosHandler.Handle(integrationEvent, CancellationToken.None);

        Assert.Single(_sender.Batches);
        Assert.Single(_dispatches.Dispatches);
    }

    [Fact]
    public async Task An_open_and_a_resolved_alert_for_the_same_report_are_two_dispatches()
    {
        SeedRoster();

        await EldHandler.Handle(Eld("Open"), CancellationToken.None);
        await EldHandler.Handle(Eld("Resolved"), CancellationToken.None);

        Assert.Equal(2, _sender.Batches.Count);
        Assert.Equal(2, _dispatches.Dispatches.Count);
        Assert.NotEqual(_dispatches.Dispatches[0].Id, _dispatches.Dispatches[1].Id);
    }

    /// <summary>The event id behind the golden dispatch ids — fixed, because the contract is the value.</summary>
    private static readonly Guid GoldenEventId = Guid.Parse("4f1c9e2a-7b3d-4e8f-9a21-0c5d6e7f8a9b");

    /// <summary>
    /// SHA-256("compliance-alert:{kind}:4f1c9e2a7b3d4e8f9a210c5d6e7f8a9b:chunk:{i}"), first 16
    /// bytes read as a Guid — computed outside this codebase, not captured from the method
    /// under test. Change the seed string, the hash or the fold and a redelivered event mints
    /// fresh ids, finds no recorded dispatch and re-emails every officer.
    /// </summary>
    [Theory]
    [InlineData("eld-malfunction-open", 0, "4f75bd7e-affc-ddee-ec68-4edf3ddcb294")]
    [InlineData("eld-malfunction-open", 1, "2285ec95-bd8f-2873-140c-b9067450a4d0")]
    [InlineData("eld-malfunction-resolved", 0, "9a1d515f-0423-f5f4-3867-64dad0b6de9a")]
    [InlineData("out-of-service-active", 0, "d0e1e2c4-20d6-9af1-62e1-ad1d756c29a2")]
    [InlineData("out-of-service-active", 1, "b91361ff-2049-8b37-f6cd-0112f4a2f006")]
    public void Dispatch_ids_match_their_golden_values(string kind, int chunkIndex, string expected)
    {
        Assert.Equal(Guid.Parse(expected), ComplianceAlertMailer.DeterministicDispatchId(kind, GoldenEventId, chunkIndex));
    }

    [Fact]
    public async Task A_sent_ELD_alert_lands_on_its_golden_id()
    {
        // Pins the wiring: kind constant, the event's own EventId, chunk 0, in that order.
        SeedRoster();

        await EldHandler.Handle(Eld(eventId: GoldenEventId), CancellationToken.None);

        Assert.Equal(Guid.Parse("4f75bd7e-affc-ddee-ec68-4edf3ddcb294"), Assert.Single(_dispatches.Dispatches).Id);
    }

    [Fact]
    public async Task A_sent_out_of_service_alert_lands_on_its_golden_id()
    {
        SeedRoster();

        await OosHandler.Handle(Oos(eventId: GoldenEventId), CancellationToken.None);

        Assert.Equal(Guid.Parse("d0e1e2c4-20d6-9af1-62e1-ad1d756c29a2"), Assert.Single(_dispatches.Dispatches).Id);
    }

    [Fact]
    public async Task More_than_sixteen_officers_chunk_and_a_redelivery_sends_only_the_unrecorded_tail()
    {
        for (var i = 1; i <= 20; i++)
        {
            _users.Seed(Roles.ComplianceOfficer, $"co{i:00}@northernlink.ca");
        }

        var integrationEvent = Eld(eventId: GoldenEventId);
        _dispatches.Add(EmailDispatch.RecordComplianceAlert(
            Guid.Parse("4f75bd7e-affc-ddee-ec68-4edf3ddcb294"), TestNotifications.TenantId, "already",
            [new DispatchRecipient { Email = "x@northernlink.ca", PassengerName = "x", Status = DispatchRecipientStatus.Sent }]).Value);

        await EldHandler.Handle(integrationEvent, CancellationToken.None);

        // Chunk 0 (co01..co16) was on record; only chunk 1 (co17..co20) goes out.
        var batch = Assert.Single(_sender.Batches);
        Assert.Equal(4, batch.Count);
        Assert.Equal("co17@northernlink.ca", batch[0].To);
        Assert.Equal(2, _dispatches.Dispatches.Count);
        Assert.Equal(Guid.Parse("2285ec95-bd8f-2873-140c-b9067450a4d0"), _dispatches.Dispatches[1].Id);
    }

    // ---- Content ----

    [Fact]
    public async Task The_ELD_open_email_names_the_driver_code_local_time_and_the_paper_log_rule()
    {
        SeedRoster();

        await EldHandler.Handle(Eld(), CancellationToken.None);

        var email = Assert.Single(_sender.Batches)[0];
        Assert.Equal("ELD malfunction reported — Doris Spence (P)", email.Subject);
        Assert.Contains("Doris Spence", email.HtmlBody);
        Assert.Contains("Monday, September 14, 2026 at 15:00 (Winnipeg time)", email.HtmlBody);
        Assert.Contains("Positioning light stayed red after the restart &amp; no GPS fix.", email.HtmlBody);
        Assert.Contains("Paper logs are expected until resolved (s.78); resolve it in the Drivers &amp; Compliance app.", email.HtmlBody);
        Assert.Contains("resolve it in the Drivers & Compliance app.", email.TextBody);
    }

    [Fact]
    public async Task The_ELD_resolved_email_is_the_short_form()
    {
        SeedRoster();

        await EldHandler.Handle(Eld("Resolved"), CancellationToken.None);

        var email = Assert.Single(_sender.Batches)[0];
        Assert.Equal("ELD malfunction resolved — Doris Spence (P)", email.Subject);
        Assert.Contains("has been resolved", email.HtmlBody);
        Assert.DoesNotContain("Paper logs are expected", email.HtmlBody);

        var dispatch = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(EldMalfunctionChangedIntegrationEventHandler.ResolvedTemplateName, dispatch.TemplateName);
    }

    [Fact]
    public async Task An_unknown_ELD_status_sends_nothing()
    {
        SeedRoster();

        await EldHandler.Handle(Eld("Escalated"), CancellationToken.None);

        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    [Fact]
    public async Task The_out_of_service_subject_states_the_end_or_until_lifted_and_the_reason()
    {
        SeedRoster();

        await OosHandler.Handle(Oos(untilUtc: NoticedAt.AddHours(72)), CancellationToken.None);
        await OosHandler.Handle(Oos(untilUtc: null), CancellationToken.None);

        Assert.Equal(
            "Driver placed out of service — until Thursday, September 17, 2026 at 15:00 (Winnipeg time): Roadside inspection: brake defect",
            _sender.Batches[0][0].Subject);
        Assert.Equal(
            "Driver placed out of service — until lifted: Roadside inspection: brake defect",
            _sender.Batches[1][0].Subject);
        Assert.Contains("Do not assign, claim or start a trip", _sender.Batches[0][0].HtmlBody);
        Assert.Contains(DriverId.ToString("D"), _sender.Batches[0][0].HtmlBody);
    }

    [Fact]
    public async Task A_long_reason_is_cut_in_the_subject_but_whole_in_the_body()
    {
        SeedRoster();
        var reason = new string('x', 300);

        await OosHandler.Handle(Oos(reason: reason), CancellationToken.None);

        var email = Assert.Single(_sender.Batches)[0];
        Assert.EndsWith("…", email.Subject);
        Assert.True(email.Subject.Length < 200);
        Assert.Contains(reason, email.HtmlBody);
    }

    [Fact]
    public async Task A_lifted_declaration_sends_nothing()
    {
        SeedRoster();

        await OosHandler.Handle(Oos("Lifted"), CancellationToken.None);

        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    // ---- The record ----

    [Fact]
    public async Task The_dispatch_is_recorded_as_a_compliance_alert_with_each_officer_s_outcome()
    {
        SeedRoster();

        await EldHandler.Handle(Eld(), CancellationToken.None);

        var dispatch = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(NotificationServiceType.ComplianceAlert, dispatch.ServiceType);
        Assert.Equal(EldMalfunctionChangedIntegrationEventHandler.OpenTemplateName, dispatch.TemplateName);
        Assert.Null(dispatch.TripId);
        Assert.Null(dispatch.ClientId);
        Assert.Null(dispatch.BookingId);
        Assert.Equal(EmailDispatchStatus.Sent, dispatch.Status);
        Assert.Equal(2, dispatch.Recipients.Count);

        // The name when set, the email when not — the replica's fallback rule.
        Assert.Equal("Owner Person", Assert.Single(dispatch.Recipients, r => r.Email == "owner@northernlink.ca").PassengerName);
        Assert.Equal("co@northernlink.ca", Assert.Single(dispatch.Recipients, r => r.Email == "co@northernlink.ca").PassengerName);
        Assert.Equal(1, _dispatches.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_provider_failure_is_recorded_not_thrown()
    {
        SeedRoster();
        _sender.OutcomeFor = (_, _) => new EmailSendOutcome(false, "Postmark.Unreachable", "down", null);

        await EldHandler.Handle(Eld(), CancellationToken.None);

        var dispatch = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(EmailDispatchStatus.Failed, dispatch.Status);
        Assert.All(dispatch.Recipients, r => Assert.Equal("Postmark.Unreachable", r.ErrorCode));
    }

    [Fact]
    public void A_compliance_alert_dispatch_needs_a_template_name_and_recipients()
    {
        var recipient = new DispatchRecipient { Email = "co@northernlink.ca", PassengerName = "CO", Status = DispatchRecipientStatus.Sent };

        Assert.Equal(
            EmailDispatchErrors.TemplateNameRequired,
            EmailDispatch.RecordComplianceAlert(Guid.NewGuid(), TestNotifications.TenantId, " ", [recipient]).Error);
        Assert.Equal(
            EmailDispatchErrors.NoRecipients,
            EmailDispatch.RecordComplianceAlert(Guid.NewGuid(), TestNotifications.TenantId, "Built-in: x", []).Error);
    }
}
