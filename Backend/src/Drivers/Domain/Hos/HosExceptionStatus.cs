namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// Lifecycle of a <see cref="HosException"/>. The first three <b>apply</b> — the rule engine
/// honours them; <see cref="Rejected"/> and <see cref="Withdrawn"/> stop applying the moment
/// they are set. Member names travel on the wire verbatim.
/// </summary>
public enum HosExceptionStatus
{
    /// <summary>Declared by the driver (or dispatch on their behalf); applies immediately.</summary>
    Declared,

    /// <summary>Dispatch or compliance has seen the declaration.</summary>
    Acknowledged,

    /// <summary>A compliance officer approved it (the only status a deferral is ever created in).</summary>
    Approved,

    /// <summary>A compliance officer refused it; it no longer applies.</summary>
    Rejected,

    /// <summary>Withdrawn by the declarer (while still Declared) or by compliance; it no longer applies.</summary>
    Withdrawn,
}
