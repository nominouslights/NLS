using System.Collections.Concurrent;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain;

/// <summary>
/// The module's one way to define an <see cref="Error"/>: every Compliance error is created
/// here with at least one regulatory-basis key, so a refusal can explain <em>which rule</em> it
/// is enforcing. The returned value is an ordinary Shared <see cref="Error"/> (the Result pipeline
/// and <c>EndpointResults</c> stay unchanged); the basis keys live in a side registry keyed on the
/// error code, which the module's own <c>EndpointResults</c> reads to add a <c>basis</c> array to
/// the error body and which <c>RegulatoryBasisCoverageTests</c> walks to prove every error is
/// grounded and every key exists in the seed catalogue.
/// <para>
/// Registration happens when the static <c>*Errors</c> field initialises, so the registry is
/// complete once every <c>*Errors</c> type has been touched — the coverage test forces that.
/// Errors are therefore <b>always static</b> in this module: a per-request error built inside a
/// handler would register late and could carry no keys the test can see.
/// </para>
/// </summary>
public static class ComplianceError
{
    private static readonly ConcurrentDictionary<string, string[]> BasisByCode = new(StringComparer.Ordinal);

    public static Error NotFound(string code, string message, params string[] basisKeys) =>
        Register(Error.NotFound(code, message), basisKeys);

    public static Error Conflict(string code, string message, params string[] basisKeys) =>
        Register(Error.Conflict(code, message), basisKeys);

    public static Error Validation(string code, string message, params string[] basisKeys) =>
        Register(Error.Validation(code, message), basisKeys);

    /// <summary>The basis keys registered for an error code; empty for a code this module never defined.</summary>
    public static IReadOnlyList<string> BasisKeysFor(string code) =>
        BasisByCode.TryGetValue(code, out var keys) ? keys : [];

    /// <summary>Every registered code with its keys — the coverage test's input.</summary>
    public static IReadOnlyDictionary<string, string[]> Registered => BasisByCode;

    private static Error Register(Error error, string[] basisKeys)
    {
        if (basisKeys.Length == 0)
        {
            throw new InvalidOperationException(
                $"Compliance error {error.Code} was defined without a regulatory-basis key. Every refusal must cite the rule it enforces.");
        }

        BasisByCode[error.Code] = basisKeys;
        return error;
    }
}
