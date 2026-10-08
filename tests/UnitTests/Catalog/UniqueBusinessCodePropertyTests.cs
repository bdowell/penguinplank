using CsCheck;
using PenguinPlank.Domain.Catalog;
using PenguinPlank.Domain.Common;

namespace UnitTests.Catalog;

/// <summary>
/// Property 1 (requirements R01 / 1.1, 1.2, 1.7): <em>Unique business code enforcement for catalog
/// records.</em> The two pure policies under test â€” <see cref="SkuPolicy.ValidateUnique"/> and
/// <see cref="PieceCodePolicy.ValidateUnique"/> â€” decide whether a candidate business code is
/// acceptable given the codes already in use. They receive their inputs explicitly and return a
/// <see cref="Result"/>; they touch no database and start no application, so these properties
/// exercise them directly across a wide input space (coding-standards Â§1, Â§7).
/// <list type="number">
///   <item><b>SKU required.</b> A null, empty, or whitespace candidate SKU is always rejected with
///   <see cref="ErrorCode.Validation"/>, regardless of the existing SKUs (requirement 1.1).</item>
///   <item><b>SKU collision.</b> A candidate SKU that, after trimming and compared
///   case-insensitively, equals a member of the existing SKUs is rejected with
///   <see cref="ErrorCode.DuplicateSku"/> (requirements 1.2, 1.7).</item>
///   <item><b>SKU accepted.</b> A supplied, non-colliding SKU succeeds (requirement 1.1).</item>
///   <item><b>Piece code optional.</b> A null, empty, or whitespace candidate piece code always
///   succeeds â€” even when the existing collection contains nulls â€” because the code is optional and
///   uniqueness is filtered (requirement 1.7).</item>
///   <item><b>Piece-code collision.</b> A supplied candidate piece code that, after trimming and
///   compared case-insensitively, equals a supplied existing code is rejected with
///   <see cref="ErrorCode.DuplicatePieceCode"/> (requirement 1.7).</item>
///   <item><b>Piece code accepted.</b> A supplied, non-colliding piece code succeeds even when the
///   existing collection is padded with nulls/blanks (requirement 1.7).</item>
/// </list>
/// </summary>
/// <remarks>
/// Collision cases are constructed to be <em>guaranteed</em> collisions: a value is drawn from the
/// existing set and mutated only by changing case and/or adding surrounding whitespace, so the
/// normalization (trim + <see cref="System.StringComparison.OrdinalIgnoreCase"/>) is the only thing
/// that can detect the duplicate. Unique cases are constructed to be <em>guaranteed</em> unique by
/// prefixing a sentinel that no generated code contains. CsCheck (pinned in <c>UnitTests.csproj</c>)
/// runs each property at <see cref="Iterations"/> samples (â‰¥100 as the design mandates).
/// </remarks>
public class UniqueBusinessCodePropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness property;
    /// this matches the suite convention of sampling well above that floor.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// A sentinel prefix that no generated code can contain, so a candidate prefixed with it is
    /// guaranteed to be unique against any generated existing collection. Generated codes are
    /// alphanumeric only, so the hyphens in the sentinel cannot appear in a generated value.
    /// </summary>
    private const string UniqueSentinel = "ZZZ-UNIQUE-SENTINEL-";

    /// <summary>Generates a non-blank business code from CsCheck's alphanumeric alphabet.</summary>
    private static readonly Gen<string> s_genCode =
        Gen.String[Gen.Char.AlphaNumeric, 1, 12];

    /// <summary>Generates a blank candidate: null, empty, or whitespace-only.</summary>
    private static readonly Gen<string?> s_genBlank =
        Gen.OneOfConst<string?>(null, string.Empty, " ", "   ", "\t", " \t ");

    /// <summary>Generates a non-empty collection of distinct, non-blank existing codes.</summary>
    private static readonly Gen<string[]> s_genExistingCodes =
        s_genCode.Array[1, 8].Select(codes => codes.Distinct().ToArray());

    /// <summary>Whitespace fragments used to surround a code without changing its trimmed value.</summary>
    private static readonly string[] s_whitespaceFragments = { "", " ", "  ", "\t" };

    /// <summary>
    /// Randomly re-cases and surrounds a code with whitespace so the result still normalizes to the
    /// same code. The mutation never changes the trimmed, case-folded value, so a collision must be
    /// detected by the policy's normalization alone.
    /// </summary>
    private static Gen<string> MutateToCollidingVariant(string code) =>
        Gen.Select(
                Gen.Int[0, 3],
                Gen.Int[0, s_whitespaceFragments.Length - 1],
                Gen.Int[0, s_whitespaceFragments.Length - 1])
            .Select(tuple =>
            {
                string recased = tuple.Item1 switch
                {
                    0 => code.ToUpperInvariant(),
                    1 => code.ToLowerInvariant(),
                    2 => SwapCasePerCharacter(code),
                    _ => code,
                };

                return s_whitespaceFragments[tuple.Item2] + recased + s_whitespaceFragments[tuple.Item3];
            });

    /// <summary>Flips the case of each character so a mixed-case collision variant is produced.</summary>
    private static string SwapCasePerCharacter(string value)
    {
        char[] characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            char current = characters[index];
            characters[index] = char.IsUpper(current)
                ? char.ToLowerInvariant(current)
                : char.ToUpperInvariant(current);
        }

        return new string(characters);
    }

    /// <summary>
    /// Generates a single existing piece-code entry: usually a supplied code, sometimes a null or a
    /// whitespace-only blank, so the "blanks and multiple nulls never cause failure" rule is
    /// exercised.
    /// </summary>
    private static readonly Gen<string?> s_genExistingPieceCodeEntry =
        Gen.Frequency(
            (3, s_genCode.Select(code => (string?)code)),
            (2, Gen.OneOfConst<string?>(null, string.Empty, "   ")));

    /// <summary>
    /// Generates an existing piece-code collection that interleaves supplied codes with null and
    /// blank entries, so the "multiple nulls never cause failure" rule is exercised.
    /// </summary>
    private static readonly Gen<string?[]> s_genExistingPieceCodesWithNulls =
        s_genExistingPieceCodeEntry.Array[0, 10];

    [Fact]
    public void ValidateUnique_BlankSku_RejectsWithValidation()
    {
        Gen.Select(s_genBlank, s_genExistingCodes)
            .Sample(
                sample =>
                {
                    Result result = SkuPolicy.ValidateUnique(sample.Item1, sample.Item2);

                    Assert.True(result.IsFailure);
                    Assert.Equal(ErrorCode.Validation, result.Error.Code);
                },
                iter: Iterations);
    }

    [Fact]
    public void ValidateUnique_SkuCollidesCaseInsensitivelyAfterTrim_RejectsWithDuplicateSku()
    {
        s_genExistingCodes
            .SelectMany(existing =>
                Gen.Int[0, existing.Length - 1]
                    .SelectMany(pick => MutateToCollidingVariant(existing[pick])
                        .Select(candidate => (Existing: existing, Candidate: candidate))))
            .Sample(
                sample =>
                {
                    Result result = SkuPolicy.ValidateUnique(sample.Candidate, sample.Existing);

                    Assert.True(result.IsFailure);
                    Assert.Equal(ErrorCode.DuplicateSku, result.Error.Code);
                },
                iter: Iterations);
    }

    [Fact]
    public void ValidateUnique_SkuDoesNotCollide_Succeeds()
    {
        Gen.Select(s_genCode, s_genExistingCodes)
            .Sample(
                sample =>
                {
                    string candidate = UniqueSentinel + sample.Item1;

                    Result result = SkuPolicy.ValidateUnique(candidate, sample.Item2);

                    Assert.True(result.IsSuccess);
                },
                iter: Iterations);
    }

    [Fact]
    public void ValidateUnique_BlankPieceCode_SucceedsRegardlessOfExistingIncludingNulls()
    {
        Gen.Select(s_genBlank, s_genExistingPieceCodesWithNulls)
            .Sample(
                sample =>
                {
                    Result result = PieceCodePolicy.ValidateUnique(sample.Item1, sample.Item2);

                    Assert.True(result.IsSuccess);
                },
                iter: Iterations);
    }

    [Fact]
    public void ValidateUnique_PieceCodeCollidesCaseInsensitivelyAfterTrim_RejectsWithDuplicatePieceCode()
    {
        s_genExistingCodes
            .SelectMany(existing =>
                Gen.Int[0, existing.Length - 1]
                    .SelectMany(pick => MutateToCollidingVariant(existing[pick])
                        .Select(candidate => (Existing: existing, Candidate: candidate))))
            .Sample(
                sample =>
                {
                    // Pad the existing supplied codes with blanks/nulls to prove they are ignored
                    // and never suppress a genuine collision.
                    string?[] existingWithBlanks = sample.Existing
                        .Select(code => (string?)code)
                        .Concat(new string?[] { null, string.Empty, "   " })
                        .ToArray();

                    Result result = PieceCodePolicy.ValidateUnique(sample.Candidate, existingWithBlanks);

                    Assert.True(result.IsFailure);
                    Assert.Equal(ErrorCode.DuplicatePieceCode, result.Error.Code);
                },
                iter: Iterations);
    }

    [Fact]
    public void ValidateUnique_PieceCodeDoesNotCollide_SucceedsEvenWithNullExistingEntries()
    {
        Gen.Select(s_genCode, s_genExistingPieceCodesWithNulls)
            .Sample(
                sample =>
                {
                    string candidate = UniqueSentinel + sample.Item1;

                    Result result = PieceCodePolicy.ValidateUnique(candidate, sample.Item2);

                    Assert.True(result.IsSuccess);
                },
                iter: Iterations);
    }
}
