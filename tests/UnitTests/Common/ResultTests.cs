using System.Reflection;
using PenguinPlank.Domain.Common;

namespace UnitTests.Common;

/// <summary>
/// Behavior of the immutable <see cref="Result"/>, <see cref="Result{T}"/>, and
/// <see cref="BusinessError"/> primitives plus the stability of the
/// <see cref="ErrorCode"/> catalog. Pure, no I/O (coding-standards §1, §7).
/// </summary>
/// <remarks>
/// These tests pin the explicit numeric <see cref="ErrorCode"/> values so a future
/// renumbering breaks the build, and assert that a failure exposes only a stable
/// code and a caller-safe message — never an exception, stack trace, or secret
/// (requirement A1 / 4.8).
/// </remarks>
public class ResultTests
{
    [Fact]
    public void Success_NonGeneric_IsSuccessAndNotFailure()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Success_NonGeneric_AccessingErrorThrowsInvalidOperation()
    {
        Result result = Result.Success();

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void FailureWithCodeAndMessage_NonGeneric_CarriesStableCodeAndMessage()
    {
        Result result = Result.Failure(ErrorCode.DuplicateSku, "SKU already exists.");

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.DuplicateSku, result.Error.Code);
        Assert.Equal("SKU already exists.", result.Error.Message);
    }

    [Fact]
    public void FailureWithBusinessError_NonGeneric_CarriesThatError()
    {
        var error = new BusinessError(ErrorCode.ArchivedRecord, "Record is archived.");

        Result result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Success_Generic_ValueReturnsSuppliedValue()
    {
        Result<int> result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Success_Generic_PreservesReferenceValueIdentity()
    {
        var payload = new object();

        Result<object> result = Result.Success(payload);

        Assert.Same(payload, result.Value);
    }

    [Fact]
    public void Success_Generic_AccessingErrorThrowsInvalidOperation()
    {
        Result<string> result = Result.Success("ok");

        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void FailureWithCodeAndMessage_Generic_CarriesStableCodeAndMessage()
    {
        Result<string> result = Result.Failure<string>(ErrorCode.StaleVersion, "Row is stale.");

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.StaleVersion, result.Error.Code);
        Assert.Equal("Row is stale.", result.Error.Message);
    }

    [Fact]
    public void FailureWithBusinessError_Generic_CarriesThatError()
    {
        var error = new BusinessError(ErrorCode.Forbidden, "Not permitted.");

        Result<int> result = Result.Failure<int>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Failure_Generic_AccessingValueThrowsInvalidOperation()
    {
        Result<int> result = Result.Failure<int>(ErrorCode.Validation, "Invalid input.");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}

/// <summary>
/// Construction and invariants of the <see cref="BusinessError"/> value object.
/// </summary>
public class BusinessErrorTests
{
    [Fact]
    public void Constructor_ValidCodeAndMessage_CarriesCodeAndMessageVerbatim()
    {
        var error = new BusinessError(ErrorCode.UploadRejected, "File too large.");

        Assert.Equal(ErrorCode.UploadRejected, error.Code);
        Assert.Equal("File too large.", error.Message);
    }

    [Fact]
    public void Constructor_NoneCode_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new BusinessError(ErrorCode.None, "message"));

        Assert.Equal("code", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new BusinessError(ErrorCode.Validation, null!));

        Assert.Equal("message", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void Constructor_EmptyOrWhitespaceMessage_ThrowsArgumentException(string message)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new BusinessError(ErrorCode.Validation, message));

        Assert.Equal("message", ex.ParamName);
    }

    [Fact]
    public void Equality_SameCodeAndMessage_AreEqual()
    {
        var first = new BusinessError(ErrorCode.MappingConflict, "Mapping conflict.");
        var second = new BusinessError(ErrorCode.MappingConflict, "Mapping conflict.");

        Assert.Equal(first, second);
    }

    [Fact]
    public void BusinessError_ExposesNoExceptionOrStackTraceMember()
    {
        // Secret/stack safety (requirement A1 / 4.8): a failure must surface only
        // a stable code and a caller-safe message. Assert the public surface is
        // exactly { Code, Message } so no exception, stack trace, or other
        // sensitive channel can leak through BusinessError.
        string[] propertyNames = typeof(BusinessError)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(2, propertyNames.Length);
        Assert.Contains(nameof(BusinessError.Code), propertyNames);
        Assert.Contains(nameof(BusinessError.Message), propertyNames);

        Assert.DoesNotContain(
            typeof(BusinessError).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => typeof(Exception).IsAssignableFrom(property.PropertyType));
    }
}

/// <summary>
/// Stability of the <see cref="ErrorCode"/> catalog. These codes become part of the
/// public API error contract (requirement A1 / 4.8): the explicit numeric values
/// must never be renumbered. Each assertion pins one member so a renumbering fails
/// the build rather than silently changing the wire contract.
/// </summary>
public class ErrorCodeStabilityTests
{
    [Theory]
    [InlineData(ErrorCode.None, 0)]
    [InlineData(ErrorCode.Validation, 1)]
    [InlineData(ErrorCode.DuplicateSku, 2)]
    [InlineData(ErrorCode.DuplicatePieceCode, 3)]
    [InlineData(ErrorCode.ArchivedRecord, 4)]
    [InlineData(ErrorCode.InvalidDimension, 5)]
    [InlineData(ErrorCode.InvalidProportion, 6)]
    [InlineData(ErrorCode.TrackingModeLocked, 7)]
    [InlineData(ErrorCode.IdempotencyConflict, 8)]
    [InlineData(ErrorCode.StaleVersion, 9)]
    [InlineData(ErrorCode.UploadRejected, 10)]
    [InlineData(ErrorCode.Forbidden, 11)]
    [InlineData(ErrorCode.MappingConflict, 12)]
    public void ErrorCode_Member_HasStableNumericValue(ErrorCode code, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)code);
    }

    [Fact]
    public void ErrorCode_Catalog_ContainsExactlyThirteenKnownMembers()
    {
        // Guards against an accidental addition or removal of a member without a
        // corresponding stable-value assertion above.
        ErrorCode[] members = Enum.GetValues<ErrorCode>();

        Assert.Equal(13, members.Length);
    }
}
