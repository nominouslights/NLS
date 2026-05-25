using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Drivers;

public sealed class Driver : AggregateRoot<Guid>
{
    private readonly List<DriverDocument> _documents = [];
    private readonly List<DriverRosterEntry> _rosterEntries = [];

    public string EmployeeId { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime HireDate { get; private set; }
    public DriverStatus Status { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<DriverDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyList<DriverRosterEntry> RosterEntries => _rosterEntries.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    private Driver() { }

    public static Driver Create(
        string employeeId,
        string firstName,
        string lastName,
        string phone,
        string email,
        DateTime hireDate)
    {
        return new Driver
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            Email = email,
            HireDate = hireDate,
            Status = DriverStatus.Available,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string employeeId,
        string firstName,
        string lastName,
        string phone,
        string email,
        DateTime hireDate,
        bool isActive)
    {
        EmployeeId = employeeId;
        FirstName = firstName;
        LastName = lastName;
        Phone = phone;
        Email = email;
        HireDate = hireDate;
        IsActive = isActive;
    }

    public void SetStatus(DriverStatus status) => Status = status;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void AddDocument(DriverDocument document) => _documents.Add(document);

    public void RemoveDocument(Guid documentId)
    {
        var doc = _documents.FirstOrDefault(d => d.Id == documentId);
        if (doc is not null)
            _documents.Remove(doc);
    }

    public void AddOrUpdateRosterEntry(DriverRosterEntry entry)
    {
        var existing = _rosterEntries.FirstOrDefault(r => r.EntryDate == entry.EntryDate);
        if (existing is not null)
            _rosterEntries.Remove(existing);
        _rosterEntries.Add(entry);
    }

    public void RemoveRosterEntry(Guid entryId)
    {
        var entry = _rosterEntries.FirstOrDefault(r => r.Id == entryId);
        if (entry is not null)
            _rosterEntries.Remove(entry);
    }
}
