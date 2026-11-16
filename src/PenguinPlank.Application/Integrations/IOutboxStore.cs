namespace PenguinPlank.Application.Integrations;

/// <summary>
/// The transactional outbox boundary for outgoing integration jobs (requirement 3.14).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="EnqueueAsync"/> only <b>stages</b> the job in the current unit of work; it does not
/// commit. The job is persisted by the same commit as the business change that requires it, so
/// the job exists if and only if that change committed, and a rollback discards both.
/// </para>
/// <para>
/// The use case that owns the transaction is responsible for committing. The operation key is
/// unique, so a concurrent enqueue of the same key that passes the pre-check is rejected by the
/// database when the transaction commits.
/// </para>
/// </remarks>
public interface IOutboxStore
{
    /// <summary>Stages an outbox job in the current unit of work without committing it.</summary>
    /// <param name="job">The job to stage.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see cref="OutboxEnqueueOutcome.Staged"/> when the job was staged;
    /// <see cref="OutboxEnqueueOutcome.AlreadyEnqueued"/> when the operation key already exists.
    /// </returns>
    Task<OutboxEnqueueOutcome> EnqueueAsync(OutboxJob job, CancellationToken cancellationToken);
}
