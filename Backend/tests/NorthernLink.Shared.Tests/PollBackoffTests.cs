using Microsoft.Extensions.Logging;
using NorthernLink.Shared.Hosting;
using Xunit;

namespace NorthernLink.Shared.Tests;

public class PollBackoffTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Cap = TimeSpan.FromSeconds(60);

    private static PollBackoff Create() => new(Interval, Cap);

    /// <summary>
    /// Jitter makes exact assertions impossible by design, so the delay tests assert the band
    /// the value must land in: the undithered target ±20%.
    /// </summary>
    private static void AssertWithinJitter(TimeSpan expected, TimeSpan actual)
    {
        Assert.InRange(
            actual.TotalMilliseconds,
            expected.TotalMilliseconds * 0.8,
            expected.TotalMilliseconds * 1.2);
    }

    [Fact]
    public void A_healthy_loop_waits_exactly_one_interval()
    {
        // No jitter while healthy — a working system should poll on a predictable cadence.
        Assert.Equal(Interval, Create().NextDelay);
    }

    [Fact]
    public void The_first_delay_happens_before_the_first_poll()
    {
        // The boot-race head start every worker relies on: NextDelay is usable before any
        // poll has been recorded.
        var backoff = Create();

        Assert.Equal(Interval, backoff.NextDelay);
        Assert.Equal(0, backoff.ConsecutiveFailures);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(6, 32)]
    public void The_delay_doubles_with_each_consecutive_failure(int failures, int expectedSeconds)
    {
        var backoff = Create();

        PollFailure failure = default;
        for (var i = 0; i < failures; i++)
        {
            failure = backoff.RecordFailure();
        }

        Assert.Equal(failures, failure.ConsecutiveFailures);
        AssertWithinJitter(TimeSpan.FromSeconds(expectedSeconds), failure.RetryDelay);
    }

    [Fact]
    public void The_delay_stops_growing_at_the_cap()
    {
        var backoff = Create();

        PollFailure failure = default;
        for (var i = 0; i < 20; i++)
        {
            failure = backoff.RecordFailure();
        }

        // 2^19 seconds without a cap. Jitter is applied after the cap, so up to 1.2x it.
        AssertWithinJitter(Cap, failure.RetryDelay);
    }

    [Fact]
    public void A_very_long_outage_never_overflows_the_delay()
    {
        // 2^(n-1) would overflow long before this; the exponent clamp keeps it finite.
        var backoff = Create();

        PollFailure failure = default;
        for (var i = 0; i < 2_000; i++)
        {
            failure = backoff.RecordFailure();
        }

        Assert.True(failure.RetryDelay > TimeSpan.Zero);
        AssertWithinJitter(Cap, failure.RetryDelay);
    }

    [Fact]
    public void Jitter_spreads_the_delay_so_workers_do_not_retry_in_lockstep()
    {
        // The whole point: nineteen workers failing together must not converge on one instant.
        // Twenty independent backoffs at the cap should not all produce the same delay.
        var delays = new HashSet<TimeSpan>();
        for (var i = 0; i < 20; i++)
        {
            var backoff = Create();
            for (var f = 0; f < 10; f++)
            {
                backoff.RecordFailure();
            }

            delays.Add(backoff.NextDelay);
        }

        Assert.True(delays.Count > 1, "every worker backed off by an identical amount — jitter is not being applied");
    }

    [Fact]
    public void A_success_returns_to_the_plain_interval()
    {
        var backoff = Create();
        backoff.RecordFailure();
        backoff.RecordFailure();

        backoff.RecordSuccess();

        Assert.Equal(Interval, backoff.NextDelay);
        Assert.Equal(0, backoff.ConsecutiveFailures);
    }

    [Fact]
    public void A_success_after_failures_reports_how_many_it_recovered_from()
    {
        var backoff = Create();
        backoff.RecordFailure();
        backoff.RecordFailure();
        backoff.RecordFailure();

        Assert.Equal(3, backoff.RecordSuccess());
    }

    [Fact]
    public void A_success_on_a_healthy_loop_reports_nothing_to_log()
    {
        // The common case, every poll of every healthy worker — it must not log.
        var backoff = Create();

        Assert.Null(backoff.RecordSuccess());
        Assert.Null(backoff.RecordSuccess());
    }

    [Fact]
    public void Recovery_is_only_reported_once()
    {
        var backoff = Create();
        backoff.RecordFailure();

        Assert.Equal(1, backoff.RecordSuccess());
        Assert.Null(backoff.RecordSuccess());
    }

    [Fact]
    public void Failures_after_a_recovery_start_the_backoff_over()
    {
        var backoff = Create();
        for (var i = 0; i < 8; i++)
        {
            backoff.RecordFailure();
        }

        backoff.RecordSuccess();
        var failure = backoff.RecordFailure();

        Assert.Equal(1, failure.ConsecutiveFailures);
        AssertWithinJitter(Interval, failure.RetryDelay);
    }

    [Fact]
    public void The_first_failure_is_logged_loudly()
    {
        // This one carries the stack trace and is the actual alert.
        Assert.Equal(LogLevel.Error, Create().RecordFailure().Level);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(11)]
    public void Repeats_of_the_same_failure_are_quiet(int failures)
    {
        var backoff = Create();

        PollFailure failure = default;
        for (var i = 0; i < failures; i++)
        {
            failure = backoff.RecordFailure();
        }

        Assert.Equal(LogLevel.Debug, failure.Level);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(100)]
    public void A_sustained_outage_leaves_a_periodic_trail(int failures)
    {
        var backoff = Create();

        PollFailure failure = default;
        for (var i = 0; i < failures; i++)
        {
            failure = backoff.RecordFailure();
        }

        Assert.Equal(LogLevel.Warning, failure.Level);
    }

    [Fact]
    public void An_outage_produces_a_readable_number_of_loud_lines_rather_than_a_wall()
    {
        // The regression this class exists to prevent. Roughly an hour of a 1s-interval worker
        // being unable to reach the database: previously one Error per second (~3,600 lines),
        // now a handful.
        var backoff = Create();
        var elapsed = TimeSpan.Zero;
        var loud = 0;

        while (elapsed < TimeSpan.FromHours(1))
        {
            var failure = backoff.RecordFailure();
            if (failure.Level >= LogLevel.Warning)
            {
                loud++;
            }

            elapsed += failure.RetryDelay;
        }

        Assert.InRange(loud, 1, 20);
    }

    [Fact]
    public void A_cap_below_the_interval_never_makes_a_failing_loop_poll_faster_than_a_healthy_one()
    {
        // Misconfiguration guard: backing off must never speed the loop up.
        var backoff = new PollBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

        var failure = backoff.RecordFailure();

        AssertWithinJitter(TimeSpan.FromSeconds(5), failure.RetryDelay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_interval_is_rejected(int seconds)
    {
        // A zero interval would spin the loop as fast as the CPU allows.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PollBackoff(TimeSpan.FromSeconds(seconds), Cap));
    }
}
