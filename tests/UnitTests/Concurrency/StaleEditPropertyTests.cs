using CsCheck;
using PenguinPlank.Domain.Common;

namespace UnitTests.Concurrency;

/// <summary>
/// Property 14 (requirements A4 Â§6.5, Â§6.6, A5 Â§7.6): <em>A stale edit never overwrites.</em>
/// The <see cref="ETag"/> helpers own the pure encoding and comparison that back the
/// optimistic-concurrency guard: a mutable aggregate's <c>rowversion</c> is exposed as a strong
/// ETag on reads, echoed back as an <c>If-Match</c> token on edits, and the server performs a
/// conditional <c>UPDATE ... WHERE rowversion = @ifMatch</c>. These properties assert that the
/// pure helpers make that guard sound:
/// <list type="number">
///   <item>Encode/parse is lossless, so a client that echoes its ETag round-trips exactly.</item>
///   <item>Two <em>different</em> row versions never compare equal, so a stale <c>If-Match</c>
///   token can never satisfy the conditional update and the edit is refused rather than
///   silently overwriting newer state.</item>
///   <item><see cref="ETag.Matches"/> is reflexive and symmetric and treats equal content as
///   equal regardless of array identity.</item>
///   <item><see cref="ETag.TryParse"/> rejects a wildcard and malformed Base64 input.</item>
/// </list>
/// </summary>
/// <remarks>
/// This is a pure-helper property test: it exercises <see cref="ETag"/> with ordinary byte
/// values and needs no database, no <c>HttpContext</c>, and no application startup
/// (coding-standards Â§1, Â§7). The EF path that translates a <c>DbUpdateConcurrencyException</c>
/// into a stale-version failure is covered by the SQL Server integration test (task 6.13), not
/// here. Generators cover empty, 8-byte (SQL <c>rowversion</c>-sized), and arbitrary-length
/// arrays, plus pairs guaranteed to differ. CsCheck (pinned in <c>UnitTests.csproj</c>) runs each
/// property at <see cref="Iterations"/> samples.
/// </remarks>
public class StaleEditPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Generates an arbitrary row-version token: 0 to 32 bytes with arbitrary byte values. The
    /// range includes the empty array (an unversioned row) and the 8-byte width of a SQL
    /// <c>rowversion</c>.
    /// </summary>
    private static readonly Gen<byte[]> s_genRowVersion =
        Gen.Byte.Array[0, 32];

    /// <summary>
    /// Generates a token sized exactly like a SQL <c>rowversion</c> (8 bytes), the realistic
    /// concurrency-token width.
    /// </summary>
    private static readonly Gen<byte[]> s_genRowVersion8 =
        Gen.Byte.Array[8, 8];

    /// <summary>
    /// Generates a pair of row versions that are guaranteed to differ in content, modelling a
    /// stale <c>If-Match</c> token (left) against a newer current version (right).
    /// </summary>
    private static readonly Gen<(byte[] Stale, byte[] Current)> s_genDifferentPair =
        Gen.Select(s_genRowVersion, s_genRowVersion)
            .Where(pair => !pair.Item1.AsSpan().SequenceEqual(pair.Item2))
            .Select(pair => (pair.Item1, pair.Item2));

    [Fact]
    public void EncodeThenTryParse_AnyRowVersion_RoundTripsLossless()
    {
        s_genRowVersion.Sample(
            rowVersion =>
            {
                string etag = ETag.Encode(rowVersion);

                bool parsed = ETag.TryParse(etag, out byte[] decoded);

                Assert.True(parsed);
                Assert.True(ETag.Matches(decoded, rowVersion));
            },
            iter: Iterations);
    }

    [Fact]
    public void Matches_DifferentRowVersions_RefusesStaleToken()
    {
        s_genDifferentPair.Sample(
            pair =>
            {
                // A stale If-Match token never equals the current version, so the conditional
                // UPDATE WHERE rowversion = @ifMatch cannot match: the edit is refused and newer
                // state is never overwritten.
                Assert.False(ETag.Matches(pair.Stale, pair.Current));
                Assert.False(ETag.Matches(pair.Current, pair.Stale));

                // The same holds after a full ETag round-trip of each token.
                Assert.True(ETag.TryParse(ETag.Encode(pair.Stale), out byte[] staleToken));
                Assert.True(ETag.TryParse(ETag.Encode(pair.Current), out byte[] currentToken));
                Assert.False(ETag.Matches(staleToken, currentToken));
            },
            iter: Iterations);
    }

    [Fact]
    public void Matches_EqualContentTokens_IsReflexiveAndSymmetric()
    {
        s_genRowVersion.Sample(
            rowVersion =>
            {
                // Reflexive: a token always matches itself (a fresh edit against the current
                // version succeeds).
                Assert.True(ETag.Matches(rowVersion, rowVersion));

                // Equal content with distinct array identity still matches, and symmetrically.
                byte[] copy = (byte[])rowVersion.Clone();
                Assert.NotSame(rowVersion, copy);
                Assert.True(ETag.Matches(rowVersion, copy));
                Assert.True(ETag.Matches(copy, rowVersion));
            },
            iter: Iterations);
    }

    [Fact]
    public void TryParse_WildcardOrMalformedInput_ReturnsFalse()
    {
        // A wildcard is not a specific version token and is rejected.
        Assert.False(ETag.TryParse("*", out byte[] wildcard));
        Assert.Empty(wildcard);

        // Malformed, non-Base64 payloads are rejected across a random sample. A '!' character is
        // never valid Base64, so prepending it guarantees a malformed candidate while keeping the
        // input non-empty and non-whitespace (so emptiness is not the reason for rejection).
        Gen.String[Gen.Char.AlphaNumeric, 0, 40]
            .Select(suffix => "!" + suffix)
            .Sample(
                malformed =>
                {
                    Assert.False(ETag.TryParse(malformed, out byte[] decoded));
                    Assert.Empty(decoded);
                },
                iter: Iterations);
    }

    [Fact]
    public void EncodeThenTryParse_EightByteRowVersion_RoundTripsLossless()
    {
        s_genRowVersion8.Sample(
            rowVersion =>
            {
                Assert.True(ETag.TryParse(ETag.Encode(rowVersion), out byte[] decoded));
                Assert.Equal(8, decoded.Length);
                Assert.True(ETag.Matches(decoded, rowVersion));
            },
            iter: Iterations);
    }
}
