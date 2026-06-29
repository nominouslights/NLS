using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Billing;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Billing;

internal sealed class ListBillingReadyTripsQueryHandler(
    ITripRepository tripRepository,
    IInvoiceRepository invoiceRepository)
    : IRequestHandler<ListBillingReadyTripsQuery, List<BillingReadyTripResponse>>
{
    public async Task<List<BillingReadyTripResponse>> Handle(
        ListBillingReadyTripsQuery request,
        CancellationToken cancellationToken)
    {
        var invoicedTripIds = await invoiceRepository.GetInvoicedTripIdsAsync(cancellationToken);

        var trips = await tripRepository.GetAllAsync(
            status: TripStatus.Completed,
            clientId: request.ClientId,
            cancellationToken: cancellationToken);

        var results = new List<BillingReadyTripResponse>();

        foreach (var trip in trips)
        {
            if (trip.PostReport is null || !trip.PostReport.IsReadyToInvoice)
                continue;

            if (invoicedTripIds.Contains(trip.Id))
                continue;

            var passengerNames = trip.Passengers
                .Select(p => p.Name)
                .ToList();

            var direction = trip.Passengers
                .Select(p => p.Direction)
                .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

            decimal? totalCargoWeightKg = trip.CargoItems.Any(c => c.WeightKg.HasValue)
                ? trip.CargoItems
                    .Where(c => c.WeightKg.HasValue)
                    .Sum(c => c.WeightKg!.Value)
                : null;

            string? cargoSummary = null;
            if (trip.CargoItems.Count > 0)
            {
                cargoSummary = string.Join(", ", trip.CargoItems
                    .Select(c => $"{c.Quantity}× {c.CargoType}" +
                                 (c.WeightKg.HasValue ? $" ({c.WeightKg}kg)" : string.Empty)));
            }

            results.Add(new BillingReadyTripResponse(
                trip.Id,
                trip.ScheduledAt,
                direction,
                passengerNames,
                totalCargoWeightKg,
                cargoSummary));
        }

        return results.OrderBy(r => r.ScheduledAt).ToList();
    }
}
