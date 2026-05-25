using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class DeleteRateLineCommandHandler(IContractRepository contractRepository)
    : IRequestHandler<DeleteRateLineCommand>
{
    public async Task Handle(DeleteRateLineCommand request, CancellationToken cancellationToken)
    {
        var rateLine = await contractRepository.GetRateLineByIdAsync(request.RateLineId, cancellationToken)
            ?? throw new NotFoundException($"Rate line {request.RateLineId} not found.");

        await contractRepository.DeleteRateLineAsync(rateLine, cancellationToken);
    }
}
