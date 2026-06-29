using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Common.Interfaces;

public interface IDispatchConfirmationSender
{
    Task SendAllAsync(Trip trip, CancellationToken cancellationToken);
}
