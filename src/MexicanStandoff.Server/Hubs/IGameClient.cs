using MexicanStandoff.Server.Contracts;

namespace MexicanStandoff.Server.Hubs;

/// <summary>Server → client events, broadcast to everyone in the game's group.</summary>
public interface IGameClient
{
    Task LobbyUpdated(LobbyView lobby);
    Task RoundStarted(RoundStartedView round);
    Task PlayerLocked(PlayerLockedView locked);
    Task RoundResolved(RoundResolvedView resolved);
}
