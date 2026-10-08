using PenguinPlank.Api.Errors;
using PenguinPlank.Domain.Common;

namespace UnitTests.Errors;

/// <summary>
/// Behavior of the pure <see cref="ErrorCodeHttpMapping"/> table that maps a Domain
/// <see cref="ErrorCode"/> to its HTTP status and stable machine-readable code
/// (requirement A1 §4.8).
/// </summary>
/// <remarks>
/// The mapping is the single HTTP contract both the API middleware and the Property 24 test rely
/// on, so these tests pin the status for each code and prove the table has no gaps — exercised
/// directly, with no application startup (coding-standards §1, §7).
/// </remarks>
public class ErrorCodeHttpMappingTests
{
    [Theory]
    [InlineData(ErrorCode.Validation, 400)]
    [InlineData(ErrorCode.InvalidDimension, 400)]
    [InlineData(ErrorCode.InvalidProportion, 400)]
    [InlineData(ErrorCode.Forbidden, 403)]
    [InlineData(ErrorCode.DuplicateSku, 409)]
    [InlineData(ErrorCode.DuplicatePieceCode, 409)]
    [InlineData(ErrorCode.ArchivedRecord, 409)]
    [InlineData(ErrorCode.TrackingModeLocked, 409)]
    [InlineData(ErrorCode.IdempotencyConflict, 409)]
    [InlineData(ErrorCode.MappingConflict, 409)]
    [InlineData(ErrorCode.StaleVersion, 412)]
    [InlineData(ErrorCode.UploadRejected, 413)]
    public void Map_KnownErrorCode_ReturnsExpectedStatus(ErrorCode errorCode, int expectedStatus)
    {
        ErrorCodeHttpMapping mapping = ErrorCodeHttpMapping.Map(errorCode);

        Assert.Equal(expectedStatus, mapping.StatusCode);
    }

    [Fact]
    public void Map_KnownErrorCode_CodeIsTheInvariantMemberName()
    {
        ErrorCodeHttpMapping mapping = ErrorCodeHttpMapping.Map(ErrorCode.DuplicateSku);

        // The stable code string is part of the public error contract; it is the member name.
        Assert.Equal(nameof(ErrorCode.DuplicateSku), mapping.Code);
    }

    [Fact]
    public void Map_EveryFailureErrorCode_HasAMappingWithNoGaps()
    {
        foreach (ErrorCode errorCode in Enum.GetValues<ErrorCode>())
        {
            if (errorCode == ErrorCode.None)
            {
                continue;
            }

            ErrorCodeHttpMapping mapping = ErrorCodeHttpMapping.Map(errorCode);

            Assert.InRange(mapping.StatusCode, 400, 599);
            Assert.False(string.IsNullOrWhiteSpace(mapping.Code));
        }
    }

    [Fact]
    public void Map_EveryFailureErrorCode_ProducesAUniqueStableCode()
    {
        List<string> codes = Enum.GetValues<ErrorCode>()
            .Where(code => code != ErrorCode.None)
            .Select(code => ErrorCodeHttpMapping.Map(code).Code)
            .ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Map_NoneSentinel_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorCodeHttpMapping.Map(ErrorCode.None));
    }

    [Fact]
    public void Map_UndefinedErrorCode_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ErrorCodeHttpMapping.Map((ErrorCode)9999));
    }
}
