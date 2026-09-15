using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Application.Drivers.GetByUser;
using NorthernLink.Drivers.Application.Drivers.LinkUser;
using NorthernLink.Drivers.Application.Drivers.SelfAccess;
using NorthernLink.Drivers.Application.Drivers.UnlinkUser;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Drivers.Tests;

/// <summary>
/// The Driver ↔ Identity link: the domain transitions, the link/unlink handlers, what
/// <c>GET /api/drivers/me</c> answers, and the caller-owns-this-row guard that makes the
/// DriverAccess policy safe to attach to <c>{driverId}</c> routes.
/// </summary>
public class DriverUserLinkTests
{
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static Driver NewDriver()
    {
        var result = TestDrivers.Register();
        Assert.True(result.IsSuccess);
        result.Value.ClearDomainEvents();
        return result.Value;
    }

    // ---- Domain ----

    [Fact]
    public void A_newly_registered_driver_has_no_linked_user() =>
        Assert.Null(NewDriver().UserId);

    [Fact]
    public void Linking_sets_the_user_and_raises_its_own_event()
    {
        var driver = NewDriver();

        var result = driver.LinkUser(UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, driver.UserId);

        // Its own event, not a generic DriverUpdated: granting an account a driver's identity is
        // an access change and the journal has to show it as one.
        var raised = Assert.IsType<DriverUserLinkedDomainEvent>(Assert.Single(driver.DomainEvents));
        Assert.Equal(driver.Id, raised.DriverId);
        Assert.Equal(UserId, raised.UserId);
    }

    [Fact]
    public void Linking_the_same_user_again_succeeds_without_a_second_event()
    {
        // Idempotent so a retried request (a flaky console, a replayed call) cannot fail.
        var driver = NewDriver();
        Assert.True(driver.LinkUser(UserId).IsSuccess);
        driver.ClearDomainEvents();

        var result = driver.LinkUser(UserId);

        Assert.True(result.IsSuccess);
        Assert.Empty(driver.DomainEvents);
    }

