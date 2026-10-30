using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.RegulatoryBasis;

/// <summary>Errors of the regulatory-basis catalogue.</summary>
public static class RegulatoryBasisErrors
{
    /// <summary>
    /// NL-SP-02 s. 11: a record must be able to say which rule it exists for, so asking for a key
    /// the catalogue lacks is an error rather than an empty list.
    /// </summary>
    public static readonly Error NotFound = ComplianceError.NotFound(
        "Compliance.RegulatoryBasis.NotFound",
        "No regulatory-basis entry exists for one or more of the requested keys.",
        BasisKeys.Plan11);

    public static readonly Error KeysRequired = ComplianceError.Validation(
        "Compliance.RegulatoryBasis.KeysRequired",
        "At least one regulatory-basis key is required.",
        BasisKeys.Plan11);
}
