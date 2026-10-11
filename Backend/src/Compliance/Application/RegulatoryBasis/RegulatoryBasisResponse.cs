using NorthernLink.Compliance.Domain.RegulatoryBasis;

namespace NorthernLink.Compliance.Application.RegulatoryBasis;

/// <summary>One catalogue entry on the wire.</summary>
public sealed record RegulatoryBasisResponse(
    string Key,
    string Jurisdiction,
    string Instrument,
    string Citation,
    string Title,
    string? Quotation,
    string PlainExplanation,
    string? Consequence,
    string? SourceUrl,
    string? PlanCitationNote,
    DateOnly EffectiveFrom,
    int RuleSetVersion)
{
    public static RegulatoryBasisResponse From(Domain.RegulatoryBasis.RegulatoryBasis basis) => new(
        basis.Key,
        basis.Jurisdiction,
        basis.Instrument.ToString(),
        basis.Citation,
        basis.Title,
        basis.Quotation,
        basis.PlainExplanation,
        basis.Consequence,
        basis.SourceUrl,
        basis.PlanCitationNote,
        basis.EffectiveFrom,
        basis.RuleSetVersion);
}
