using System.Text.RegularExpressions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;

/// <summary>
/// The single recipient gate used by both the accruals send command and its preview query:
/// trims addresses, rejects anything failing the module's RFC-lite regex, de-duplicates by
/// address (case-insensitive; the first occurrence's name wins), and enforces the 1–16
/// dispatch cap on the distinct list. Shared so a preview can never accept a recipient list
/// the send would reject — the same guarantee <c>PickupEmailReportComposer</c> gives for
/// report content.
/// </summary>
public static partial class AccrualsRecipientGate
{
    public static Result<IReadOnlyList<AccrualsRecipientInput>> Normalize(
        IReadOnlyList<AccrualsRecipientInput> recipients)
    {
        if (recipients.Count == 0)
        {
            return Result.Failure<IReadOnlyList<AccrualsRecipientInput>>(EmailDispatchErrors.NoRecipients);
        }

        var normalized = new List<AccrualsRecipientInput>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var recipient in recipients)
        {
            var email = recipient.Email?.Trim() ?? string.Empty;
            if (!EmailRegex().IsMatch(email))
            {
                return Result.Failure<IReadOnlyList<AccrualsRecipientInput>>(
                    EmailDispatchErrors.InvalidRecipientEmail(recipient.Email ?? string.Empty));
            }

            if (seen.Add(email))
            {
                normalized.Add(new AccrualsRecipientInput(email, recipient.ContactName?.Trim() ?? string.Empty));
            }
        }

        if (normalized.Count > EmailDispatch.MaxRecipients)
        {
            return Result.Failure<IReadOnlyList<AccrualsRecipientInput>>(EmailDispatchErrors.TooManyRecipients);
        }

        return Result.Success<IReadOnlyList<AccrualsRecipientInput>>(normalized);
    }

    // RFC-lite, identical to the pickup handlers' gate: something@something.tld, no whitespace.
    // Deliberately loose — Postmark is the real arbiter.
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();
}
