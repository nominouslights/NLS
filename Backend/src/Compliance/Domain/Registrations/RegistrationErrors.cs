using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Registrations;

/// <summary>Errors of vehicle registrations (NL-SP-02 ss. 5.3, 11; M.R. 76/94 s. 3(2)(a)(i)).</summary>
public static class RegistrationErrors
{
    public static readonly Error NotFound = ComplianceError.NotFound(
        "Compliance.Registration.NotFound", "The vehicle registration was not found.",
        BasisKeys.Plan5_3, BasisKeys.Plan11);

    public static readonly Error CurrentExists = ComplianceError.Conflict(
        "Compliance.Registration.CurrentExists",
        "This vehicle already has a current registration. Renew it (POST vehicle-registrations/{id}/renew) so the old one is kept as superseded.",
        BasisKeys.Plan5_3, BasisKeys.Plan11);

    public static readonly Error NotCurrent = ComplianceError.Conflict(
        "Compliance.Registration.NotCurrent", "Only the vehicle's current registration can be renewed.",
        BasisKeys.Plan5_3, BasisKeys.Plan11);

    public static readonly Error PlateRequired = ComplianceError.Validation(
        "Compliance.Registration.PlateRequired",
        $"Enter the plate number (up to {VehicleRegistration.PlateMaxLength} characters).",
        BasisKeys.Plan5_3, BasisKeys.Mr76_94_3_2_a_i);

    public static readonly Error VehicleClassTooLong = ComplianceError.Validation(
        "Compliance.Registration.VehicleClassTooLong",
        $"The registration class must be {VehicleRegistration.VehicleClassMaxLength} characters or fewer.",
        BasisKeys.Plan5_3);

    public static readonly Error TermMonthsInvalid = ComplianceError.Validation(
        "Compliance.Registration.TermMonthsInvalid",
        $"The registration term must be between 1 and {VehicleRegistration.MaxTermMonths} months.",
        BasisKeys.Plan5_3, BasisKeys.Mr76_94_3_2_a_i);

    public static readonly Error EffectiveOnRequired = ComplianceError.Validation(
        "Compliance.Registration.EffectiveOnRequired", "Enter the date the registration takes effect.",
        BasisKeys.Plan5_3);

    public static readonly Error ExpiryNotAfterEffective = ComplianceError.Validation(
        "Compliance.Registration.ExpiryNotAfterEffective", "The expiry date must be after the effective date.",
        BasisKeys.Plan5_3);
}
