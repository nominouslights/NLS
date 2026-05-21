namespace ShuttleApi.Domain.Common;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string paramName) =>
        value ?? throw new ArgumentNullException(paramName);

    public static string AgainstNullOrEmpty(string? value, string paramName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be null or empty.", paramName)
            : value;

    public static T AgainstOutOfRange<T>(T value, string paramName, T min, T max)
        where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be between {min} and {max}.");
        return value;
    }

    public static void Against(bool condition, string message)
    {
        if (condition) throw new InvalidOperationException(message);
    }
}
