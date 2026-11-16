using PenguinPlank.Application.Integrations;
using PenguinPlank.Domain.Common;

namespace UnitTests.Integrations;

/// <summary>Unit tests for mapping validation and SKU-match suggestion.</summary>
public class EntityMappingRulesTests
{
    private static readonly IReadOnlySet<string> s_noneMapped = new HashSet<string>();

    private static EntityMappingRequest ValidRequest() =>
        new(Guid.NewGuid(), "shopify", "shop-1", "ext-1", "ProductVariant", Guid.NewGuid());

    [Fact]
    public void Validate_ValidRequest_Succeeds()
    {
        Assert.True(EntityMappingValidator.Validate(ValidRequest()).IsSuccess);
    }

    [Theory]
    [InlineData("", "a", "e", "ProductVariant")]
    [InlineData("p", " ", "e", "ProductVariant")]
    [InlineData("p", "a", "", "ProductVariant")]
    [InlineData("p", "a", "e", "")]
    public void Validate_BlankField_FailsValidation(string platform, string account, string externalId, string ownerType)
    {
        EntityMappingRequest request = ValidRequest() with
        {
            Platform = platform,
            Account = account,
            ExternalId = externalId,
            OwnerType = ownerType,
        };

        Result result = EntityMappingValidator.Validate(request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    [Fact]
    public void Validate_EmptyInternalId_FailsValidation()
    {
        Result result = EntityMappingValidator.Validate(ValidRequest() with { InternalId = Guid.Empty });

        Assert.Equal(ErrorCode.Validation, result.Error.Code);
    }

    [Fact]
    public void Validate_ExternalIdTooLong_FailsValidation()
    {
        string tooLong = new('x', EntityMappingValidator.MaxIdentifierLength + 1);

        Result result = EntityMappingValidator.Validate(ValidRequest() with { ExternalId = tooLong });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Suggest_MatchingSkuIgnoringCase_ReturnsSuggestion()
    {
        var variantId = Guid.NewGuid();

        IReadOnlyList<MappingSuggestion> result = SkuMatchSuggester.Suggest(
            [new ExternalSkuCandidate("ext-1", " ab-100 ")],
            [new InternalVariantSku(variantId, "AB-100")],
            s_noneMapped);

        MappingSuggestion suggestion = Assert.Single(result);
        Assert.Equal("ext-1", suggestion.ExternalId);
        Assert.Equal(variantId, suggestion.VariantId);
        Assert.Equal("AB-100", suggestion.Sku);
    }

    [Fact]
    public void Suggest_AlreadyMappedExternal_IsSkipped()
    {
        IReadOnlyList<MappingSuggestion> result = SkuMatchSuggester.Suggest(
            [new ExternalSkuCandidate("ext-1", "AB-100")],
            [new InternalVariantSku(Guid.NewGuid(), "AB-100")],
            new HashSet<string> { "ext-1" });

        Assert.Empty(result);
    }

    [Fact]
    public void Suggest_AmbiguousInternalSku_ReturnsNothing()
    {
        IReadOnlyList<MappingSuggestion> result = SkuMatchSuggester.Suggest(
            [new ExternalSkuCandidate("ext-1", "AB-100")],
            [new InternalVariantSku(Guid.NewGuid(), "AB-100"), new InternalVariantSku(Guid.NewGuid(), "ab-100")],
            s_noneMapped);

        Assert.Empty(result);
    }

    [Fact]
    public void Suggest_BlankOrUnmatchedSku_ReturnsNothing()
    {
        IReadOnlyList<MappingSuggestion> result = SkuMatchSuggester.Suggest(
            [new ExternalSkuCandidate("ext-1", " "), new ExternalSkuCandidate("ext-2", "ZZ-1")],
            [new InternalVariantSku(Guid.NewGuid(), "AB-100")],
            s_noneMapped);

        Assert.Empty(result);
    }
}
