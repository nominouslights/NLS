using Microsoft.Extensions.Logging;

namespace NorthernLink.Shared.Hosting;

/// <summary>
/// Exponential backoff and log-noise policy for the module background workers' poll loops —
/// <c>OutboxDispatcher</c>, <c>ProjectionWorker</c> and <c>OutboxPollingConsumer</c>, which all
/// run the same shape: wait, poll, log-and-continue on failure, never kill the host.
///
/// This exists because of how the platform fails in practice. Every worker in the API talks to
/// the same managed Postgres over the same connection string, so a single unreachable database
/// fails all of them at once — currently nineteen workers across eight modules. At their healthy
/// intervals (1s for outbox polling, 5s for projections and dispatch) that produced a continuous
/// wall of identical stack traces, each one having first burned a full connection timeout, which
/// buried whatever the real error was. Retrying a database that is down every second has no
/// upside: the work is not time-sensitive to the second, and nothing is lost by waiting.
///
/// Two behaviours, both keyed on <em>consecutive</em> failures and both reset by the first
/// success:
///
/// <list type="number">
/// <item>The delay grows <c>pollInterval × 2^(n-1)</c> up to a cap, so a long outage settles at
/// one attempt per cap rather than per interval.</item>
/// <item>The failure is logged loudly once, quietly while it persists, and loudly again every
/// tenth attempt — see <see cref="LevelFor"/>.</item>
/// </list>
///
/// The jitter is not decoration. Without it the workers stay in lockstep: they fail together,
/// back off by the same amount, and retry together forever, which is both a thundering herd at
/// the database and the reason the logs interleave into an unreadable block. Spreading each
/// worker's retry across ±20% decorrelates them within a few rounds.
///
/// Not thread-safe, and deliberately so — each instance belongs to exactly one worker's loop.
/// </summary>
public sealed class PollBackoff
{
    /// <summary>
    /// Ceiling on the doubling exponent. <c>2^31</c> would overflow the multiply long before the
    /// cap could clamp it; any value past ~30 is astronomically beyond every realistic cap
    /// anyway, so clamping the exponent keeps the arithmetic finite without changing behaviour.
    /// </summary>
    private const int MaxExponent = 30;

    private const double JitterFloor = 0.8;
    private const double JitterCeiling = 1.2;

    /// <summary>
    /// While a failure persists, re-log at <see cref="LogLevel.Warning"/> every this many
    /// attempts. Frequent enough that a sustained outage leaves a visible trail at default log
    /// levels, rare enough that the trail is readable.
    /// </summary>
    private const int SustainedFailureLogEvery = 10;

    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _maxDelay;

    /// <param name="pollInterval">The healthy cadence, and the base the doubling starts from.</param>
    /// <param name="maxDelay">
    /// Cap on the backed-off delay. Clamped up to <paramref name="pollInterval"/> if a caller
    /// configures a cap below the interval, so the loop can never end up polling faster while
    /// failing than it does while healthy.
    /// </param>
    public PollBackoff(TimeSpan pollInterval, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

        _pollInterval = pollInterval;
        _maxDelay = maxDelay < pollInterval ? pollInterval : maxDelay;
        NextDelay = pollInterval;
    }

    /// <summary>Failures since the last success. Zero while healthy.</summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>
    /// How long the loop should wait before its next poll — the plain interval while healthy,
    /// the jittered backoff while failing. Starts at the interval, so the first wait happens
    /// before the first poll (the boot-race head start every worker already relied on).
    /// </summary>
    public TimeSpan NextDelay { get; private set; }

    /// <summary>
    /// Records a successful poll and returns to the healthy cadence.
    /// </summary>
    /// <returns>
    /// <c>null</c> when the previous poll also succeeded — the overwhelmingly common case, and
    /// nothing worth logging. Otherwise the number of consecutive failures that just ended, so
    /// the caller can log the recovery. Recovery is worth a line: without one, a log shows a
    /// worker failing and then simply says nothing, which reads the same as a worker that died.
    /// </returns>
    public int? RecordSuccess()
    {
        NextDelay = _pollInterval;

        if (ConsecutiveFailures == 0)
        {
            return null;
        }

        var recoveredAfter = ConsecutiveFailures;
        ConsecutiveFailures = 0;
        return recoveredAfter;
    }

    /// <summary>
    /// Records a failed poll, advances the backoff, and returns how the caller should log it.
    /// The caller keeps ownership of the message itself so each worker's structured properties
    /// (module, schema, DbContext) survive.
    /// </summary>
    public PollFailure RecordFailure()
    {
        ConsecutiveFailures++;
        NextDelay = Jitter(BackoffFor(ConsecutiveFailures));

        return new PollFailure(LevelFor(ConsecutiveFailures), ConsecutiveFailures, NextDelay);
    }

    private TimeSpan BackoffFor(int consecutiveFailures)
    {
        var factor = Math.Pow(2, Math.Min(consecutiveFailures - 1, MaxExponent));
        var scaledMs = _pollInterval.TotalMilliseconds * factor;

        return scaledMs >= _maxDelay.TotalMilliseconds
            ? _maxDelay
            : TimeSpan.FromMilliseconds(scaledMs);
    }

    /// <summary>
    /// Spreads the delay across ±20%. Applied after the cap, so a capped worker can wait up to
    /// 1.2× the cap — intended, and the only way capped workers ever drift apart from each other.
    /// </summary>
    private static TimeSpan Jitter(TimeSpan delay) =>
        TimeSpan.FromMilliseconds(
            delay.TotalMilliseconds * (JitterFloor + (Random.Shared.NextDouble() * (JitterCeiling - JitterFloor))));

    /// <summary>
    /// First failure is the alert and carries the stack trace. Everything after it is the same
    /// exception repeating, so it drops to Debug — with a Warning every
    /// <see cref="SustainedFailureLogEvery"/> attempts so a long outage is still visible to
    /// anyone reading at default levels, and so the recovery line has something to pair with.
    /// </summary>
    private static LogLevel LevelFor(int consecutiveFailures) => consecutiveFailures switch
    {
        1 => LogLevel.Error,
        _ when consecutiveFailures % SustainedFailureLogEvery == 0 => LogLevel.Warning,
        _ => LogLevel.Debug,
    };
}

/// <summary>How a failed poll should be logged, and when the loop will try again.</summary>
public readonly record struct PollFailure(LogLevel Level, int ConsecutiveFailures, TimeSpan RetryDelay);
