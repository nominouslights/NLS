using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.CommunityCalendar;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Community.Queries;

internal sealed class GetCommunityCalendarQueryHandler(
    ITripRepository tripRepository,
    ICommunityCalendarBlockRepository blockRepository)
    : IRequestHandler<GetCommunityCalendarQuery, IReadOnlyList<CalendarDayResult>>
{
    private const int MinimumThreshold = 2;
    private const int DefaultSeatCapacity = 14;

    public async Task<IReadOnlyList<CalendarDayResult>> Handle(
        GetCommunityCalendarQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = today.AddDays(1);
        var windowEnd = today.AddDays(14);

        var trips = await tripRepository.GetByDateRangeAsync(
            windowStart, windowEnd, TripServiceType.Community, cancellationToken);

        var blocks = await blockRepository.GetBlocksInRangeAsync(
            windowStart, windowEnd, cancellationToken);

        var blockSet = blocks.ToDictionary(b => b.BlockedDate);
        var tripsByDate = trips
            .GroupBy(t => DateOnly.FromDateTime(t.ScheduledAt))
            .ToDictionary(g => g.Key, g => g.ToList());

        var results = new List<CalendarDayResult>();

        for (var i = 1; i <= 14; i++)
        {
            var date = today.AddDays(i);
            var isZone2 = i >= 8;

            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                results.Add(new CalendarDayResult(
                    date, date.DayOfWeek.ToString(), "Unavailable",
                    isZone2, 0, 0, 0, null, false, null));
                continue;
            }

            if (blockSet.TryGetValue(date, out var block))
            {
                results.Add(new CalendarDayResult(
                    date, date.DayOfWeek.ToString(), "Unavailable",
                    isZone2, 0, 0, 0, null, true,
                    request.IsAdmin ? block.Reason : null));
                continue;
            }

            if (!tripsByDate.TryGetValue(date, out var dayTrips) || dayTrips.Count == 0)
            {
                results.Add(new CalendarDayResult(
                    date, date.DayOfWeek.ToString(), "Open",
                    isZone2, 0, 0, DefaultSeatCapacity, null, false, null));
                continue;
            }

            // Aggregate across both direction trips for the day
            var confirmed = dayTrips.Sum(t => t.Passengers.Count(p =>
                p.PaymentStatus == PassengerPaymentStatus.Confirmed));
            var tentative = dayTrips.Sum(t => t.Passengers.Count(p =>
                p.PaymentStatus is PassengerPaymentStatus.Tentative
                    or PassengerPaymentStatus.AwaitingPayment));
            var capacity = dayTrips.Sum(t => t.SeatCapacity ?? DefaultSeatCapacity);
            var active = confirmed + tentative;
            var available = capacity - active;

            var status = confirmed >= MinimumThreshold ? "Go"
                : active > 0 ? "Building"
                : "Open";

            var firstTrip = dayTrips.First();

            results.Add(new CalendarDayResult(
                date, date.DayOfWeek.ToString(), status,
                isZone2, confirmed, tentative, available,
                firstTrip.Id, false, null));
        }

        return results.AsReadOnly();
    }
}
