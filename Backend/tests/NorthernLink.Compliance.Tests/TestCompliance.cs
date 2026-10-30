using NorthernLink.Compliance.Domain.Records;

namespace NorthernLink.Compliance.Tests;

/// <summary>Shared builders for Compliance tests.</summary>
internal static class TestCompliance
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>A stand-in for an authenticated user id, for the registered_by / placed_by paths.</summary>
    public static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly Guid VehicleId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>A valid registration for a trip inspection report (R9), every field overridable.</summary>
    public static RecordRegistration Registration(
        string? title = "Trip inspection — Unit 12 — 2026-09-14",
        RecordSubject? subject = null,
        string? sourceModule = "fleet",
        string? sourceRecordId = "inspection:7f3c",
        IReadOnlyDictionary<AnchorKind, DateOnly>? anchors = null,
        IReadOnlyList<SupportingLink>? links = null,
        IReadOnlyList<string>? basisKeys = null,
        string? printTemplateKey = null) => new()
        {
            Title = title,
            Subject = subject ?? new RecordSubject(RecordSubjectKind.Vehicle, VehicleId, "Unit 12 (2019 Ford Transit)"),
            SourceModule = sourceModule,
            SourceRecordId = sourceRecordId,
            AnchorDates = anchors ?? Anchors((AnchorKind.RecordDate, new DateOnly(2026, 9, 14))),
            SupportingLinks = links,
            RegulatoryBasisKeys = basisKeys,
            PrintTemplateKey = printTemplateKey,
        };

    public static IReadOnlyDictionary<AnchorKind, DateOnly> Anchors(params (AnchorKind Kind, DateOnly Date)[] anchors) =>
        anchors.ToDictionary(a => a.Kind, a => a.Date);

    /// <summary>A registered record straight from the aggregate (no handler), events cleared.</summary>
    public static ComplianceRecord Record(
        ComplianceRecordType type = ComplianceRecordType.R9,
        RecordRegistration? registration = null,
        string documentNumber = "NL-VM-03-2026-0001")
    {
        var definition = ComplianceRecordCatalogue.For(type);
        var result = ComplianceRecord.Register(TenantId, definition, documentNumber, registration ?? Registration(), ActorId, DateTimeOffset.UtcNow);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test record invalid: {result.Error.Code}");
        }

        result.Value.ClearDomainEvents();
        return result.Value;
    }
}
