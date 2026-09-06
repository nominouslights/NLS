using NorthernLink.Trips.Application.Schedules;
using NorthernLink.Trips.Application.Schedules.AddException;
using NorthernLink.Trips.Application.Schedules.GetExceptions;
using NorthernLink.Trips.Application.Schedules.RemoveException;
using NorthernLink.Trips.Application.Schedules.UpdateException;
using NorthernLink.Trips.Domain.Schedules;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Application-layer coverage for the schedule-exception slices: load template → aggregate
/// method → save, template-not-found on every command, and the read-side query pass-through.
/// </summary>
public class ScheduleExceptionHandlerTests
{
    private static readonly DateOnly TreatyDays = new(2026, 8, 14);

    private readonly FakeScheduleTemplateRepository _templates = new();

    // ----- Add -----

    [Fact]
    public async Task Add_persists_through_the_aggregate_and_returns_the_new_id()
    {
        var template = TestPlanning.CreateTemplate();
        _templates.Add(template);
        var handler = new AddScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new AddScheduleExceptionCommand(template.Id, TreatyDays, ScheduleExceptionKind.Skip, null, null, "Treaty Days"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var exception = Assert.Single(template.Exceptions);
        Assert.Equal(exception.Id, result.Value);
        Assert.Equal(1, _templates.SaveCount);
    }

    [Fact]
    public async Task Add_on_a_missing_template_fails_with_NotFound_and_never_saves()
    {
        var handler = new AddScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new AddScheduleExceptionCommand(Guid.NewGuid(), TreatyDays, ScheduleExceptionKind.Skip, null, null, null),
            CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.NotFound, result.Error);
        Assert.Equal(0, _templates.SaveCount);
    }

    [Fact]
    public async Task Add_surfaces_domain_validation_failures_and_never_saves()
    {
        var template = TestPlanning.CreateTemplate();
        _templates.Add(template);
        var handler = new AddScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new AddScheduleExceptionCommand(
                template.Id, TreatyDays, ScheduleExceptionKind.ExtraRun, null, null, null),
            CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.ExtraRunDepartureRequired, result.Error);
        Assert.Empty(template.Exceptions);
        Assert.Equal(0, _templates.SaveCount);
    }

    // ----- Update -----

    [Fact]
    public async Task Update_edits_the_exception_and_saves()
    {
        var template = TestPlanning.CreateTemplate();
        var exceptionId = template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, null).Value;
        _templates.Add(template);
        var handler = new UpdateScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new UpdateScheduleExceptionCommand(
                template.Id, exceptionId, TreatyDays, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var exception = Assert.Single(template.Exceptions);
        Assert.Equal(ScheduleExceptionKind.ExtraRun, exception.Kind);
        Assert.Equal(new TimeOnly(9, 0), exception.DepartureTime);
        Assert.Equal(1, _templates.SaveCount);
    }

    [Fact]
    public async Task Update_on_a_missing_template_fails_with_NotFound()
    {
        var handler = new UpdateScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new UpdateScheduleExceptionCommand(
                Guid.NewGuid(), Guid.NewGuid(), TreatyDays, ScheduleExceptionKind.Skip, null, null, null),
            CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Update_of_a_missing_exception_fails_with_ExceptionNotFound_and_never_saves()
    {
        var template = TestPlanning.CreateTemplate();
        _templates.Add(template);
        var handler = new UpdateScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new UpdateScheduleExceptionCommand(
                template.Id, Guid.NewGuid(), TreatyDays, ScheduleExceptionKind.Skip, null, null, null),
            CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.ExceptionNotFound, result.Error);
        Assert.Equal(0, _templates.SaveCount);
    }

    // ----- Remove -----

    [Fact]
    public async Task Remove_deletes_the_exception_and_saves()
    {
        var template = TestPlanning.CreateTemplate();
        var exceptionId = template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, null).Value;
        _templates.Add(template);
        var handler = new RemoveScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new RemoveScheduleExceptionCommand(template.Id, exceptionId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(template.Exceptions);
        Assert.Equal(1, _templates.SaveCount);
    }

    [Fact]
    public async Task Remove_on_a_missing_template_fails_with_NotFound()
    {
        var handler = new RemoveScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new RemoveScheduleExceptionCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Remove_of_a_missing_exception_fails_with_ExceptionNotFound_and_never_saves()
    {
        var template = TestPlanning.CreateTemplate();
        _templates.Add(template);
        var handler = new RemoveScheduleExceptionCommandHandler(_templates);

        var result = await handler.Handle(
            new RemoveScheduleExceptionCommand(template.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.ExceptionNotFound, result.Error);
        Assert.Equal(0, _templates.SaveCount);
    }

    // ----- Get -----

    [Fact]
    public async Task Get_returns_only_the_requested_templates_exceptions()
    {
        var templateId = Guid.NewGuid();
        var readService = new FakeScheduleTemplateReadService();
        readService.Exceptions.Add(new ScheduleExceptionResponse(
            Guid.NewGuid(), templateId, "2026-08-14", "Skip", null, null, "Treaty Days",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        readService.Exceptions.Add(new ScheduleExceptionResponse(
            Guid.NewGuid(), Guid.NewGuid(), "2026-08-15", "Skip", null, null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var handler = new GetScheduleExceptionsQueryHandler(readService);

        var result = await handler.Handle(
            new GetScheduleExceptionsQuery(TestPlanning.TenantId, templateId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(templateId, row.ScheduleTemplateId);
        Assert.Equal("2026-08-14", row.Date);
        Assert.Equal("Skip", row.Kind);
    }
}
