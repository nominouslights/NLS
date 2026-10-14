using System.Reflection;
using System.Runtime.CompilerServices;
using NorthernLink.Compliance.Application.RegulatoryBasis;
using NorthernLink.Compliance.Domain;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// Every refusal explains itself, and every explanation exists: each <c>Error</c> the module
/// defines carries at least one regulatory-basis key, every key referenced anywhere (errors,
/// record types, the <see cref="BasisKeys"/> constants) exists in <see cref="RegulatoryBasisSeed"/>,
/// and the seed and the constants agree exactly.
/// </summary>
public class RegulatoryBasisCoverageTests
{
    private static readonly Assembly Compliance = typeof(ComplianceError).Assembly;

    private static readonly HashSet<string> SeedKeys = RegulatoryBasisSeed.All.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

    private static readonly IReadOnlyList<string> ConstantKeys = typeof(BasisKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    private static IReadOnlyList<Type> ErrorsTypes() => Compliance.GetTypes()
        .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && t.Name.EndsWith("Errors", StringComparison.Ordinal))
        .ToList();

    private static IReadOnlyList<(Type Owner, string Field, Error Error)> AllDefinedErrors()
    {
        var errors = new List<(Type, string, Error)>();
        foreach (var type in ErrorsTypes())
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(Error)))
            {
                errors.Add((type, field.Name, (Error)field.GetValue(null)!));
            }
        }

        return errors;
    }

    [Fact]
    public void The_module_defines_errors_and_every_one_is_registered_with_a_basis()
    {
        var errors = AllDefinedErrors();

        Assert.NotEmpty(errors);
        foreach (var (owner, field, error) in errors)
        {
            var keys = ComplianceError.BasisKeysFor(error.Code);
            Assert.True(keys.Count > 0,
                $"{owner.Name}.{field} ({error.Code}) was defined without ComplianceError — it carries no regulatory basis.");
        }
    }

    [Fact]
    public void Every_error_basis_key_exists_in_the_seed_catalogue()
    {
        AllDefinedErrors();

        var missing = ComplianceError.Registered
            .SelectMany(pair => pair.Value.Select(key => (pair.Key, key)))
            .Where(pair => !SeedKeys.Contains(pair.key))
            .ToList();

        Assert.True(missing.Count == 0, "Errors cite keys the seed lacks: " + string.Join(", ", missing.Select(m => $"{m.Key} → {m.key}")));
    }

    [Fact]
    public void Every_record_type_basis_key_exists_in_the_seed_catalogue()
    {
        var missing = ComplianceRecordCatalogue.All
            .SelectMany(d => d.RegulatoryBasisKeys.Select(key => (d.Code, key)))
            .Where(pair => !SeedKeys.Contains(pair.key))
            .ToList();

        Assert.True(missing.Count == 0, "Record types cite keys the seed lacks: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_BasisKeys_constants_and_the_seed_name_exactly_the_same_keys()
    {
        Assert.Equal(ConstantKeys.Count, ConstantKeys.Distinct().Count());
        Assert.Equal(ConstantKeys.Order(), SeedKeys.Order());
    }

    [Fact]
    public void Seed_keys_are_unique_and_the_count_matches_the_verified_source_list()
    {
        // 86 keys from the verified regulatory-sources list + 35 per-section quotation entries
        // (NSC 9/10/11/13 sections, M.R. subsections, SOR 85(3)) + 22 NL-SP-02 section entries,
        // then Epic 2: MTI.new-carrier + NL-SP-02 s. 1, the Declaration and the Designations.
        Assert.Equal(RegulatoryBasisSeed.All.Count, SeedKeys.Count);
        Assert.Equal(147, RegulatoryBasisSeed.All.Count);
        Assert.Equal(25, RegulatoryBasisSeed.All.Count(d => d.Instrument == RegulatoryInstrument.SafetyPlan));
    }

    [Fact]
    public void Every_statutory_and_guidance_entry_carries_a_verified_source_url()
    {
        var withoutUrl = RegulatoryBasisSeed.All
            .Where(d => d.Instrument != RegulatoryInstrument.SafetyPlan && d.Instrument != RegulatoryInstrument.CompanyPolicy)
            .Where(d => string.IsNullOrWhiteSpace(d.SourceUrl))
            .Select(d => d.Key)
            .ToList();

        Assert.True(withoutUrl.Count == 0, "Entries without a source URL: " + string.Join(", ", withoutUrl));
        Assert.All(RegulatoryBasisSeed.All, d => Assert.False(string.IsNullOrWhiteSpace(d.PlainExplanation), $"{d.Key} has no plain explanation"));
    }

    [Theory]
    [InlineData(BasisKeys.Hta317)]
    [InlineData(BasisKeys.Hta322_1_3)]
    [InlineData(BasisKeys.Hta265_1_1)]
    [InlineData(BasisKeys.Nsc9_12)]
    [InlineData(BasisKeys.Nsc9_14)]
    [InlineData(BasisKeys.Nsc9_25_26)]
    public void The_consequences_the_plan_states_are_recorded(string key) =>
        Assert.False(string.IsNullOrWhiteSpace(RegulatoryBasisSeed.All.Single(d => d.Key == key).Consequence));

    [Theory]
    [InlineData(BasisKeys.Mr95_2008_7_3)]
    [InlineData(BasisKeys.Mr31_2019_2_4_27)]
    [InlineData(BasisKeys.Mr37_2005)]
    [InlineData(BasisKeys.Mr28_2019)]
    [InlineData(BasisKeys.Hta318_1_3)]
    public void Citation_corrections_against_the_consolidations_carry_the_plans_original_wording(string key) =>
        Assert.False(string.IsNullOrWhiteSpace(RegulatoryBasisSeed.All.Single(d => d.Key == key).PlanCitationNote));

    [Fact]
    public void Quotations_are_kept_verbatim_where_the_plan_quotes()
    {
        var bus = RegulatoryBasisSeed.All.Single(d => d.Key == BasisKeys.HtaBus);
        Assert.Equal("“‘bus’ means a motor vehicle designed to carry 11 or more persons including the driver;”", bus.Quotation);
    }

    [Fact]
    public void Defining_an_error_without_a_basis_is_refused_at_definition()
    {
        Assert.Throws<InvalidOperationException>(() => ComplianceError.Validation("Compliance.Test.NoBasis", "nope"));
    }
}
