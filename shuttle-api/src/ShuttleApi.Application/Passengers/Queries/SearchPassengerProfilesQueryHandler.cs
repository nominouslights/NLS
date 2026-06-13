using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Passengers;

namespace ShuttleApi.Application.Passengers.Queries;

internal sealed class SearchPassengerProfilesQueryHandler(IPassengerProfileRepository repository)
    : IRequestHandler<SearchPassengerProfilesQuery, IReadOnlyList<PassengerProfileResult>>
{
    public async Task<IReadOnlyList<PassengerProfileResult>> Handle(
        SearchPassengerProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var profiles = await repository.SearchAsync(
            request.ClientId,
            request.Query,
            cancellationToken);

        return profiles
            .Select(p => new PassengerProfileResult(p.Id, p.Name, p.Phone, p.Email, p.LastBookedAt))
            .ToList();
    }
}
