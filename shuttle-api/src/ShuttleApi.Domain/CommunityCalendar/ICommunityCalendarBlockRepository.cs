namespace ShuttleApi.Domain.CommunityCalendar;

public interface ICommunityCalendarBlockRepository
{
    Task<IReadOnlyList<CommunityCalendarBlock>> GetBlocksInRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<CommunityCalendarBlock?> GetByDateAsync(
        DateOnly date, CancellationToken cancellationToken = default);

    Task AddAsync(
        CommunityCalendarBlock block, CancellationToken cancellationToken = default);

    Task RemoveAsync(
        CommunityCalendarBlock block, CancellationToken cancellationToken = default);
}
