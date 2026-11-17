namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>The counts from one processing cycle.</summary>
/// <param name="Leased">Items leased.</param>
/// <param name="Succeeded">Items completed.</param>
/// <param name="Rescheduled">Items failed and scheduled for a later attempt, or left failed when attempts are exhausted.</param>
/// <param name="Released">Items returned unchanged because no connector is enabled.</param>
public sealed record WorkBatchResult(int Leased, int Succeeded, int Rescheduled, int Released);
