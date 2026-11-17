namespace PenguinPlank.Web.Shell;

/// <summary>The browser's knowledge of the cookie session.</summary>
public enum SessionStatus
{
    /// <summary>The session has not been checked yet.</summary>
    Unknown,

    /// <summary>The server confirmed an authenticated actor.</summary>
    Authenticated,

    /// <summary>The server reported no session.</summary>
    Anonymous,

    /// <summary>The session could not be determined because the server was unreachable or failed.</summary>
    Unavailable,
}
