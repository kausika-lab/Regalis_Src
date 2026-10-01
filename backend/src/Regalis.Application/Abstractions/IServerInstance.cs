namespace Regalis.Application.Abstractions;

/// <summary>
/// The running API process. Sessions belong to the process that issued them: every access
/// token carries <see cref="BootId"/>, and a token from an earlier process is refused, so a
/// server restart sends everyone back to the sign-in page (setting Auth:EndSessionsOnRestart,
/// default true).
/// </summary>
public interface IServerInstance
{
    /// <summary>Random per process start.</summary>
    string BootId { get; }

    DateTime StartedUtc { get; }

    /// <summary>False when Auth:EndSessionsOnRestart is off: tokens outlive a restart.</summary>
    bool EndSessionsOnRestart { get; }
}
