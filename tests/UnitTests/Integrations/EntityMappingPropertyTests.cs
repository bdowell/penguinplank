using CsCheck;
using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;

namespace UnitTests.Integrations;

/// <summary>
/// Properties 10 and 22 (requirements 3.4, 3.5) over the pure mapping rules.
/// <list type="number">
///   <item><b>Property 10: EntityMapping uniqueness and identifier preservation.</b> The pure part is
///   <see cref="EntityMappingValidator"/>: every well-formed (platform, account, external id,
///   owner type, internal id) request is accepted and left unchanged, and any request with a
///   missing, blank, or over-long identifier is rejected with <see cref="ErrorCode.Validation"/>
///   before it can reach storage.</item>
///   <item><b>Property 22: SKU matches are suggestions, not silent mappings.</b>
///   <see cref="SkuMatchSuggester"/> returns only <see cref="MappingSuggestion"/> values (never an
///   <see cref="EntityMappingRecord"/>). Each suggestion is sound (a unique, case- and
///   whitespace-insensitive SKU match for a candidate that is not already mapped) and the set is
///   complete for unambiguous matches.</item>
/// </list>
/// </summary>
/// <remarks>
/// Rejection of a duplicate (platform, account, external id) triple is decided by the composite
/// unique index in SQL Server, so that half of Property 10 and the "suggesting writes nothing" half
/// of Property 22 are covered against real SQL Server by <c>EntityMappingStoreTests</c> in the
/// integration test project, not here. CsCheck (pinned in <c>UnitTests.csproj</c>) runs each
/// property at <see cref="Iterations"/> samples.
/// </remarks>
public class EntityMappingPropertyTests
{
    private const int Iterations = 500;

    private static readonly IReadOnlySet<string> s_noneMapped = new HashSet<string>();

    private static readonly Gen<string> s_genText = Gen.String[Gen.Char.AlphaNumeric, 1, 12];

    private static readonly Gen<Guid> s_genNonEmptyGuid = Gen.Guid.Where(id => id != Guid.Empty);

    private static readonly Gen<EntityMappingRequest> s_genValidRequest =
        Gen.Select(
                s_genNonEmptyGuid,
                Gen.String[Gen.Char.AlphaNumeric, 1, EntityMappingValidator.MaxPlatformLength],
                Gen.String[Gen.Char.AlphaNumeric, 1, EntityMappingValidator.MaxIdentifierLength],
                Gen.String[Gen.Char.AlphaNumeric, 1, EntityMappingValidator.MaxIdentifierLength],
                Gen.String[Gen.Char.AlphaNumeric, 1, EntityMappingValidator.MaxPlatformLength],
                s_genNonEmptyGuid)
            .Select(parts => new EntityMappingRequest(
                parts.Item1,
                parts.Item2,
                parts.Item3,
                parts.Item4,
                parts.Item5,
                parts.Item6));

    /// <summary>A set of internal SKUs that are pairwise distinct ignoring case.</summary>
    private static readonly Gen<List<string>> s_genDistinctSkus =
        s_genText.List[1, 8].Select(skus => skus.DistinctBy(sku => sku, StringComparer.OrdinalIgnoreCase).ToList());

    /// <summary>
    /// Internal variants with distinct SKUs, one external candidate per variant whose SKU is a
    /// re-cased or padded spelling of the internal SKU, and a random mapped flag per candidate.
    /// </summary>
    private static readonly Gen<MatchScenario> s_genMatchScenario =
        s_genDistinctSkus.SelectMany(skus =>
            Gen.Select(Gen.Int[0, 3], Gen.Bool).List[skus.Count]
                .Select(choices => BuildScenario(skus, choices)));

    [Fact]
    public void Validate_WellFormedRequest_IsAcceptedAndUnchanged()
    {
        s_genValidRequest.Sample(
            request =>
            {
                EntityMappingRequest original = request with { };

                Result result = EntityMappingValidator.Validate(request);

                Assert.True(result.IsSuccess);
                Assert.Equal(original, request);
            },
            iter: Iterations);
    }

    [Fact]
    public void Validate_BlankOrOverlongIdentifier_RejectsWithValidation()
    {
        Gen.Select(s_genValidRequest, Gen.Int[0, 4], Gen.Bool)
            .Sample(
                sample =>
                {
                    EntityMappingRequest invalid = Corrupt(sample.Item1, sample.Item2, sample.Item3);

                    Result result = EntityMappingValidator.Validate(invalid);

                    Assert.True(result.IsFailure);
                    Assert.Equal(ErrorCode.Validation, result.Error.Code);
                },
                iter: Iterations);
    }

