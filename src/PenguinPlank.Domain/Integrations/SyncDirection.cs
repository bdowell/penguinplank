namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// The direction in which an <see cref="IntegrationConnection"/> is permitted to synchronize data
/// with its external platform.
/// </summary>
/// <remarks>
/// All connectors are disabled in Phase A, so this is schema that records a connection's intended
/// direction without any synchronization taking place.
/// </remarks>
public enum SyncDirection
{
    /// <summary>No synchronization in either direction. The Phase A default.</summary>
    None = 0,

    /// <summary>Data flows from the external platform into this system only.</summary>
    Inbound = 1,

    /// <summary>Data flows from this system out to the external platform only.</summary>
    Outbound = 2,

    /// <summary>Data flows in both directions.</summary>
    Bidirectional = 3,
}
