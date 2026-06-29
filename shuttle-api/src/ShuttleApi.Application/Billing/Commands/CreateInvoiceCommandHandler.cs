using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Billing;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Billing;

internal sealed class CreateInvoiceCommandHandler(
    IClientRepository clientRepository,
    ITripRepository tripRepository,
    IInvoiceRepository invoiceRepository)
    : IRequestHandler<CreateInvoiceCommand, InvoiceDetailResponse>
{
    public async Task<InvoiceDetailResponse> Handle(
        CreateInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(request.ClientId, cancellationToken)
            ?? throw new NotFoundException($"Client {request.ClientId} not found.");

        if (request.TripIds.Count == 0)
            throw new ArgumentException("At least one trip must be selected.");

        var trips = new List<Trip>(request.TripIds.Count);
        foreach (var tripId in request.TripIds)
        {
            var trip = await tripRepository.GetByIdAsync(tripId, cancellationToken)
                ?? throw new NotFoundException($"Trip {tripId} not found.");
            trips.Add(trip);
        }

        // Generate invoice number: INV-{year}-{count+1:D4}
        var year = DateTime.UtcNow.Year;
        var existingCount = await invoiceRepository.GetCountForYearAsync(year, cancellationToken);
        var invoiceNumber = $"INV-{year}-{existingCount + 1:D4}";

        var issuedDate = DateTime.UtcNow;
        var dueDate = issuedDate.AddDays(client.NetPaymentTerms > 0 ? client.NetPaymentTerms : 30);

        var invoice = Invoice.Create(client.Id, invoiceNumber, issuedDate, dueDate, request.Notes);

        // Determine rate: round-trip if 2+ trips, one-way if 1 trip
        var isRoundTrip = trips.Count >= 2;
        var passengerRate = isRoundTrip
            ? (client.RunRateRoundTrip ?? 0m)
            : (client.RunRateOneWay ?? 0m);
        var cargoRate = client.CargoRatePerKg ?? 0m;

        var sortOrder = 0;

        foreach (var trip in trips.OrderBy(t => t.ScheduledAt))
        {
            var passengerNames = trip.Passengers
                .Select(p => p.Name)
                .ToList();

            // Derive a direction string from the first passenger's Direction field, or null
            var direction = trip.Passengers
                .Select(p => p.Direction)
                .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

            var namesJoined = passengerNames.Count > 0
                ? string.Join(", ", passengerNames)
                : "No passengers";

            var passengerDescription =
                $"{trip.ScheduledAt:yyyy-MM-dd} {(direction is not null ? direction + " " : "")}Run – {namesJoined}";

            var passengerLine = InvoiceLineItem.Create(
                invoice.Id,
                trip.Id,
                InvoiceLineType.PassengerService,
                passengerDescription,
                passengerRate,
                1m,
                sortOrder++);

            invoice.AddLineItem(passengerLine);

            // Cargo line items
            foreach (var cargo in trip.CargoItems.Where(c => c.WeightKg.HasValue && c.WeightKg.Value > 0))
            {
                var cargoDescription =
                    $"{trip.ScheduledAt:yyyy-MM-dd} Cargo – {cargo.Quantity}× {cargo.CargoType} ({cargo.WeightKg}kg)";

                var cargoLine = InvoiceLineItem.Create(
                    invoice.Id,
                    trip.Id,
                    InvoiceLineType.Cargo,
                    cargoDescription,
                    cargoRate,
                    cargo.WeightKg!.Value,
                    sortOrder++);

                invoice.AddLineItem(cargoLine);
            }
        }

        await invoiceRepository.AddAsync(invoice, cancellationToken);

        return MapToDetailResponse(invoice);
    }

    private static InvoiceDetailResponse MapToDetailResponse(Invoice invoice)
    {
        var lineItems = invoice.LineItems
            .OrderBy(l => l.SortOrder)
            .Select(l => new InvoiceLineItemResponse(
                l.Id,
                l.TripId,
                l.LineType.ToString(),
                l.Description,
                l.UnitRate,
                l.Quantity,
                l.LineTotal,
                l.SortOrder))
            .ToList();

        return new InvoiceDetailResponse(
            invoice.Id,
            invoice.ClientId,
            invoice.InvoiceNumber,
            invoice.Status.ToString(),
            invoice.IssuedDate,
            invoice.DueDate,
            invoice.Notes,
            invoice.PaidAt,
            invoice.SubTotal,
            invoice.TotalAmount,
            lineItems);
    }
}
