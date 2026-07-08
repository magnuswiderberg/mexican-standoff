using MexicanStandoff.Engine;

namespace MexicanStandoff.Server.Games;

public enum GamePhase
{
    Lobby,
    Selecting,
    GameOver,
}

public sealed class SessionPlayer
{
    public required string Id { get; init; }
    public required string Token { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// Mutable per-game session. All mutation happens under <see cref="Lock"/>;
/// SignalR broadcasts happen after the lock is released.
/// </summary>
public sealed class GameSession
{
    public required string Code { get; init; }
    public readonly object Lock = new();

    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public List<SessionPlayer> Players { get; } = [];
    public GameState? State { get; set; }

    public Dictionary<string, PlayerAction> PendingActions { get; } = [];
    public Dictionary<string, IReadOnlyList<PlayerAction>> PendingSequences { get; } = [];

    /// <summary>Incremented per selection phase; stale timeout callbacks compare and bail.</summary>
    public int SelectionNonce { get; set; }

    public DateTimeOffset? Deadline { get; set; }
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string>? WinnerIds { get; set; }
    public WinReason? WinReason { get; set; }

    public SessionPlayer? PlayerByToken(string token) => Players.FirstOrDefault(p => p.Token == token);
}
