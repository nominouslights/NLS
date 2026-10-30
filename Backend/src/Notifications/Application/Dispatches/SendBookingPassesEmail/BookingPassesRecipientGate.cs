using NorthernLink.Shared.Kernel;
using NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;
using NorthernLink.Notifications.Domain.Dispatches;

namespace NorthernLink.Notifications.Application.Dispatches.SendBookingPassesEmail;

/// <summary>
/// The single input gate used by both the booking-passes send command and its preview query.
/// Recipient rules (trim, RFC-lite regex, case-insensitive de-dupe with the first name
/// winning, 1–16 cap) are exactly the accruals rules — this delegates to
/// <see cref="AccrualsRecipientGate.Normalize"/> so there is one regex and one cap in the
/// module — and it adds the passes-specific check that the sheet lists at least one
/// traveller (<see cref="EmailDispatchErrors.NoTravellers"/>): an email of zero passes is
/// a bug upstream, not something to send.
/// </summary>
public static class BookingPassesRecipientGate
{
    public static Result<IReadOnlyList<PassRecipientInput>> Normalize(
        BookingPassSheet sheet,
        IReadOnlyList<PassRecipientInput> recipients)
    {
        if (sheet.Travellers.Count == 0)
        {
            return Result.Failure<IReadOnlyList<PassRecipientInput>>(EmailDispatchErrors.NoTravellers);
        }

        var normalized = AccrualsRecipientGate.Normalize(
            recipients.Select(r => new AccrualsRecipientInput(r.Email, r.ContactName)).ToList());
        if (normalized.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PassRecipientInput>>(normalized.Error);
        }

        return Result.Success<IReadOnlyList<PassRecipientInput>>(
            normalized.Value.Select(r => new PassRecipientInput(r.Email, r.ContactName)).ToList());
    }
}
