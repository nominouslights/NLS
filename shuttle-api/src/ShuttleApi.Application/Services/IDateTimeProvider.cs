namespace ShuttleApi.Application.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
