namespace NorthernLink.Compliance.Domain.Numbering;

/// <summary>
/// The twelve document series of NL-SP-02's form codes (<c>NL-HS-01</c> is series HS, form 01).
/// A <see cref="DocumentSeries"/> row exists per tenant, series and year and hands out the
/// gap-free instance sequence that completes <c>NL-HS-04-2026-0031</c>.
/// </summary>
public enum DocumentSeriesCode
{
    /// <summary>Hours of service.</summary>
    HS,

    /// <summary>Compliance monitoring.</summary>
    CM,

    /// <summary>Driver qualification.</summary>
    DQ,

    /// <summary>Vehicle maintenance.</summary>
    VM,

    /// <summary>Freight.</summary>
    FR,

    /// <summary>Incident reporting.</summary>
    IR,

    /// <summary>Training.</summary>
    TR,

    /// <summary>Safety plan and policy.</summary>
    SP,

    /// <summary>Emergency preparedness.</summary>
    EP,

    /// <summary>Human resources.</summary>
    HR,

    /// <summary>Passenger manifests.</summary>
    PM,

    /// <summary>Equipment.</summary>
    EQ,
}

/// <summary>
/// The one place the instance-number format lives. Form codes stay <c>NL-HS-01</c>; an instance
/// is <c>{prefix}-{series}-{formNo}-{year}-{seq:0000}</c>, e.g. <c>NL-HS-04-2026-0031</c>. The
/// prefix comes from <c>TenantComplianceSettings</c>, never a constant.
/// </summary>
public static class DocumentNumberFormat
{
    public const int MaxLength = 40;

    public static string Compose(string prefix, DocumentSeriesCode series, string formNumber, int year, int sequence) =>
        $"{prefix}-{series}-{formNumber}-{year}-{sequence:0000}";

    /// <summary>
    /// The form number of a plan form code: <c>NL-HS-04</c> → "04", <c>NL-SP-01-A</c> → "01".
    /// Null when the code does not have the <c>XX-SS-NN</c> shape.
    /// </summary>
    public static string? FormNumberOf(string formCode)
    {
        var parts = formCode.Split('-');
        return parts.Length >= 3 && parts[2].Length > 0 && parts[2].All(char.IsDigit) ? parts[2] : null;
    }
}