    [Fact]
    public void Repointing_a_linked_driver_at_a_different_user_is_a_conflict()
    {
        // Unlink first, so both halves land in the audit trail as separate events.
        var driver = NewDriver();
        Assert.True(driver.LinkUser(UserId).IsSuccess);

        var result = driver.LinkUser(OtherUserId);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.AlreadyLinkedToAnotherUser, result.Error);
        Assert.Equal(UserId, driver.UserId);
    }

    [Fact]
    public void An_empty_user_id_is_rejected()
    {
        var result = NewDriver().LinkUser(Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.UserIdRequired, result.Error);
    }

    [Fact]
    public void Unlinking_clears_the_user_and_reports_the_one_removed()
    {
        var driver = NewDriver();
        Assert.True(driver.LinkUser(UserId).IsSuccess);
        driver.ClearDomainEvents();

        var result = driver.UnlinkUser();

        Assert.True(result.IsSuccess);
        Assert.Null(driver.UserId);

        var raised = Assert.IsType<DriverUserUnlinkedDomainEvent>(Assert.Single(driver.DomainEvents));
        Assert.Equal(UserId, raised.PreviousUserId);
    }

    [Fact]
    public void Unlinking_an_unlinked_driver_fails_as_not_linked()
    {
        var result = NewDriver().UnlinkUser();

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.NotLinked, result.Error);
    }

    /// <summary>
    /// Linking is its own transition. If Update ever grew a user-id parameter, an ordinary
    /// "fix the phone number" edit could silently re-point a login with no distinct event.
    /// </summary>
    [Fact]
    public void Updating_roster_details_cannot_touch_the_link()
    {
        var driver = NewDriver();
        Assert.True(driver.LinkUser(UserId).IsSuccess);

        Assert.True(driver.Update("J. Spence", "204-555-0000", "Class 1", null, "Northern Link", false).IsSuccess);

        Assert.Equal(UserId, driver.UserId);
    }

    // ---- Handlers ----

    [Fact]
    public async Task Linking_a_user_already_linked_to_another_driver_is_a_conflict()
    {
        // The partial unique index would also stop this, but as a raw unique violation — i.e. a
        // 500 with a Postgres string in it. Checking here turns it into a 409 the console can
        // explain.
        var taken = NewDriver();
        Assert.True(taken.LinkUser(UserId).IsSuccess);
        var target = NewDriver();
        var repository = new FakeDriverRepository(taken, target);

        var result = await new LinkDriverUserCommandHandler(repository).Handle(
            new LinkDriverUserCommand(TestDrivers.TenantId, target.Id, UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.UserAlreadyLinkedToAnotherDriver, result.Error);
        Assert.Null(target.UserId);
        Assert.False(repository.Saved);
    }

    [Fact]
    public async Task Linking_an_unknown_driver_is_a_not_found()
    {
        var result = await new LinkDriverUserCommandHandler(new FakeDriverRepository()).Handle(
            new LinkDriverUserCommand(TestDrivers.TenantId, Guid.NewGuid(), UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DriverErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Linking_persists_the_link()
    {
        var driver = NewDriver();
        var repository = new FakeDriverRepository(driver);

        var result = await new LinkDriverUserCommandHandler(repository).Handle(
            new LinkDriverUserCommand(TestDrivers.TenantId, driver.Id, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserId, driver.UserId);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Unlinking_persists_the_removal()
    {
        var driver = NewDriver();
        Assert.True(driver.LinkUser(UserId).IsSuccess);
        var repository = new FakeDriverRepository(driver);

        var result = await new UnlinkDriverUserCommandHandler(repository).Handle(
            new UnlinkDriverUserCommand(TestDrivers.TenantId, driver.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(driver.UserId);
        Assert.True(repository.Saved);
    }

    // ---- GET /api/drivers/me ----

    [Fact]
    public async Task Me_returns_the_linked_drivers_record()
    {
        var expected = Response(Guid.NewGuid(), UserId);
        var handler = new GetDriverByUserQueryHandler(new FakeDriverReadService(expected));

        var result = await handler.Handle(
            new GetDriverByUserQuery(TestDrivers.TenantId, UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    /// <summary>
    /// The contract value the Driver Field App branches on. An unlinked account is an onboarding
    /// state with a specific fix ("ask dispatch to link you"), not a generic lookup miss — so the
    /// app can say that instead of showing a blank error. Renaming this code breaks the app
    /// silently, which is why the literal is asserted rather than the Error instance alone.
    /// </summary>
    [Fact]
    public async Task Me_returns_404_Drivers_NotLinked_when_the_account_has_no_driver_row()
    {
        var handler = new GetDriverByUserQueryHandler(new FakeDriverReadService(null));

        var result = await handler.Handle(
            new GetDriverByUserQuery(TestDrivers.TenantId, UserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Drivers.NotLinked", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    // ---- The caller-owns-this-row guard ----

    /// <summary>
    /// The half most likely to be skipped, and the reason DriverAccess is safe on a
    /// <c>{driverId}</c> route at all: the policy admits every Driver, so without this driver A
    /// can post a duty log against driver B.
    /// </summary>
    [Fact]
    public async Task A_driver_may_act_on_their_own_record()
    {
        var mine = NewDriver();
        Assert.True(mine.LinkUser(UserId).IsSuccess);
        var guard = new DriverSelfAccess(
            new FakeActor(UserId, Roles.Driver), new FakeDriverRepository(mine));

        Assert.True(await guard.MayActOnDriverAsync(mine.Id));
    }

    [Fact]
    public async Task A_driver_may_not_act_on_another_drivers_record()
    {
        var mine = NewDriver();
        Assert.True(mine.LinkUser(UserId).IsSuccess);
        var theirs = NewDriver();
        var guard = new DriverSelfAccess(
            new FakeActor(UserId, Roles.Driver), new FakeDriverRepository(mine, theirs));

        Assert.False(await guard.MayActOnDriverAsync(theirs.Id));
    }

    [Fact]
    public async Task A_driver_with_no_linked_record_may_act_on_nobody()
    {
        var theirs = NewDriver();
        Assert.True(theirs.LinkUser(OtherUserId).IsSuccess);
        var guard = new DriverSelfAccess(
            new FakeActor(UserId, Roles.Driver), new FakeDriverRepository(theirs));

        Assert.False(await guard.MayActOnDriverAsync(theirs.Id));
    }

    /// <summary>Dispatch staff act on other people's rows for a living — that is the job.</summary>
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Dispatcher)]
    [InlineData(Roles.Supervisor)]
    public async Task Dispatch_roles_may_act_on_any_drivers_record(string role)
    {
        var theirs = NewDriver();
        var repository = new FakeDriverRepository(theirs);
        var guard = new DriverSelfAccess(new FakeActor(Guid.NewGuid(), role), repository);

        Assert.True(await guard.MayActOnDriverAsync(theirs.Id));

        // The override short-circuits before the lookup: a dispatcher's request never pays for it.
        Assert.Equal(0, repository.UserLookups);
    }

    [Fact]
    public async Task An_actor_with_no_user_id_may_act_on_nobody()
    {
        // Background work has no principal. "No actor" must never read as "owns everything".
        var theirs = NewDriver();
        var guard = new DriverSelfAccess(
            new FakeActor(null, Roles.Driver), new FakeDriverRepository(theirs));

        Assert.False(await guard.MayActOnDriverAsync(theirs.Id));
    }

    /// <summary>
    /// Role matching is ordinal everywhere on this platform (User.Create rejects "owner" outright
    /// rather than letting it 403 later). A case-insensitive comparison here — which
    /// ClaimsPrincipal.IsInRole would have given us — would quietly hand dispatch override to a
    /// role string the platform is written to refuse.
    /// </summary>
    [Fact]
    public async Task Role_matching_is_case_sensitive()
    {
        var theirs = NewDriver();
        var guard = new DriverSelfAccess(
            new FakeActor(Guid.NewGuid(), "dispatcher"), new FakeDriverRepository(theirs));

        Assert.False(await guard.MayActOnDriverAsync(theirs.Id));
    }

    private static DriverResponse Response(Guid id, Guid? userId) => new(
        id, userId, "J. Spence", null, "Class 2", null, "Northern Link", false, "Active",
        0, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class FakeActor(Guid? userId, params string[] roles) : ICurrentActor
    {
        public Guid? UserId => userId;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => roles;
    }

    private sealed class FakeDriverRepository(params Driver[] drivers) : IDriverRepository
    {
        private readonly List<Driver> _drivers = [.. drivers];

        public bool Saved { get; private set; }

        public int UserLookups { get; private set; }

        public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_drivers.FirstOrDefault(d => d.Id == id));

        public Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            UserLookups++;
            return Task.FromResult(_drivers.FirstOrDefault(d => d.UserId == userId));
        }

        public void Add(Driver driver) => _drivers.Add(driver);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDriverReadService(DriverResponse? linked) : IDriverReadService
    {
        public Task<IReadOnlyList<DriverResponse>> GetDriversAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DriverResponse>>(linked is null ? [] : [linked]);

        public Task<DriverResponse?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
            Task.FromResult(linked?.Id == driverId ? linked : null);

        public Task<DriverResponse?> GetDriverByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(linked?.UserId == userId ? linked : null);
    }
}
