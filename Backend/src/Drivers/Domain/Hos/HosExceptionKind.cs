namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// The three exceptions the regulation allows against the hours limits. Member names match the
/// rule engine's derived <c>HosException</c> record types one-for-one (<c>AdverseDrivingConditions</c>,
/// <c>Emergency</c>, <c>OffDutyDeferral</c>); the wire carries the <c>HosWire</c> kind strings.
/// </summary>
public enum HosExceptionKind
{
    /// <summary>s.76 — up to two extra hours in the shift and day it was declared in.</summary>
    AdverseDrivingConditions,

    /// <summary>s.76 — violations inside the span are reported suppressed, never erased.</summary>
    Emergency,

    /// <summary>s.16 — up to two hours of day one's off-duty time deferred to day two; CO-approved only.</summary>
    OffDutyDeferral,
}
