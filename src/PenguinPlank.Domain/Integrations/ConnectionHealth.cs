namespace PenguinPlank.Domain.Integrations;

/// <summary>
/// The observed health of an <see cref="IntegrationConnection"/>.
/// </summary>
/// <remarks>
/// Connectors are disabled in Phase A, so a connection records its health without any live
/// synchronization updating it. The default is <see cref="Unknown"/>.
/// </remarks>
public enum ConnectionHealth
{
    /// <summary>Health has not been determined. The default for a newly recorded connection.</summary>
    Unknown = 0,

    /// <summary>The connection is healthy.</summary>
    Healthy = 1,

    /// <summary>The connection is degraded but partially usable.</summary>
    Degraded = 2,

    /// <summary>The connection has failed and cannot be used.</summary>
    Failed = 3,
}
