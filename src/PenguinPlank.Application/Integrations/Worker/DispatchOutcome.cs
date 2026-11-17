namespace PenguinPlank.Application.Integrations.Worker;

/// <summary>The result of handing a leased item to a connector.</summary>
public enum DispatchOutcome
{
    /// <summary>The item was handled successfully.</summary>
    Succeeded = 0,

    /// <summary>The item failed transiently and may be retried after a backoff delay.</summary>
    TransientFailure = 1,

    /// <summary>No connector is enabled for the item, so nothing was sent. The item is released unchanged.</summary>
    ConnectorDisabled = 2,
}
