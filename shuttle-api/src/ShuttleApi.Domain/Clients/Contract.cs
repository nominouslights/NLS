using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Clients;

public sealed class Contract : Entity<Guid>
{
    private readonly List<ContractRateLine> _rateLines = [];

    public Guid ClientId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public string? Notes { get; private set; }
    public IReadOnlyList<ContractRateLine> RateLines => _rateLines.AsReadOnly();

    public bool IsExpiringSoon => EndDate <= DateTime.UtcNow.AddDays(60);

    private Contract() { }

    public static Contract Create(Guid clientId, DateTime startDate, DateTime endDate, string? notes)
    {
        return new Contract
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true,
            Notes = notes
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Update(DateTime startDate, DateTime endDate, string? notes)
    {
        StartDate = startDate;
        EndDate = endDate;
        Notes = notes;
    }
}
