using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Vendors;

/// <summary>All domain errors the Vendor aggregate (and its handlers) can produce.</summary>
public static class VendorErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Budgeting.Vendor.NotFound", "The vendor was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Budgeting.Vendor.NameRequired", "A vendor needs a name.");

    public static readonly Error NameTooLong = Error.Validation(
        "Budgeting.Vendor.NameTooLong",
        $"The vendor name must be {Vendor.NameMaxLength} characters or fewer.");

    /// <summary>
    /// Built per request rather than a static, because the message names the vendor that already
    /// holds the name — and says when that vendor is retired, since the fix is then to reactivate
    /// it rather than to invent a second spelling.
    /// </summary>
    public static Error DuplicateName(string existingName, bool existingIsActive) => Error.Conflict(
        "Budgeting.Vendor.DuplicateName",
        existingIsActive
            ? $"A vendor named \"{existingName}\" already exists. Vendor names are unique, ignoring case."
            : $"A retired vendor named \"{existingName}\" already exists. Reactivate it instead of adding it again.");

    public static readonly Error ContactNameTooLong = Error.Validation(
        "Budgeting.Vendor.ContactNameTooLong",
        $"The contact name must be {Vendor.ContactNameMaxLength} characters or fewer.");

    public static readonly Error EmailTooLong = Error.Validation(
        "Budgeting.Vendor.EmailTooLong",
        $"The email must be {Vendor.EmailMaxLength} characters or fewer.");

    public static readonly Error EmailInvalid = Error.Validation(
        "Budgeting.Vendor.EmailInvalid",
        "The email must look like an address (name@example.com).");

    public static readonly Error PhoneTooLong = Error.Validation(
        "Budgeting.Vendor.PhoneTooLong",
        $"The phone number must be {Vendor.PhoneMaxLength} characters or fewer.");

    public static readonly Error AddressTooLong = Error.Validation(
        "Budgeting.Vendor.AddressTooLong",
        $"The address must be {Vendor.AddressMaxLength} characters or fewer.");

    public static readonly Error NotesTooLong = Error.Validation(
        "Budgeting.Vendor.NotesTooLong",
        $"The notes must be {Vendor.NotesMaxLength} characters or fewer.");

    public static readonly Error GstRegistrationNumberTooLong = Error.Validation(
        "Budgeting.Vendor.GstRegistrationNumberTooLong",
        $"The GST registration number must be {Vendor.GstRegistrationNumberMaxLength} characters or fewer.");

    public static readonly Error QboDisplayNameTooLong = Error.Validation(
        "Budgeting.Vendor.QboDisplayNameTooLong",
        $"The QuickBooks display name must be {Vendor.QboDisplayNameMaxLength} characters or fewer.");

    public static readonly Error DefaultBudgetCodeTooLong = Error.Validation(
        "Budgeting.Vendor.DefaultBudgetCodeTooLong",
        $"The default budget code must be {Vendor.DefaultBudgetCodeMaxLength} characters or fewer.");

    public static readonly Error DefaultBudgetCodeInvalidFormat = Error.Validation(
        "Budgeting.Vendor.DefaultBudgetCodeInvalidFormat",
        "The default budget code may use letters, digits and hyphens only, and must start and end with a letter or digit (for example FLEET-MAINT).");

    /// <summary>Names the alternative, like <c>BudgetCodeErrors.InUse</c>.</summary>
    public static readonly Error InUse = Error.Conflict(
        "Budgeting.Vendor.InUse",
        "This vendor is referenced by budget items and cannot be deleted. Retire it instead — a retired vendor stays listed so existing items keep resolving.");
}
