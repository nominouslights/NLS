using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NorthernLink.Compliance.Infrastructure.Persistence;

/// <summary>
/// Maps a value (a record, a list, a dictionary) to one <c>jsonb</c> column through System.Text.Json
/// — camelCase, enums as names, so the stored document reads the way the wire does and a
/// reordered enum can never relabel history. Used for the rule-set sections and the record's
/// anchors / holds / links / basis keys. The comparer is by serialized form, which is what makes
/// a replaced immutable collection register as a change.
/// </summary>
internal static class JsonColumns
{
    public static readonly JsonSerializerOptions Options = Create();

    public static PropertyBuilder<T> HasJsonb<T>(this PropertyBuilder<T> builder)
        where T : class?
    {
        var converter = new ValueConverter<T, string>(
            value => JsonSerializer.Serialize(value, Options),
            json => JsonSerializer.Deserialize<T>(json, Options)!);

        var comparer = new ValueComparer<T>(
            (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
            value => JsonSerializer.Serialize(value, Options).GetHashCode(),
            value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!);

        return builder.HasColumnType("jsonb").HasConversion(converter, comparer);
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
