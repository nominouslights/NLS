using NorthernLink.Compliance.Domain.Signatures;

namespace NorthernLink.Compliance.Domain.Authority;

/// <summary>The rating the Motor Carrier Division gives a carrier on its SFC. A new entrant is unaudited.</summary>
public enum SafetyRating
{
    Unaudited,
    Satisfactory,
    Conditional,
    Unsatisfactory,
}

/// <summary>Lifecycle of a renewable authority document (insurance, registration): one Current per vehicle, the rest Superseded.</summary>
public enum AuthorityDocumentStatus
{
    Current,
    Superseded,
}

/// <summary>
/// One Safety Fitness Certificate as recorded — the current one, or a superseded one kept in the
/// authority's history. <see cref="SourceRecordId"/> is how the record registry knows it (R20):
/// the record-produced event carries it, and the registered event's answer is stamped back as
/// <see cref="ComplianceRecordId"/> / <see cref="DocumentNumber"/> (null until then — a print
/// shows "number pending").
/// </summary>
public sealed record SfcCertificate(
    Guid CertificateId,
    string Number,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    AttachmentRef? Attachment,
    string SourceRecordId,
    Guid? ComplianceRecordId,
    string? DocumentNumber,
    DateOnly? SupersededOn)
{
    public const int NumberMaxLength = 32;

    /// <summary>A new certificate with a fresh id; the caller has validated the number and dates.</summary>
    public static SfcCertificate New(string number, DateOnly issuedOn, DateOnly expiresOn)
    {
        var id = Guid.NewGuid();
        return new SfcCertificate(id, number, issuedOn, expiresOn, null, ComplianceSourceIds.Sfc(id), null, null, null);
    }

    /// <summary>True on <paramref name="asOf"/> when the certificate is in force (issued and not yet expired).</summary>
    public bool IsValidOn(DateOnly asOf) => IssuedOn <= asOf && asOf <= ExpiresOn;
}

/// <summary>
/// The producer-side ids under which this module registers its <em>own</em> documents in the
/// record registry (source module <see cref="Module"/>). The prefix is what routes a
/// <c>ComplianceRecordRegisteredIntegrationEvent</c> answer back to the aggregate to stamp.
/// </summary>
public static class ComplianceSourceIds
{
    public const string Module = "compliance";

    public const string SfcPrefix = "sfc:";
    public const string InsurancePolicyPrefix = "insurance-policy:";
    public const string VehicleRegistrationPrefix = "vehicle-registration:";

    public static string Sfc(Guid certificateId) => $"{SfcPrefix}{certificateId:N}";

    public static string InsurancePolicy(Guid policyId) => $"{InsurancePolicyPrefix}{policyId:N}";

    public static string VehicleRegistration(Guid registrationId) => $"{VehicleRegistrationPrefix}{registrationId:N}";

    /// <summary>The id after <paramref name="prefix"/>, or null when the source id is not of that kind.</summary>
    public static Guid? IdAfter(string prefix, string sourceRecordId) =>
        sourceRecordId.StartsWith(prefix, StringComparison.Ordinal)
        && Guid.TryParseExact(sourceRecordId[prefix.Length..], "N", out var id)
            ? id
            : null;
}

/// <summary>
/// Whether a vehicle is a <em>regulated vehicle</em> for this carrier. Owner question Q1's
/// default: a "bus" is designed to carry 11 or more persons including the driver (HTA s. 1(1)),
/// so seating at or above the rule set's <c>authority.regulatedMinSeatingCapacity</c> is
/// regulated; a Compliance-only per-vehicle override wins either way.
/// </summary>
public static class VehicleRegulation
{
    /// <summary>Fleet statuses (the event's strings) that take a vehicle out of the carrier's operating fleet.</summary>
    public static readonly IReadOnlySet<string> OutOfFleetStatuses = new HashSet<string>(StringComparer.Ordinal) { "Retired", "Sold", "Recycled" };

    public static bool IsRegulated(int seatingCapacity, bool? regulatedOverride, int minSeatingCapacity) =>
        regulatedOverride ?? seatingCapacity >= minSeatingCapacity;

    /// <summary>False for a retired, sold or recycled vehicle — it never counts toward carrier readiness.</summary>
    public static bool CountsForReadiness(string status) => !OutOfFleetStatuses.Contains(status);
}
