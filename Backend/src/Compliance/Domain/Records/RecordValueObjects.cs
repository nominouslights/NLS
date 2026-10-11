namespace NorthernLink.Compliance.Domain.Records;

/// <summary>What a record is about: a driver, a vehicle, a trip, an incident, or the carrier itself.</summary>
/// <param name="Id">The subject's id in its owning module; null for <see cref="RecordSubjectKind.Carrier"/>.</param>
/// <param name="Display">What a person reads — "Unit 12 (2019 Ford Transit)", "J. Smith", "NL-2026-0412".</param>
public sealed record RecordSubject(RecordSubjectKind Kind, Guid? Id, string Display)
{
    public const int DisplayMaxLength = 200;
}

/// <summary>A pointer to a document the record indexes — the registry never stores the file itself.</summary>
/// <param name="Kind">What <paramref name="Reference"/> is: "url", "storage-key", "fleet-document", "drivers-credential", …</param>
public sealed record SupportingLink(string Kind, string Reference, string Label)
{
    public const int KindMaxLength = 32;
    public const int ReferenceMaxLength = 1024;
    public const int LabelMaxLength = 200;
}

/// <summary>A legal/litigation hold: while one exists the record never reaches ReviewForDisposal.</summary>
public sealed record RecordHold(Guid HoldId, string Reason, Guid? PlacedBy, DateTimeOffset PlacedAtUtc)
{
    public const int ReasonMaxLength = 500;
}

/// <summary>
/// Everything a registration (or a new version) supplies about a record. Raw input — the
/// aggregate trims and validates. <see cref="PrintTemplateKey"/> null means the record type's
/// default template; <see cref="RegulatoryBasisKeys"/> are added to the type's own keys.
/// </summary>
public sealed record RecordRegistration
{
    public string? Title { get; init; }
    public RecordSubject? Subject { get; init; }
    public string? SourceModule { get; init; }
    public string? SourceRecordId { get; init; }
    public IReadOnlyDictionary<AnchorKind, DateOnly>? AnchorDates { get; init; }
    public IReadOnlyList<SupportingLink>? SupportingLinks { get; init; }
    public IReadOnlyList<string>? RegulatoryBasisKeys { get; init; }
    public string? PrintTemplateKey { get; init; }
}
