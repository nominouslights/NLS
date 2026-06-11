using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Common.Interfaces;

public interface IEmailTemplateRenderer
{
    string Render(string template, EmailTemplateContext context);
}

public sealed record EmailTemplateContext
{
    public required Trip Trip { get; init; }
    public Client? Client { get; init; }
    public TripPassenger? Passenger { get; init; }
    public string? Status { get; init; }
    public string? StopLocation { get; init; }
}
