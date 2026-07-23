namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Reads a secret (connection string, signing key, credential) directly from the process
/// environment, bypassing appsettings.json entirely — secrets never live in a committed file.
/// Local standalone runs set these in launchSettings.json; orchestrated runs get them from
/// AppHost.cs; production sets them on the host/container itself.
/// </summary>
public static class RequiredEnvironmentVariable
{
    public static string Get(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"The {name} environment variable is not set.");
        }

        return value;
    }
}
