using CsCheck;
using Microsoft.AspNetCore.Mvc;
using PenguinPlank.Api.Errors;
using PenguinPlank.Domain.Common;

namespace UnitTests.Errors;

/// <summary>
/// Property 24 (requirement A1 Â§4.8): <em>Every error response is a ProblemDetails with a stable
/// code.</em> For any business failure and any non-empty correlation id, the pure
/// <see cref="ProblemDetailsBuilder"/> produces a ProblemDetails whose status matches the
/// <see cref="ErrorCodeHttpMapping"/>, that carries a non-empty stable <c>errorCode</c> extension
/// equal to the mapping's code and the supplied <c>correlationId</c>, and that never leaks internal
/// detail. The generic <c>Unexpected</c> path is held to the same contract at a <c>500</c> with the
/// stable <see cref="ErrorCodeHttpMapping.UnexpectedCode"/> code.
/// </summary>
/// <remarks>
/// This is the first property-based test in the suite. It uses CsCheck (pinned in
/// <c>UnitTests.csproj</c>): generators produce random (failure code, message, correlation id)
/// combinations, and CsCheck's <c>Sample</c> runs the assertions at a configured iteration count.
/// It exercises the same pure builder/mapper the ProblemDetails middleware uses,
/// with no <c>HttpContext</c> and no application startup (coding-standards Â§1, Â§7). The companion
/// example-based tests live in <see cref="ProblemDetailsBuilderTests"/> and
/// <see cref="ErrorCodeHttpMappingTests"/>; this test adds the universal property only.
/// </remarks>
public class ProblemDetailsPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Every failure <see cref="ErrorCode"/> â€” all members except the success sentinel
    /// <see cref="ErrorCode.None"/>. These are the only codes a <see cref="BusinessError"/> may
    /// carry, so the property is generated across exactly this space.
    /// </summary>
    private static readonly ErrorCode[] s_failureErrorCodes =
        Enum.GetValues<ErrorCode>().Where(code => code != ErrorCode.None).ToArray();

    /// <summary>
    /// Generates a failure error code (never <see cref="ErrorCode.None"/>) by picking uniformly
    /// over the failure-code array.
    /// </summary>
    private static readonly Gen<ErrorCode> s_genFailureErrorCode =
        Gen.Int[0, s_failureErrorCodes.Length - 1].Select(index => s_failureErrorCodes[index]);

    /// <summary>
    /// Generates a correlation id that is always non-empty and non-whitespace, matching the
    /// precondition the builder enforces. A leading non-whitespace character guarantees the string
    /// is never blank regardless of the random suffix.
    /// </summary>
    private static readonly Gen<string> s_genCorrelationId =
        Gen.String[Gen.Char.AlphaNumeric, 0, 40].Select(suffix => "c" + suffix);

    /// <summary>
    /// Generates a non-empty, non-whitespace business message, matching the precondition
    /// <see cref="BusinessError"/> enforces.
    /// </summary>
    private static readonly Gen<string> s_genMessage =
        Gen.String[Gen.Char.AlphaNumeric, 0, 60].Select(suffix => "m" + suffix);

    [Fact]
    public void FromBusinessError_AnyFailureCodeAndCorrelationId_IsProblemDetailsWithStableCode()
    {
        Gen.Select(s_genFailureErrorCode, s_genMessage, s_genCorrelationId)
            .Sample(
                (code, message, correlationId) =>
                {
                    var error = new BusinessError(code, message);
                    ProblemDetails problem =
                        ProblemDetailsBuilder.FromBusinessError(error, correlationId);
                    ErrorCodeHttpMapping mapping = ErrorCodeHttpMapping.Map(code);

                    // (a) Status is a client/server error that matches the single mapping table.
                    Assert.Equal(mapping.StatusCode, problem.Status);
                    Assert.InRange(problem.Status!.Value, 400, 599);

                    // (b) A non-empty, stable errorCode equal to the mapping's code.
                    object? errorCodeValue =
                        problem.Extensions[ProblemDetailsBuilder.ErrorCodeExtension];
                    string errorCode = Assert.IsType<string>(errorCodeValue);
                    Assert.False(string.IsNullOrWhiteSpace(errorCode));
                    Assert.Equal(mapping.Code, errorCode);

                    // (c) The correlation id round-trips unchanged.
                    object? correlationValue =
                        problem.Extensions[ProblemDetailsBuilder.CorrelationIdExtension];
                    Assert.Equal(correlationId, Assert.IsType<string>(correlationValue));

                    // (d) No internal detail leaks: the detail is the caller-safe business message
                    // only, carrying no stack-trace, SQL, or secret markers.
                    Assert.Equal(message, problem.Detail);
                    AssertNoInternalLeak(problem);
                },
                iter: Iterations);
    }

    [Fact]
    public void Unexpected_AnyCorrelationId_IsGeneric500WithStableCodeAndNoLeak()
    {
        s_genCorrelationId.Sample(
            correlationId =>
            {
                ProblemDetails problem = ProblemDetailsBuilder.Unexpected(correlationId);

                // A generic internal error.
                Assert.Equal(500, problem.Status);

                // The stable "Unexpected" code and the supplied correlation id.
                Assert.Equal(
                    ErrorCodeHttpMapping.UnexpectedCode,
                    problem.Extensions[ProblemDetailsBuilder.ErrorCodeExtension]);
                Assert.Equal(
                    correlationId,
                    problem.Extensions[ProblemDetailsBuilder.CorrelationIdExtension]);

                // The body carries no internal detail (requirement A6 Â§8.5).
                AssertNoInternalLeak(problem);
            },
            iter: Iterations);
    }

    /// <summary>
    /// Asserts a ProblemDetails body carries no markers of leaked internal state â€” no exception
    /// text, stack-trace frames, SQL fragments, or obvious secret labels â€” in its title or detail.
    /// </summary>
    private static void AssertNoInternalLeak(ProblemDetails problem)
    {
        string body = $"{problem.Title}\n{problem.Detail}";

        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT ", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionstring", body, StringComparison.OrdinalIgnoreCase);
    }
}
