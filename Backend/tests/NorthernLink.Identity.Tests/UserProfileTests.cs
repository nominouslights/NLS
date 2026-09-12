using NorthernLink.Identity.Domain.Users;
using NorthernLink.Identity.Domain.Users.Events;
using Xunit;

namespace NorthernLink.Identity.Tests;

/// <summary>
/// User.UpdateProfile — the aggregate's first mutation. The rules pinned here are the ones the
/// rest of the platform depends on being true: blank means cleared (so a name can be removed all
/// the way through to Budgeting's replica), a rejected field changes nothing, and a redundant
/// save raises no event.
/// </summary>
public class UserProfileTests
{
    /// <summary>A freshly created user, with the creation event discarded so each test asserts
    /// only on what UpdateProfile raised.</summary>
    private static User Existing()
    {
        var user = TestUsers.Create();
        user.ClearDomainEvents();
        return user;
    }

    [Fact]
    public void A_new_user_has_no_profile_yet()
    {
        // Null, not "" — every account predating profiles is genuinely unnamed, and the whole
        // display chain falls back to the email on null.
        var user = TestUsers.Create();

        Assert.Null(user.FullName);
        Assert.Null(user.JobTitle);
    }

    [Fact]
    public void Setting_a_profile_stores_it_and_raises_one_event()
    {
        var user = Existing();

        var result = user.UpdateProfile("Léa Fontaine", "Financial Planner");

        Assert.True(result.IsSuccess);
        Assert.Equal("Léa Fontaine", user.FullName);
        Assert.Equal("Financial Planner", user.JobTitle);

        var raised = Assert.Single(user.DomainEvents.OfType<UserProfileUpdatedDomainEvent>());
        Assert.Equal(user.Id, raised.UserId);
        Assert.Equal(user.TenantId, raised.TenantId);
        Assert.Equal("Léa Fontaine", raised.FullName);
        Assert.Equal("Financial Planner", raised.JobTitle);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_off_both_fields()
    {
        var user = Existing();

        user.UpdateProfile("  Léa Fontaine  ", "\tFinancial Planner\n");

        Assert.Equal("Léa Fontaine", user.FullName);
        Assert.Equal("Financial Planner", user.JobTitle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_input_clears_a_previously_set_field(string? blank)
    {
        // The clear path matters as much as the set path: it is what lets a planner remove a
        // name, and what UserLookupRepository.UpsertAsync has to write through as null.
        var user = Existing();
        user.UpdateProfile("Léa Fontaine", "Financial Planner");
        user.ClearDomainEvents();

        var result = user.UpdateProfile(blank, blank);

        Assert.True(result.IsSuccess);
        Assert.Null(user.FullName);
        Assert.Null(user.JobTitle);
        Assert.Single(user.DomainEvents.OfType<UserProfileUpdatedDomainEvent>());
    }

    [Fact]
    public void A_name_at_the_maximum_length_is_accepted()
    {
        var user = Existing();
        var atLimit = new string('a', User.ProfileFieldMaxLength);

        Assert.True(user.UpdateProfile(atLimit, atLimit).IsSuccess);
        Assert.Equal(atLimit, user.FullName);
        Assert.Equal(atLimit, user.JobTitle);
    }

    [Fact]
    public void A_name_is_measured_after_trimming_not_before()
    {
        var user = Existing();
        var atLimit = new string('a', User.ProfileFieldMaxLength);

        Assert.True(user.UpdateProfile($"   {atLimit}   ", null).IsSuccess);
        Assert.Equal(atLimit, user.FullName);
    }

    [Fact]
    public void A_too_long_name_is_rejected()
    {
        var user = Existing();

        var result = user.UpdateProfile(new string('a', User.ProfileFieldMaxLength + 1), null);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.FullNameTooLong, result.Error);
    }

    [Fact]
    public void A_too_long_job_title_leaves_a_good_name_unapplied()
    {
        // Both fields are validated before either is assigned. Without that, a rejected title
        // would still have committed the name — a half-applied write reported as a failure.
        var user = Existing();

        var result = user.UpdateProfile(
            "Léa Fontaine", new string('a', User.ProfileFieldMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.JobTitleTooLong, result.Error);
        Assert.Null(user.FullName);
        Assert.Null(user.JobTitle);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void An_update_that_changes_nothing_raises_no_event()
    {
        // Every save writes an aggregate snapshot, an event-journal row and an outbox row, and
        // bumps the concurrency version. Pressing SAVE twice must not produce two of each.
        var user = Existing();
        user.UpdateProfile("Léa Fontaine", "Financial Planner");
        user.ClearDomainEvents();

        var result = user.UpdateProfile("Léa Fontaine", "Financial Planner");

        Assert.True(result.IsSuccess);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void A_no_op_is_judged_on_the_normalized_values()
    {
        // "  Léa Fontaine  " is not a change to "Léa Fontaine" — otherwise a stray space in the
        // form would publish a fresh snapshot to every replica.
        var user = Existing();
        user.UpdateProfile("Léa Fontaine", null);
        user.ClearDomainEvents();

        user.UpdateProfile("  Léa Fontaine  ", "   ");

        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void Changing_only_the_job_title_still_raises()
    {
        var user = Existing();
        user.UpdateProfile("Léa Fontaine", "Financial Planner");
        user.ClearDomainEvents();

        var result = user.UpdateProfile("Léa Fontaine", "Senior Financial Planner");

        Assert.True(result.IsSuccess);
        Assert.Equal("Léa Fontaine", user.FullName);
        var raised = Assert.Single(user.DomainEvents.OfType<UserProfileUpdatedDomainEvent>());
        Assert.Equal("Senior Financial Planner", raised.JobTitle);
    }
}
