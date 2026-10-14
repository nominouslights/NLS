namespace NorthernLink.Compliance.Application.Printing;

/// <summary>
/// The page frame every printed compliance record shares: the carrier's legal name and NSC
/// number in the header, and a footer of
/// <c>Record {id} · {documentNumber} · v{version} · generated {timestamp} · page x of y</c>.
/// </summary>
public sealed record RecordPrintFrame(
    string CarrierLegalName,
    string NscNumber,
    string PrincipalPlaceOfBusiness,
    Guid RecordId,
    string DocumentNumber,
    int DocumentVersion,
    DateTimeOffset GeneratedAtUtc);

/// <summary>A labelled line on a printed sheet — every value already formatted.</summary>
public sealed record PrintField(string Label, string Value);

/// <summary>One regulatory-basis line on a printed sheet.</summary>
public sealed record PrintBasisLine(string Key, string Citation, string Title, string PlainExplanation);

/// <summary>
/// What a template renders: the frame, the sheet title, the record's fields, its retention
/// block and the rules it exists for. <see cref="TemplateKey"/> selects the layout
/// (<c>record-cover</c> today).
/// </summary>
public sealed record RecordPrintDocument(
    RecordPrintFrame Frame,
    string TemplateKey,
    string Title,
    IReadOnlyList<PrintField> Fields,
    IReadOnlyList<PrintField> Retention,
    IReadOnlyList<PrintBasisLine> Basis);

/// <summary>The rendered PDF and the file name a browser should save it under.</summary>
public sealed record RecordPrintResult(byte[] Content, string FileName);
