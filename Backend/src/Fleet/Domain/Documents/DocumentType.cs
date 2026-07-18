namespace NorthernLink.Fleet.Domain.Documents;

/// <summary>
/// Category of a vehicle compliance document. Serialized as the enum name; the frontend
/// maps names to display labels ("InsuranceMpi" → "Insurance / MPI", etc.).
/// </summary>
public enum DocumentType
{
    Registration,
    InsuranceMpi,
    NscSafetyCertificate,
    Emissions,
    BillOfSale,
    Warranty,
    Other,
}
