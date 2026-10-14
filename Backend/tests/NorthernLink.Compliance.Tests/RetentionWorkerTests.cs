using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Records.QueueForDisposalReview;
using NorthernLink.Compliance.Infrastructure.Retention;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// The nightly worker's one pass: every tenant with retained records gets a sweep command
/// dispatched under its own ambient tenant, one tenant's failure does not stop the rest, and the
/// schedule arithmetic lands on 02:00 Winnipeg time.
/// </summary>
public class RetentionWorkerTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TenantC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static (RetentionWorker Worker, RecordingSender Sender) Build(params Guid[] tenants)
    {
        var sender = new RecordingSender();
        var services = new ServiceCollection();
        services.AddScoped<IRetentionSweepSource>(_ => new StubSweepSource(tenants));
        services.AddScoped<ISender>(_ => sender);
        var provider = services.BuildServiceProvider();

        var worker = new RetentionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(), new RetentionOptions(), NullLogger<RetentionWorker>.Instance);
        return (worker, sender);
    }

    [Fact]
    public async Task One_pass_sweeps_every_tenant_under_its_own_ambient_tenant()
    {
        var (worker, sender) = Build(TenantA, TenantB);
        sender.QueuedPerTenant[TenantA] = 2;
        sender.QueuedPerTenant[TenantB] = 1;

        var queued = await worker.OnceAsync(new DateOnly(2026, 10, 11), CancellationToken.None);

        Assert.Equal(3, queued);
        Assert.Equal([TenantA, TenantB], sender.Dispatched.Select(d => d.Command.TenantId));
        Assert.All(sender.Dispatched, d => Assert.Equal(d.Command.TenantId, d.AmbientTenant));
        Assert.All(sender.Dispatched, d => Assert.Equal(new DateOnly(2026, 10, 11), d.Command.AsOf));
        Assert.Null(AmbientTenant.Current); // the push is scoped to the sweep
    }

    [Fact]
    public async Task A_failing_tenant_is_skipped_and_the_others_still_run()
    {
        var (worker, sender) = Build(TenantA, TenantB, TenantC);
        sender.ThrowFor = TenantB;
        sender.QueuedPerTenant[TenantA] = 1;
        sender.QueuedPerTenant[TenantC] = 1;

        var queued = await worker.OnceAsync(new DateOnly(2026, 10, 11), CancellationToken.None);

        Assert.Equal(2, queued);
        Assert.Equal([TenantA, TenantB, TenantC], sender.Dispatched.Select(d => d.Command.TenantId));
    }

    [Fact]
    public async Task No_tenants_means_no_dispatch()
    {
        var (worker, sender) = Build();

        Assert.Equal(0, await worker.OnceAsync(new DateOnly(2026, 10, 11), CancellationToken.None));
        Assert.Empty(sender.Dispatched);
    }

    [Fact]
    public void The_schedule_lands_on_two_in_the_morning_Winnipeg_time()
    {
        var options = new RetentionOptions();
        var winnipeg = options.ResolveTimeZone();
        Assert.NotEqual(TimeZoneInfo.Utc, winnipeg);

        // 2026-10-11 is on Central Daylight Time (UTC-5): 01:00 local is 06:00Z, 02:00 local is 07:00Z.
        var beforeTonight = new DateTimeOffset(2026, 10, 11, 6, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 7, 0, 0, TimeSpan.Zero), RetentionSchedule.NextRunAfter(beforeTonight, options.RunAt, winnipeg));

        // 03:00 local has passed today's run: the next is tomorrow's.
        var afterTonight = new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 7, 0, 0, TimeSpan.Zero), RetentionSchedule.NextRunAfter(afterTonight, options.RunAt, winnipeg));

        // In standard time (UTC-6) 02:00 local is 08:00Z.
        var winter = new DateTimeOffset(2026, 12, 1, 1, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero), RetentionSchedule.NextRunAfter(winter, options.RunAt, winnipeg));

        Assert.Equal(new DateOnly(2026, 10, 10), RetentionSchedule.TodayIn(new DateTimeOffset(2026, 10, 11, 4, 0, 0, TimeSpan.Zero), winnipeg));
    }

    [Fact]
    public void Options_default_to_the_brief()
    {
        var options = new RetentionOptions();

        Assert.Equal(new TimeOnly(2, 0), options.RunAt);
        Assert.Equal("America/Winnipeg", options.TimeZone);
        Assert.Equal(30, options.ReviewWindowDays);
        Assert.Equal("Compliance:Retention", RetentionOptions.SectionName);
    }

    private sealed class StubSweepSource(IReadOnlyList<Guid> tenants) : IRetentionSweepSource
    {
        public Task<IReadOnlyList<Guid>> ListTenantsWithRetainedRecordsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(tenants);
    }

    private sealed class RecordingSender : ISender
    {
        public List<(QueueForDisposalReviewCommand Command, Guid? AmbientTenant)> Dispatched { get; } = [];

        public Dictionary<Guid, int> QueuedPerTenant { get; } = [];

        public Guid? ThrowFor { get; set; }

        public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            var sweep = (QueueForDisposalReviewCommand)(object)command;
            Dispatched.Add((sweep, AmbientTenant.Current));
            if (sweep.TenantId == ThrowFor)
            {
                throw new InvalidOperationException("database unreachable");
            }

            var queued = QueuedPerTenant.TryGetValue(sweep.TenantId, out var count) ? count : 0;
            return Task.FromResult((Result<TResponse>)(object)Result.Success(queued));
        }

        public Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
