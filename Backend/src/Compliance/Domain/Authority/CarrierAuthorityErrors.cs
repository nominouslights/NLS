using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Authority;

/// <summary>Errors of the carrier authority (NL-SP-02 ss. 1–2; HTA s. 312.2; M.R. 132/2024 s. 9).</summary>
public static class CarrierAuthorityErrors
{
    public static readonly Error SfcNumberRequired = ComplianceError.Validation(
        "Compliance.Authority.SfcNumberRequired",
        $"Enter the safety fitness certificate number as printed on the certificate (up to {SfcCertificate.NumberMaxLength} characters).",
        BasisKeys.Hta312_2_1, BasisKeys.MtiSfc, BasisKeys.Plan2);

    public static readonly Error SfcDatesRequired = ComplianceError.Validation(
        "Compliance.Authority.SfcDatesRequired",
        "Enter the certificate's issue and expiry dates as printed on it.",
        BasisKeys.MtiSfc, BasisKeys.Plan2);

    public static readonly Error DeclarationDateRequired = ComplianceError.Validation(
        "Compliance.Authority.DeclarationDateRequired",
        "Enter the date the declaration is signed.",
        BasisKeys.PlanDeclaration);

    /// <summary>The certificate id is not the SFC in force — only the current certificate takes a new scan.</summary>
    public static readonly Error SfcNotCurrent = ComplianceError.Conflict(
        "Compliance.Authority.SfcNotCurrent",
        "That is not the safety fitness certificate in force. Attach the scan to the current certificate (GET authority shows its id).",
        BasisKeys.Hta312_2_1, BasisKeys.MtiSfc, BasisKeys.Plan2);

    public static readonly Error SfcNotFound = ComplianceError.NotFound(
        "Compliance.Authority.SfcNotFound",
        "No safety fitness certificate with that id is on file.",
        BasisKeys.Hta312_2_1, BasisKeys.MtiSfc, BasisKeys.Plan2);

    public static readonly Error SfcExpiryNotAfterIssue = ComplianceError.Validation(
        "Compliance.Authority.SfcExpiryNotAfterIssue",
        "The certificate's expiry date must be after its issue date.",
        BasisKeys.MtiSfc, BasisKeys.MtiNewCarrier, BasisKeys.Plan2);

    public static readonly Error NscNumberRequired = ComplianceError.Validation(
        "Compliance.Authority.NscNumberRequired",
        $"Enter the carrier's NSC number (up to {CarrierAuthority.NscNumberMaxLength} characters) — it identifies the carrier on every vehicle and record.",
        BasisKeys.Mr132_2024_9, BasisKeys.Plan5_6, BasisKeys.Plan2);

    public static readonly Error SafetyRatingUnknown = ComplianceError.Validation(
        "Compliance.Authority.SafetyRatingUnknown",
        "The safety rating must be Unaudited, Satisfactory, Conditional or Unsatisfactory.",
        BasisKeys.MtiSfc, BasisKeys.Plan2);

    public static readonly Error SafetyRatingDateRequired = ComplianceError.Validation(
        "Compliance.Authority.SafetyRatingDateRequired",
        "Enter the date the department's rating applies from.",
        BasisKeys.MtiSfc, BasisKeys.Plan2);

    public static readonly Error IntraProvincialSignerMustBeOwner = ComplianceError.Validation(
        "Compliance.Authority.IntraProvincialSignerMustBeOwner",
        "The intra-provincial declaration is signed by the owner.",
        BasisKeys.Plan1, BasisKeys.PlanDeclaration);

    public static readonly Error DeclarationDateInFuture = ComplianceError.Validation(
        "Compliance.Authority.DeclarationDateInFuture",
        "A declaration cannot be dated in the future.",
        BasisKeys.PlanDeclaration);

    public static readonly Error OperatingRoutesTooLong = ComplianceError.Validation(
        "Compliance.Authority.OperatingRoutesTooLong",
        $"The operating routes must be {CarrierAuthority.OperatingRoutesMaxLength} characters or fewer.",
        BasisKeys.Plan1);
}

/// <summary>Errors about a vehicle as Compliance knows it — its replica row and the regulated override.</summary>
public static class ComplianceVehicleErrors
{
    /// <summary>
    /// The vehicle is not in Compliance's replica of the fleet (<c>vehicle_lookup</c>, fed by
    /// Fleet's vehicle-changed events). A vehicle registered moments ago may not have arrived yet.
    /// </summary>
    public static readonly Error NotFound = ComplianceError.NotFound(
        "Compliance.Vehicle.NotFound",
        "Compliance has no record of that vehicle. If it was just added in Fleet, try again in a moment.",
        BasisKeys.HtaRegulatedVehicle, BasisKeys.Plan11);

    public static readonly Error VehicleIdRequired = ComplianceError.Validation(
        "Compliance.Vehicle.VehicleIdRequired",
        "Name the vehicle (vehicleId) the document belongs to.",
        BasisKeys.HtaRegulatedVehicle, BasisKeys.Plan11);
}
