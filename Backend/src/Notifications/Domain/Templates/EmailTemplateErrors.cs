using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Domain.Templates;

/// <summary>All domain errors the EmailTemplate aggregate (and its handlers) can produce.</summary>
public static class EmailTemplateErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Notifications.Template.NotFound", "The email template was not found.");

    public static readonly Error Inactive = Error.Validation(
        "Notifications.Template.Inactive", "The email template is inactive and cannot be used to send.");

    public static readonly Error NameRequired = Error.Validation(
        "Notifications.Template.NameRequired", "A template name is required.");

    public static readonly Error NameTooLong = Error.Validation(
        "Notifications.Template.NameTooLong", "The template name must be 200 characters or fewer.");

    public static readonly Error SubjectRequired = Error.Validation(
        "Notifications.Template.SubjectRequired", "A subject line is required.");

    public static readonly Error SubjectTooLong = Error.Validation(
        "Notifications.Template.SubjectTooLong", "The subject line must be 300 characters or fewer.");

    public static readonly Error HtmlBodyRequired = Error.Validation(
        "Notifications.Template.HtmlBodyRequired", "An HTML body is required.");

    public static readonly Error HtmlBodyTooLong = Error.Validation(
        "Notifications.Template.HtmlBodyTooLong", "The HTML body must be 20,000 characters or fewer.");

    public static readonly Error ClientNameRequired = Error.Validation(
        "Notifications.Template.ClientNameRequired",
        "A client name snapshot is required when the template targets a specific client.");

    public static readonly Error ClientNameWithoutClientId = Error.Validation(
        "Notifications.Template.ClientNameWithoutClientId",
        "A client name is only allowed when a client id is set.");

    /// <summary>An unrecognized <c>{{Token}}</c> in the subject or body — a typo caught at save time.</summary>
    public static Error UnknownMergeField(string token) => Error.Validation(
        "Notifications.Template.UnknownMergeField",
        $"Unknown merge field {{{{{token}}}}}. Valid fields: {string.Join(", ", MergeFields.All)}.");
}
