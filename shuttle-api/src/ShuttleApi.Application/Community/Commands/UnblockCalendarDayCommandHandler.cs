using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.CommunityCalendar;

namespace ShuttleApi.Application.Community.Commands;

internal sealed class UnblockCalendarDayCommandHandler(
    ICommunityCalendarBlockRepository blockRepository)
    : IRequestHandler<UnblockCalendarDayCommand>
{
    public async Task Handle(UnblockCalendarDayCommand request, CancellationToken cancellationToken)
    {
        var block = await blockRepository.GetByDateAsync(request.Date, cancellationToken)
            ?? throw new KeyNotFoundException($"No block found for {request.Date:yyyy-MM-dd}.");

        await blockRepository.RemoveAsync(block, cancellationToken);
    }
}
