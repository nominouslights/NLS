using System.Security.Cryptography;
using System.Text;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.Transcribe;

/// <summary>
/// Deterministic event ids for transcribed entries: the same transcription id and line index
/// always yield the same <c>EventId</c>, which is what makes a retried
/// <c>POST …/transcribe</c> a no-op at the aggregate (it sees the id already on the day).
/// SHA-256 over "{transcriptionId}:{index}", first 16 bytes — not a UUID version anyone parses,
/// just a stable, collision-resistant Guid.
/// </summary>
public static class TranscriptionEventIds
{
    public static Guid For(Guid transcriptionId, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{transcriptionId:D}:{index}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// The same derivation for an <c>Idempotency-Key</c> header that is not itself a Guid: any
    /// stable client string becomes a stable event id.
    /// </summary>
    public static Guid ForKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("idempotency-key:" + key.Trim()));
        return new Guid(hash.AsSpan(0, 16));
    }
}
