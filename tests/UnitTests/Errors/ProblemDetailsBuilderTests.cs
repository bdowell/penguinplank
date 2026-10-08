using Microsoft.AspNetCore.Mvc;
using PenguinPlank.Api.Errors;
using PenguinPlank.Domain.Common;

namespace UnitTests.Errors;

/// <summary>
/// Behavior of the pure <see cref="ProblemDetailsBuilder"/> that renders business failures and
/// unexpected failures into RFC 7807 bodies (requirement A1 §4.8, A6 §8.5).
/// </summary>
/// <remarks>
/// These tests assert the two invariants a client depends on — a stable <c>errorCode</c> plus a
/// <c>correlationId</c> on every body, and no internal detail on the generic 500 — without any
/// <c>HttpContext</c> or application startup (coding-standards §1, §7).
/// </remarks>
public class ProblemDetailsBuilderTests
{
    private const string CorrelationId = "corr-123";

    [Fact]
    public void FromBusinessError_SetsMappedStatusStableCodeAndCorrelationId()
    {
        var error = new BusinessError(ErrorCode.DuplicateSku, "SKU already exists.");

        ProblemDetails problem = ProblemDetailsBuilder.FromBusinessError(error, CorrelationId);

        Assert.Equal(409, problem.Status);
        Assert.Equal(nameof(ErrorCode.DuplicateSku), problem.Extensions[ProblemDetailsBuilder.ErrorCodeExtension]);
        Assert.Equal(CorrelationId, problem.Extensions[ProblemDetailsBuilder.CorrelationIdExtension]);
    }

    [Fact]
    public void FromBusinessError_DetailIsTheCallerSafeMessage()
    {
        var error = new BusinessError(ErrorCode.Validation, "Name is required.");

        ProblemDetails problem = ProblemDetailsBuilder.FromBusinessError(error, CorrelationId);

        Assert.Equal("Name is required.", problem.Detail);
    }

    [Fact]
    public void Unexpected_IsGeneric500WithStableCodeCorrelationIdAndNoLeakedDetail()
    {
        ProblemDetails problem = ProblemDetailsBuilder.Unexpected(CorrelationId);

        Assert.Equal(500, problem.Status);
        Assert.Equal(
            ErrorCodeHttpMapping.UnexpectedCode,
            problem.Extensions[ProblemDetailsBuilder.ErrorCodeExtension]);
        Assert.Equal(CorrelationId, problem.Extensions[ProblemDetailsBuilder.CorrelationIdExtension]);

        // The generic body must not carry internal detail (requirement A6 §8.5).
        Assert.DoesNotContain("Exception", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ", problem.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromBusinessError_BlankCorrelationId_Throws(string correlationId)
    {
        var error = new BusinessError(ErrorCode.Validation, "bad");

        Assert.Throws<ArgumentException>(
            () => ProblemDetailsBuilder.FromBusinessError(error, correlationId));
    }

    [Fact]
    public void Unexpected_BlankCorrelationId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProblemDetailsBuilder.Unexpected("  "));
    }
}
