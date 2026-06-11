using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Common.Interfaces;

public interface IClientTripNotifier
{
    Task NotifyDepartureArrivalAsync(
        Trip trip,
        ClientEmailTemplateType type,
        string status,
        string? stopLocation = null,
        CancellationToken cancellationToken = default);
}