    [Fact]
    public void Suggest_AnyInputs_EverySuggestionIsSoundAndUnmapped()
    {
        Gen.Select(
                s_genText.List[0, 6],
                s_genText.List[0, 6],
                Gen.Int[0, 3].List[6],
                Gen.Bool.List[6],
                Gen.Bool.List[6])
            .Sample(
                sample =>
                {
                    List<InternalVariantSku> variants = sample.Item1
                        .Select(sku => new InternalVariantSku(Guid.NewGuid(), sku))
                        .ToList();
                    List<ExternalSkuCandidate> candidates = sample.Item2
                        .Select((sku, index) => new ExternalSkuCandidate($"ext-{index}", Mutate(sku, sample.Item3[index])))
                        .ToList();
                    HashSet<string> mapped = candidates
                        .Where((_, index) => sample.Item4[index])
                        .Select(candidate => candidate.ExternalId)
                        .ToHashSet();

                    IReadOnlyList<MappingSuggestion> suggestions = SkuMatchSuggester.Suggest(candidates, variants, mapped);

                    Assert.Equal(suggestions.Count, suggestions.Select(s => s.ExternalId).Distinct().Count());
                    foreach (MappingSuggestion suggestion in suggestions)
                    {
                        ExternalSkuCandidate candidate = Assert.Single(candidates, c => c.ExternalId == suggestion.ExternalId);
                        Assert.DoesNotContain(suggestion.ExternalId, mapped);
                        InternalVariantSku match = Assert.Single(
                            variants,
                            v => string.Equals(v.Sku.Trim(), candidate.Sku.Trim(), StringComparison.OrdinalIgnoreCase));
                        Assert.Equal(match.VariantId, suggestion.VariantId);
                        Assert.Equal(match.Sku, suggestion.Sku);
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void Suggest_UnambiguousMatches_SuggestsExactlyTheUnmappedCandidates()
    {
        s_genMatchScenario.Sample(
            scenario =>
            {
                IReadOnlyList<MappingSuggestion> suggestions =
                    SkuMatchSuggester.Suggest(scenario.Candidates, scenario.Variants, scenario.Mapped);

                Dictionary<string, Guid> expected = scenario.ExpectedVariantByExternalId;
                Assert.Equal(expected.Count, suggestions.Count);
                Assert.All(suggestions, suggestion => Assert.Equal(expected[suggestion.ExternalId], suggestion.VariantId));
            },
            iter: Iterations);
    }

    [Fact]
    public void Suggest_EveryExternalAlreadyMapped_ReturnsNothing()
    {
        s_genMatchScenario.Sample(
            scenario =>
            {
                HashSet<string> allMapped = scenario.Candidates.Select(candidate => candidate.ExternalId).ToHashSet();

                IReadOnlyList<MappingSuggestion> suggestions =
                    SkuMatchSuggester.Suggest(scenario.Candidates, scenario.Variants, allMapped);

                Assert.Empty(suggestions);
            },
            iter: Iterations);
    }

    [Fact]
    public void Suggest_InternalSkuAppearsTwice_ReturnsNothingForThatSku()
    {
        Gen.Select(s_genText, Gen.Int[0, 3])
            .Sample(
                sample =>
                {
                    InternalVariantSku[] variants =
                    [
                        new(Guid.NewGuid(), sample.Item1),
                        new(Guid.NewGuid(), sample.Item1.ToUpperInvariant()),
                    ];

                    IReadOnlyList<MappingSuggestion> suggestions = SkuMatchSuggester.Suggest(
                        [new ExternalSkuCandidate("ext-1", Mutate(sample.Item1, sample.Item2))],
                        variants,
                        s_noneMapped);

                    Assert.Empty(suggestions);
                },
                iter: Iterations);
    }

    private static MatchScenario BuildScenario(List<string> skus, List<(int Style, bool Mapped)> choices)
    {
        List<InternalVariantSku> variants = skus.Select(sku => new InternalVariantSku(Guid.NewGuid(), sku)).ToList();
        var candidates = new List<ExternalSkuCandidate>();
        var mapped = new HashSet<string>();
        var expected = new Dictionary<string, Guid>();

        for (int index = 0; index < variants.Count; index++)
        {
            string externalId = $"ext-{index}";
            candidates.Add(new ExternalSkuCandidate(externalId, Mutate(variants[index].Sku, choices[index].Style)));
            if (choices[index].Mapped)
            {
                mapped.Add(externalId);
            }
            else
            {
                expected[externalId] = variants[index].VariantId;
            }
        }

        return new MatchScenario(variants, candidates, mapped, expected);
    }

    /// <summary>Re-cases or pads a SKU so only case/whitespace-insensitive matching can find it.</summary>
    private static string Mutate(string sku, int style)
    {
        return style switch
        {
            0 => sku,
            1 => sku.ToUpperInvariant(),
            2 => " " + sku.ToLowerInvariant() + " ",
            _ => sku.ToLowerInvariant() + "\t",
        };
    }

    /// <summary>Breaks one field of a valid request: blank, or longer than the column allows.</summary>
    private static EntityMappingRequest Corrupt(EntityMappingRequest request, int field, bool tooLong)
    {
        string Bad(int maxLength) => tooLong ? new string('x', maxLength + 1) : " ";

        return field switch
        {
            0 => request with { Platform = Bad(EntityMappingValidator.MaxPlatformLength) },
            1 => request with { Account = Bad(EntityMappingValidator.MaxIdentifierLength) },
            2 => request with { ExternalId = Bad(EntityMappingValidator.MaxIdentifierLength) },
            3 => request with { OwnerType = Bad(EntityMappingValidator.MaxPlatformLength) },
            _ => request with { InternalId = Guid.Empty },
        };
    }

    private sealed record MatchScenario(
        IReadOnlyList<InternalVariantSku> Variants,
        IReadOnlyList<ExternalSkuCandidate> Candidates,
        IReadOnlySet<string> Mapped,
        Dictionary<string, Guid> ExpectedVariantByExternalId);
}
