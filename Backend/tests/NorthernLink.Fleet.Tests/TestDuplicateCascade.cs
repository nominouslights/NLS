using Microsoft.Extensions.Logging;
using NorthernLink.Fleet.Application.Inspections;

namespace NorthernLink.Fleet.Tests;

/// <summary>Builds the real <see cref="DuplicateDefectCascade"/> over the in-memory fakes.</summary>
internal static class TestDuplicateCascade
{
    public static DuplicateDefectCascade Over(
        InMemoryVehicleInspectionRepository inspections,
        InMemoryVehicleRepository? vehicles = null,
        CapturingLogger<DuplicateDefectCascade>? logger = null) =>
        new(inspections, vehicles ?? new InMemoryVehicleRepository(), logger ?? new CapturingLogger<DuplicateDefectCascade>());
}

/// <summary>An <see cref="ILogger{T}"/> that keeps every entry, so a test can assert what was logged.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));
}
