using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Insurance;

/// <summary>Errors of insurance policies (NL-SP-02 s. 12.1; HTA ss. 312.2(7), 312.4; M.R. 132/2024 s. 7(1)(b)).</summary>
public static class InsuranceErrors
{
    public static readonly Error NotFound = ComplianceError.NotFound(
        "Compliance.Insurance.NotFound", "The insurance policy was not found.",
        BasisKeys.Hta312_2_7, BasisKeys.Plan12_1);

    public static readonly Error CurrentExists = ComplianceError.Conflict(
        "Compliance.Insurance.CurrentExists",
        "This vehicle already has a current insurance policy. Renew it (POST insurance-policies/{id}/renew) so the old certificate is kept as superseded.",
        BasisKeys.Hta312_2_7, BasisKeys.Plan12_1, BasisKeys.Plan11);

    public static readonly Error NotCurrent = ComplianceError.Conflict(
        "Compliance.Insurance.NotCurrent", "Only the vehicle's current policy can be renewed.",
        BasisKeys.Plan12_1, BasisKeys.Plan11);

    public static readonly Error InsurerTooLong = ComplianceError.Validation(
        "Compliance.Insurance.InsurerTooLong", $"The insurer's name must be {InsurancePolicy.InsurerMaxLength} characters or fewer.",
        BasisKeys.Plan12_1);

    public static readonly Error PolicyNumberRequired = ComplianceError.Validation(
        "Compliance.Insurance.PolicyNumberRequired",
        $"Enter the policy number as printed on the certificate (up to {InsurancePolicy.PolicyNumberMaxLength} characters).",
        BasisKeys.Hta312_2_7, BasisKeys.Plan12_1);

    public static readonly Error LiabilityLimitRequired = ComplianceError.Validation(
        "Compliance.Insurance.LiabilityLimitRequired",
        "Enter the third-party liability limit in dollars (a limit below the $2,000,000 minimum is saved and flagged, not refused).",
        BasisKeys.Mr132_2024_7_1_b, BasisKeys.Plan12_1);

    public static readonly Error CargoCoverageInvalid = ComplianceError.Validation(
        "Compliance.Insurance.CargoCoverageInvalid", "Cargo coverage cannot be negative — leave it empty when the certificate has none.",
        BasisKeys.Plan12_1);

    public static readonly Error DatesRequired = ComplianceError.Validation(
        "Compliance.Insurance.DatesRequired", "Enter the certificate's effective and expiry dates.",
        BasisKeys.Hta312_2_7, BasisKeys.Plan12_1);

    public static readonly Error ExpiryNotAfterEffective = ComplianceError.Validation(
        "Compliance.Insurance.ExpiryNotAfterEffective", "The expiry date must be after the effective date.",
        BasisKeys.Hta312_2_7, BasisKeys.Plan12_1);
}
