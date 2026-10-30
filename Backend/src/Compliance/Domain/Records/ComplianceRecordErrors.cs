using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Records;

/// <summary>All errors the ComplianceRecord aggregate and its handlers can produce — each grounded in a basis key.</summary>
public static class ComplianceRecordErrors
{
    public static readonly Error NotFound = ComplianceError.NotFound(
        "Compliance.Record.NotFound", "The compliance record was not found.",
        BasisKeys.Hta318_8_1);

    public static readonly Error TitleRequired = ComplianceError.Validation(
        "Compliance.Record.TitleRequired", "A record needs a title.",
        BasisKeys.Hta318_8_1, BasisKeys.Plan11);

    public static readonly Error TitleTooLong = ComplianceError.Validation(
        "Compliance.Record.TitleTooLong", $"The title must be {ComplianceRecord.TitleMaxLength} characters or fewer.",
        BasisKeys.Plan11);

    public static readonly Error SubjectRequired = ComplianceError.Validation(
        "Compliance.Record.SubjectRequired", "A record must say what it is about (carrier, driver, vehicle, trip or incident).",
        BasisKeys.Hta318_8_1, BasisKeys.Plan11);

    public static readonly Error SubjectDisplayTooLong = ComplianceError.Validation(
        "Compliance.Record.SubjectDisplayTooLong", $"The subject display must be {RecordSubject.DisplayMaxLength} characters or fewer.",
        BasisKeys.Plan11);

    public static readonly Error SourceRequired = ComplianceError.Validation(
        "Compliance.Record.SourceRequired", "A record needs a source module and a source record id — that pair is what makes registration idempotent.",
        BasisKeys.Hta318_8_1, BasisKeys.Plan11);

    public static readonly Error SourceTooLong = ComplianceError.Validation(
        "Compliance.Record.SourceTooLong", $"The source module must be {ComplianceRecord.SourceModuleMaxLength} and the source record id {ComplianceRecord.SourceRecordIdMaxLength} characters or fewer.",
        BasisKeys.Plan11);

    public static readonly Error SupportingLinkInvalid = ComplianceError.Validation(
        "Compliance.Record.SupportingLinkInvalid", "Every supporting link needs a kind, a reference and a label within their length limits.",
        BasisKeys.Hta318_8_2, BasisKeys.Plan11);

    public static readonly Error PrintTemplateKeyTooLong = ComplianceError.Validation(
        "Compliance.Record.PrintTemplateKeyTooLong", $"The print template key must be {ComplianceRecord.PrintTemplateKeyMaxLength} characters or fewer.",
        BasisKeys.Plan11);

    public static readonly Error AnchorNotApplicable = ComplianceError.Validation(
        "Compliance.Record.AnchorNotApplicable", "This record type's retention rule does not count from that anchor.",
        BasisKeys.Plan11);

    public static readonly Error HoldReasonRequired = ComplianceError.Validation(
        "Compliance.Record.HoldReasonRequired", "A hold needs a reason — it is what the destruction review reads later.",
        BasisKeys.Hta318_10_2, BasisKeys.Plan11);

    public static readonly Error HoldNotFound = ComplianceError.NotFound(
        "Compliance.Record.HoldNotFound", "No hold with that id is on this record.",
        BasisKeys.Plan11);

    public static readonly Error NotActive = ComplianceError.Conflict(
        "Compliance.Record.NotActive", "Only an active record can be changed, held or superseded.",
        BasisKeys.Plan11);

    public static readonly Error NotReviewable = ComplianceError.Conflict(
        "Compliance.Record.NotReviewable", "Only a record in review for disposal can be disposed. Its retention period has not ended, or it has already been disposed or superseded.",
        BasisKeys.Hta318_8_1, BasisKeys.Plan11);

    public static readonly Error Held = ComplianceError.Conflict(
        "Compliance.Record.Held", "A record under hold cannot be disposed. Release the hold first.",
        BasisKeys.Hta318_10_2, BasisKeys.Plan11);

    public static readonly Error Permanent = ComplianceError.Conflict(
        "Compliance.Record.Permanent", "This record type is kept permanently and is never disposed.",
        BasisKeys.Nsc15, BasisKeys.Plan11);

    public static readonly Error DisposalMethodRequired = ComplianceError.Validation(
        "Compliance.Record.DisposalMethodRequired", "Say how the record was destroyed (shredded, deleted, returned …) — the destruction log needs it.",
        BasisKeys.Plan11);

    public static readonly Error DisposalIdsRequired = ComplianceError.Validation(
        "Compliance.Record.DisposalIdsRequired", "Name at least one record to dispose.",
        BasisKeys.Plan11);

    public static readonly Error UnknownRecordType = ComplianceError.Validation(
        "Compliance.Record.UnknownRecordType", "The record type must be one of R1 to R21 from NL-SP-02 s. 11.",
        BasisKeys.Plan11);

    public static readonly Error UnknownSubjectKind = ComplianceError.Validation(
        "Compliance.Record.UnknownSubjectKind", "The subject kind must be Carrier, Driver, Vehicle, Trip or Incident.",
        BasisKeys.Plan11);

    public static readonly Error AnchorDateRequired = ComplianceError.Validation(
        "Compliance.Record.AnchorDateRequired", "An anchor needs a date.",
        BasisKeys.Plan11);

    public static readonly Error SweepDateInFuture = ComplianceError.Validation(
        "Compliance.Record.SweepDateInFuture", "A disposal-review sweep judges retention as of today or earlier — a future date would queue records whose retention has not ended. Use the due-for-disposal list to look ahead.",
        BasisKeys.Plan11);

    public static readonly Error UnknownAnchorKind = ComplianceError.Validation(
        "Compliance.Record.UnknownAnchorKind", "An anchor date must be keyed by a known anchor kind (RecordDate, ReceivedDate, MonthEnd, …).",
        BasisKeys.Plan11);
}
