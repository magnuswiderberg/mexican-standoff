using MexicanStandoff.Server.Contracts;

namespace MexicanStandoff.Server.Hubs;

/// <summary>Server → client events, broadcast to everyone in the game's group.</summary>
public interface IGameClient
{
    Task LobbyUpdated(LobbyView lobby);
    Task RoundStarted(RoundStartedView round);
    Task PlayerLocked(PlayerLockedView locked);
    Task RoundResolved(RoundResolvedView resolved);
    /// <summary>A rematch was requested: everyone is back in the lobby, seats intact.</summary>
    Task ReturnedToLobby(LobbyView lobby);
    /// <summary>The game was stopped from the monitor; the session is dead.</summary>
    Task GameStopped();
    /// <summary>A monitor appeared (true) or the last one left (false) — while one
    /// is watching, rematches start from the monitor, not the phones.</summary>
    Task MonitorPresence(bool hasMonitor);

    /// <summary>
    /// A screen is asking to become the board, or null once the request is answered.
    /// Group-wide on purpose: it authorizes nothing, and only the host's device
    /// renders the prompt.
    /// </summary>
    Task MonitorRequested(MonitorRequestView? request);

    /// <summary>The host's answer — sent to the asking screen only, since it carries the token.</summary>
    Task MonitorDecision(MonitorDecisionView decision);
}
