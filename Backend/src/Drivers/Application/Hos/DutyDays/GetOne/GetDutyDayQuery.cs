using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.GetOne;

/// <summary>One day's summary. A day after today is not found; a day with no record is returned with <c>hasRecords = false</c>.</summary>
public sealed record GetDutyDayQuery(Guid TenantId, Guid DriverId, DateOnly LocalDate) : IQuery<DutyDaySummaryResponse>;
