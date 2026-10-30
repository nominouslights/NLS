using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Numbering;

public static class DocumentSeriesErrors
{
    /// <summary>
    /// Every retry of the issue-and-save loop lost the version race. Practically unreachable at
    /// this carrier's volume; surfaced as a 409 rather than a 500 so the caller simply retries.
    /// </summary>
    public static readonly Error ConcurrencyExhausted = ComplianceError.Conflict(
        "Compliance.DocumentSeries.ConcurrencyExhausted",
        "Another registration took the next document number every time this one tried. Please retry.",
        BasisKeys.Hta318_8_1, BasisKeys.Plan11);
}
