namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>
/// How GST applies to amounts tagged with a budget code. Manitoba charges no PST on
/// transportation services, so GST at 5% is the only tax in play and one enum covers it.
/// <para>
/// This is a planning annotation, not a tax engine: nothing here computes or remits anything.
/// The authoritative tax treatment of an actual dollar is whatever QuickBooks recorded — the
/// platform never calls the QBO API (see <c>Invoice</c>). Nothing downstream reads this to
/// compute tax, and nothing may: invoicing carries no tax fields at all, by design.
/// </para>
/// </summary>
public enum BudgetTaxTreatment
{
    /// <summary>GST is charged on this code's amounts (5% in Manitoba).</summary>
    GstApplicable,

    /// <summary>Taxable at 0% — GST-registered, but the rate is nil.</summary>
    ZeroRated,

    /// <summary>Outside the GST system entirely.</summary>
    Exempt,

    /// <summary>Tax is not a meaningful concept for this code — most internal expense codes.</summary>
    NotApplicable,
}
