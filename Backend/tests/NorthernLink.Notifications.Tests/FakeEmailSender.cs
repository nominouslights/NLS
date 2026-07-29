using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// In-memory <see cref="IEmailSender"/> for handler tests: records every batch it receives
/// and answers with a configurable outcome per email (default: everything sent).
/// </summary>
internal sealed class FakeEmailSender : IEmailSender
{
    public List<IReadOnlyList<OutgoingEmail>> Batches { get; } = [];

    /// <summary>Outcome factory applied per email; index-aligned with the batch.</summary>
    public Func<OutgoingEmail, int, EmailSendOutcome> OutcomeFor { get; set; } =
        (_, index) => new EmailSendOutcome(true, null, null, $"msg-{index}");

    public Task<IReadOnlyList<EmailSendOutcome>> SendBatchAsync(
        IReadOnlyList<OutgoingEmail> emails,
        CancellationToken cancellationToken = default)
    {
        Batches.Add(emails);
        IReadOnlyList<EmailSendOutcome> outcomes = emails.Select((email, index) => OutcomeFor(email, index)).ToList();
        return Task.FromResult(outcomes);
    }
}
