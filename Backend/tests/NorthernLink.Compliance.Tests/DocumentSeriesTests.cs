using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Application.Records.Register;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// The document series: the instance-number format, and — through the registrar — gap-free
/// numbering under a lost optimistic-concurrency race.
/// </summary>
public class DocumentSeriesTests
{
    private readonly InMemoryComplianceStore _store = new();
    private readonly RegisterRecordCommandHandler _register;
    private readonly int _year = DateTimeOffset.UtcNow.Year;

    public DocumentSeriesTests()
    {
        _register = new RegisterRecordCommandHandler(new RecordRegistrar(_store, _store, _store));
    }

    private Task<Result<RegisterRecordResult>> RegisterAsync(string sourceRecordId, ComplianceRecordType type = ComplianceRecordType.R9) =>
        _register.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, type, TestCompliance.Registration(sourceRecordId: sourceRecordId), TestCompliance.ActorId),
            CancellationToken.None);

    [Fact]
    public void Instance_numbers_follow_prefix_series_form_year_sequence()
    {
        Assert.Equal("NL-HS-04-2026-0031", DocumentNumberFormat.Compose("NL", DocumentSeriesCode.HS, "04", 2026, 31));
        Assert.Equal("NL-VM-R11-2026-0003", DocumentNumberFormat.Compose("NL", DocumentSeriesCode.VM, "R11", 2026, 3));
    }

    [Fact]
    public void Form_codes_keep_their_plan_shape_and_yield_a_two_digit_form_number()
    {
        Assert.Equal("04", DocumentNumberFormat.FormNumberOf("NL-HS-04"));
        Assert.Equal("01", DocumentNumberFormat.FormNumberOf("NL-SP-01-A"));
        Assert.Null(DocumentNumberFormat.FormNumberOf("NL-HS"));
    }

    [Fact]
    public void A_series_issues_consecutive_numbers_from_one()
    {
        var series = DocumentSeries.Open(TestCompliance.TenantId, DocumentSeriesCode.HS, 2026);

        Assert.Equal("NL-HS-04-2026-0001", series.IssueNext("NL", "04"));
        Assert.Equal("NL-HS-01-2026-0002", series.IssueNext("NL", "01"));
        Assert.Equal(2, series.LastSequence);
        Assert.Equal(3, series.DomainEvents.Count); // opened + 2 issued
    }

    [Fact]
    public async Task Consecutive_registrations_get_consecutive_numbers_in_the_type_series()
    {
        var first = await RegisterAsync("inspection:a");
        var second = await RegisterAsync("inspection:b");

        Assert.Equal($"NL-VM-03-{_year}-0001", first.Value.DocumentNumber);
        Assert.Equal($"NL-VM-03-{_year}-0002", second.Value.DocumentNumber);
        Assert.True(first.Value.Created);
        Assert.Equal(2, _store.CommittedLastSequence(DocumentSeriesCode.VM, _year));
    }

    [Fact]
    public async Task A_type_with_no_plan_form_uses_its_R_code_as_the_form_number()
    {
        var result = await RegisterAsync("pmvi:1", ComplianceRecordType.R11);

        Assert.Equal($"NL-VM-R11-{_year}-0001", result.Value.DocumentNumber);
    }

    [Fact]
    public async Task Two_concurrent_registrations_get_consecutive_numbers_and_the_loser_retries_rather_than_skipping()
    {
        // Request A loads the series (committed version 1, last 1 after the warm-up) and, before
        // it saves, request B — its own unit of work over the same committed state — runs to
        // completion on the same series and takes 0002. A's save must then fail the version
        // check, A must reload (last 2) and take 0003 — never 0002 twice, never a jump to 0004.
        Result<RegisterRecordResult>? bResult = null;
        await RegisterAsync("warm-up"); // opens the VM series so both requests load an existing row
        var requestB = new InMemoryComplianceStore(_store.State);
        var registerB = new RegisterRecordCommandHandler(new RecordRegistrar(requestB, requestB, requestB));
        _store.BeforeNextSave = async () => bResult = await registerB.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, ComplianceRecordType.R9, TestCompliance.Registration(sourceRecordId: "inspection:b"), TestCompliance.ActorId),
            CancellationToken.None);

        var aResult = await RegisterAsync("inspection:a");

        Assert.NotNull(bResult);
        Assert.True(aResult.IsSuccess, aResult.IsFailure ? aResult.Error.Code : null);
        Assert.True(bResult.IsSuccess);
        Assert.Equal($"NL-VM-03-{_year}-0002", bResult.Value.DocumentNumber);
        Assert.Equal($"NL-VM-03-{_year}-0003", aResult.Value.DocumentNumber);
        Assert.Equal(1, _store.ConcurrencyConflicts);
        Assert.Equal(3, _store.CommittedLastSequence(DocumentSeriesCode.VM, _year));
        Assert.Equal(3, _store.Records.Count);
        Assert.Equal(3, _store.Records.Select(r => r.DocumentNumber).Distinct().Count());
    }

    [Fact]
    public async Task A_registration_refused_by_validation_consumes_no_number()
    {
        var refused = await _register.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, ComplianceRecordType.R9, TestCompliance.Registration(title: "  "), TestCompliance.ActorId),
            CancellationToken.None);
        var next = await RegisterAsync("inspection:a");

        Assert.Equal("Compliance.Record.TitleRequired", refused.Error.Code);
        Assert.Equal($"NL-VM-03-{_year}-0001", next.Value.DocumentNumber);
    }

    [Fact]
    public async Task Different_series_count_independently()
    {
        var inspection = await RegisterAsync("inspection:a", ComplianceRecordType.R9);
        var audit = await RegisterAsync("hos-audit:2026-09", ComplianceRecordType.R7);

        Assert.Equal($"NL-VM-03-{_year}-0001", inspection.Value.DocumentNumber);
        Assert.Equal($"NL-HS-01-{_year}-0001", audit.Value.DocumentNumber);
    }

    [Fact]
    public async Task The_prefix_comes_from_tenant_settings()
    {
        _store.Settings.Add(TenantComplianceSettings.Create(TestCompliance.TenantId, new ComplianceSettingsDetails
        {
            CarrierLegalName = "Northern Link Shuttle & Cargo",
            DocumentPrefix = "nlsc",
            PrincipalPlaceOfBusiness = "Leaf Rapids, Manitoba",
        }, null).Value);

        var result = await RegisterAsync("inspection:a");

        Assert.StartsWith($"NLSC-VM-03-{_year}-", result.Value.DocumentNumber);
    }

    [Fact]
    public async Task An_unseeded_tenant_gets_default_settings_created_by_the_first_registration()
    {
        Assert.Empty(_store.Settings);

        await RegisterAsync("inspection:a");

        var settings = Assert.Single(_store.Settings);
        Assert.Equal("NL", settings.DocumentPrefix);
        Assert.Equal("Leaf Rapids, Manitoba", settings.PrincipalPlaceOfBusiness);
    }
}
