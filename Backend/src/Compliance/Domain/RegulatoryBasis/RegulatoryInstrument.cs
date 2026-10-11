namespace NorthernLink.Compliance.Domain.RegulatoryBasis;

/// <summary>
/// What kind of instrument a <see cref="RegulatoryBasis"/> entry quotes. The three statutory
/// kinds (Act, NSC standard, regulation) carry legal force; the two guidance kinds are the
/// administrator's own how-to pages and forms, cited so a screen can link "where to get the
/// form" beside "why you need it"; <see cref="SafetyPlan"/> and <see cref="CompanyPolicy"/> are
/// Northern Link's own documents.
/// </summary>
public enum RegulatoryInstrument
{
    /// <summary>The Highway Traffic Act, C.C.S.M. c. H60.</summary>
    HTA,

    /// <summary>A National Safety Code standard (CCMTA).</summary>
    NSC,

    /// <summary>A Manitoba regulation (M.R. nnn/yyyy).</summary>
    ManitobaRegulation,

    /// <summary>A federal regulation (SOR/yyyy-nnn).</summary>
    FederalRegulation,

    /// <summary>Safety Plan NL-SP-02 itself.</summary>
    SafetyPlan,

    /// <summary>A Northern Link policy document other than the Safety Plan.</summary>
    CompanyPolicy,

    /// <summary>Manitoba Transportation and Infrastructure / MPI guidance pages and forms.</summary>
    ManitobaGuidance,

    /// <summary>Transport Canada guidance pages and lists.</summary>
    FederalGuidance,
}
