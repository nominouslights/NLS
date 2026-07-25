using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Documents;

/// <summary>All domain errors the VehicleDocument aggregate (and its handlers) can produce.</summary>
public static class DocumentErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Fleet.Document.NotFound", "The document was not found.");

    public static readonly Error FileNameRequired = Error.Validation(
        "Fleet.Document.FileNameRequired", "A document file name is required.");

    public static readonly Error InvalidFileSize = Error.Validation(
        "Fleet.Document.InvalidFileSize", "The document file size cannot be negative.");
}
