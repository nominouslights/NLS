namespace ShuttleApi.Domain.Common;

public sealed class ConflictException(string message) : Exception(message);
