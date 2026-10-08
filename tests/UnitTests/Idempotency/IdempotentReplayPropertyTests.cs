using CsCheck;
using PenguinPlank.Application.Idempotency;

namespace UnitTests.Idempotency;

/// <summary>
/// Property 13 (requirements A4 Ã‚Â§6.2, Ã‚Â§6.3, Ã‚Â§6.4): <em>Idempotent command replay.</em> Reusing an
/// idempotency key with the <em>same</em> payload hash replays the stored result and never repeats
/// the command's effect; reusing it with a <em>different</em> payload hash is a conflict that also
/// never executes the command again.
/// </summary>
/// <remarks>
/// <para>
/// The strongest assertions are made directly against the pure building blocks with no fake and no
/// application startup (coding-standards Ã‚Â§1, Ã‚Â§7):
/// <see cref="IdempotencyDecision.ForExisting"/> (equal hashes Ã¢â€¡â€™ replay carrying the stored result;
/// unequal Ã¢â€¡â€™ conflict) and <see cref="PayloadHasher"/> determinism (same input Ã¢â€¡â€™ same hash,
/// distinct input Ã¢â€¡â€™ distinct hash).
/// </para>
/// <para>
/// The pipeline-level replay-once behaviour is exercised through
/// <see cref="IdempotencyPipeline.ExecuteAsync"/> over a hand-written in-memory
/// <see cref="IIdempotencyStore"/> fake (<see cref="InMemoryIdempotencyStore"/>). The fake performs
/// no I/O and resolves repeated keys via the same pure <see cref="IdempotencyDecision"/> the real
/// EF store relies on, so a random sequence of repeated same-key calls proves the command body runs
/// exactly once and every call returns the identical stored result Ã¢â‚¬â€ relational uniqueness itself
/// is covered by the integration tests, not here (coding-standards Ã‚Â§7).
/// </para>
/// </remarks>
public class IdempotentReplayPropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates Ã¢â€°Â¥100 iterations for every correctness property;
    /// this matches the suite convention of sampling well above that floor.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// Generates a non-empty, non-whitespace, bounded idempotency-key string (always Ã¢â€°Â¤ the value
    /// object's limit). A leading non-whitespace character guarantees it is never blank.
    /// </summary>
    private static readonly Gen<string> s_genKeyText =
        Gen.String[Gen.Char.AlphaNumeric, 0, 40].Select(suffix => "k" + suffix);

    /// <summary>Generates arbitrary payload text to feed the deterministic hasher.</summary>
    private static readonly Gen<string> s_genPayload =
        Gen.String[Gen.Char.AlphaNumeric, 0, 64];

    /// <summary>Generates a repeat count in [2, 6] for how many times the same key is replayed.</summary>
    private static readonly Gen<int> s_genRepeatCount = Gen.Int[2, 6];

    [Fact]
    public void ForExisting_EqualHashes_ReplaysStoredResult()
    {
        s_genPayload.Sample(
            payload =>
            {
                PayloadHash existing = PayloadHasher.Compute(payload);

                // The incoming hash is computed independently from the same payload: equal value,
                // and (per PayloadHash) equal even if a source produced differently-cased hex.
                PayloadHash incoming = PayloadHasher.Compute(payload);
                var stored = new ResultRef("result-" + payload);

                IdempotencyOutcome outcome =
                    IdempotencyDecision.ForExisting(existing, incoming, stored);

                Assert.True(outcome.IsReplay);
                Assert.Equal(stored, outcome.StoredResult);
            },
            iter: Iterations);
    }

    [Fact]
    public void ForExisting_DifferentHashes_IsConflict()
    {
        Gen.Select(s_genPayload, s_genPayload)
            .Where((first, second) => first != second)
            .Sample(
                tuple =>
                {
                    PayloadHash existing = PayloadHasher.Compute(tuple.Item1);
                    PayloadHash incoming = PayloadHasher.Compute(tuple.Item2);
                    var stored = new ResultRef("result-reference");

                    IdempotencyOutcome outcome =
                        IdempotencyDecision.ForExisting(existing, incoming, stored);

                    Assert.True(outcome.IsConflict);
                    Assert.Null(outcome.StoredResult);
                },
                iter: Iterations);
    }

    [Fact]
    public void Compute_SameInput_IsDeterministic()
    {
        s_genPayload.Sample(
            payload =>
            {
                PayloadHash first = PayloadHasher.Compute(payload);
                PayloadHash second = PayloadHasher.Compute(payload);

                Assert.Equal(first, second);
            },
            iter: Iterations);
    }

    [Fact]
    public void Compute_DifferentInput_ProducesDifferentHash()
    {
        Gen.Select(s_genPayload, s_genPayload)
            .Where((first, second) => first != second)
            .Sample(
                tuple =>
                {
                    PayloadHash first = PayloadHasher.Compute(tuple.Item1);
                    PayloadHash second = PayloadHasher.Compute(tuple.Item2);

                    // Distinct inputs almost surely hash distinctly under SHA-256; a collision over
                    // these short generated strings would itself be a defect worth surfacing.
                    Assert.NotEqual(first, second);
                },
                iter: Iterations);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatedSameKeyAndHash_RunsCommandOnceAndReplaysSameResultAsync()
    {
        await Gen.Select(s_genKeyText, s_genPayload, s_genRepeatCount)
            .SampleAsync(
                async tuple =>
                {
                    (string keyText, string payload, int repeats) = tuple;

                    var store = new InMemoryIdempotencyStore();
                    var pipeline = new IdempotencyPipeline(store);

                    var key = new IdempotencyKey(keyText);
                    var caller = new Caller(Guid.NewGuid());
                    var operation = new Operation("CreateVariant");
                    PayloadHash hash = PayloadHasher.Compute(payload);

                    var resultRef = new ResultRef("variant-" + Guid.NewGuid().ToString("N"));
                    int executions = 0;

                    Task<CommandExecution<int>> Command(CancellationToken _)
                    {
                        int sideEffect = Interlocked.Increment(ref executions);
                        return Task.FromResult(new CommandExecution<int>(sideEffect, resultRef));
                    }

                    // First call: a brand-new key must execute exactly once.
                    IdempotencyPipelineResult<int> first = await pipeline.ExecuteAsync(
                        key, caller, operation, hash, Command, CancellationToken.None);

                    Assert.True(first.WasExecuted);
                    Assert.Equal(resultRef, first.StoredResult);

                    // Every subsequent same-key + same-hash call must replay the stored result
                    // without invoking the command body again.
                    for (int i = 0; i < repeats; i++)
                    {
                        IdempotencyPipelineResult<int> replay = await pipeline.ExecuteAsync(
                            key, caller, operation, hash, Command, CancellationToken.None);

                        Assert.True(replay.WasReplayed);
                        Assert.Equal(resultRef, replay.StoredResult);
                    }

                    // The business side effect happened exactly once across all calls.
                    Assert.Equal(1, executions);
                },
                iter: Iterations);
    }

    [Fact]
    public async Task ExecuteAsync_SameKeyDifferentHash_ConflictsWithoutSecondExecutionAsync()
    {
        await Gen.Select(s_genKeyText, s_genPayload, s_genPayload)
            .Where((_, firstPayload, secondPayload) => firstPayload != secondPayload)
            .SampleAsync(
                async tuple =>
                {
                    (string keyText, string firstPayload, string secondPayload) = tuple;

                    var store = new InMemoryIdempotencyStore();
                    var pipeline = new IdempotencyPipeline(store);

                    var key = new IdempotencyKey(keyText);
                    var caller = new Caller(Guid.NewGuid());
                    var operation = new Operation("CreateVariant");
                    PayloadHash firstHash = PayloadHasher.Compute(firstPayload);
                    PayloadHash secondHash = PayloadHasher.Compute(secondPayload);

                    var resultRef = new ResultRef("variant-" + Guid.NewGuid().ToString("N"));
                    int executions = 0;

                    Task<CommandExecution<int>> Command(CancellationToken _)
                    {
                        int sideEffect = Interlocked.Increment(ref executions);
                        return Task.FromResult(new CommandExecution<int>(sideEffect, resultRef));
                    }

                    IdempotencyPipelineResult<int> first = await pipeline.ExecuteAsync(
                        key, caller, operation, firstHash, Command, CancellationToken.None);
                    Assert.True(first.WasExecuted);

                    // Same key, different payload hash: a conflict that does not run the body again.
                    IdempotencyPipelineResult<int> conflict = await pipeline.ExecuteAsync(
                        key, caller, operation, secondHash, Command, CancellationToken.None);

                    Assert.True(conflict.WasConflict);
                    Assert.NotNull(conflict.Conflict);
                    Assert.Equal(1, executions);
                },
                iter: Iterations);
    }

    /// <summary>
    /// A hand-written, in-memory <see cref="IIdempotencyStore"/> fake for orchestration tests. It
    /// holds reserved keys in a dictionary and resolves repeated keys with the same pure
    /// <see cref="IdempotencyDecision"/> the real EF store uses, so the pipeline's replay-versus-
    /// conflict behaviour is exercised with no database (coding-standards Ã‚Â§7). It performs no I/O.
    /// </summary>
    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<string, Record> _records = new(StringComparer.Ordinal);

        public Task<IdempotencyOutcome> BeginAsync(
            IdempotencyKey key,
            Caller caller,
            Operation operation,
            PayloadHash payloadHash,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_records.TryGetValue(key.Value, out Record existing))
            {
                IdempotencyOutcome outcome = IdempotencyDecision.ForExisting(
                    existing.PayloadHash, payloadHash, existing.Result);
                return Task.FromResult(outcome);
            }

            _records[key.Value] = new Record(payloadHash, Result: null);
            return Task.FromResult(IdempotencyOutcome.New);
        }

        public Task CompleteAsync(
            IdempotencyKey key,
            ResultRef result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Record reserved = _records[key.Value];
            _records[key.Value] = new Record(reserved.PayloadHash, result);
            return Task.CompletedTask;
        }

        private readonly record struct Record(PayloadHash PayloadHash, ResultRef? Result);
    }
}
