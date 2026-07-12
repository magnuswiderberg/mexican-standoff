using MexicanStandoff.Bots;
using MexicanStandoff.Engine;

namespace MexicanStandoff.Server.Games;

public enum GamePhase
{
    Lobby,
    Selecting,
    GameOver,
    /// <summary>Terminal: the monitor stopped the game; no further play is possible.</summary>
    Stopped,
}

public sealed class SessionPlayer
{
    public required string Id { get; init; }
    public required string Token { get; init; }
    public required string Name { get; init; }
    /// <summary>Avatar key from <see cref="Games.Avatars.All"/>.</summary>
    public required string Avatar { get; init; }
    /// <summary>Dev bot seat: the strategy the server plays each selection phase; null for humans.</summary>
    public IBot? Brain { get; init; }
    public bool IsBot => Brain is not null;

    /// <summary>
    /// Resigned this game: dodges the round they resigned in, then the engine
    /// eliminates them at resolution. Reset on game start.
    /// </summary>
    public bool Resigned { get; set; }
}

/// <summary>
/// Mutable per-game session. All mutation happens under <see cref="Lock"/>;
/// SignalR broadcasts happen after the lock is released.
/// </summary>
public sealed class GameSession
{
    public required string Code { get; init; }

    /// <summary>
    /// Secret minted with the game and handed only to the screen that created it.
    /// It — or the host's seat token — is what authorizes running the game
    /// (start, stop, kick, rematch, add bot); the game code authorizes nothing.
    /// </summary>
    public required string MonitorToken { get; init; }

    public readonly object Lock = new();

    public GamePhase Phase { get; set; } = GamePhase.Lobby;
    public List<SessionPlayer> Players { get; } = [];

    /// <summary>
    /// Seconds players get to pick an action; 0 disables the timer. Set once at
    /// creation (host's settings panel, falling back to config) — immutable after.
    /// </summary>
    public int SelectionTimerSeconds { get; set; }

    /// <summary>Total seats ever issued — ids stay unique even after lobby leaves/kicks.</summary>
    public int SeatsIssued { get; set; }
    public GameState? State { get; set; }

    public Dictionary<string, PlayerAction> PendingActions { get; } = [];
    public Dictionary<string, IReadOnlyList<PlayerAction>> PendingSequences { get; } = [];

    /// <summary>Incremented per selection phase; stale timeout callbacks compare and bail.</summary>
    public int SelectionNonce { get; set; }

    /// <summary>
    /// Connection ids of monitor pages watching this game (each proved the
    /// <see cref="MonitorToken"/> to get in). While non-empty, starting a rematch
    /// is monitor-only. Only touched under <see cref="Lock"/>.
    /// </summary>
    public HashSet<string> MonitorConnections { get; } = [];

    /// <summary>RNG for bot decisions; only touched under <see cref="Lock"/>.</summary>
    public Random BotRng { get; } = new();

    public DateTimeOffset? Deadline { get; set; }
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string>? WinnerIds { get; set; }
    public WinReason? WinReason { get; set; }

    public SessionPlayer? PlayerByToken(string? token) =>
        token is null ? null : Players.FirstOrDefault(p => Tokens.Equal(p.Token, token));

    public bool IsMonitorToken(string? token) => Tokens.Equal(MonitorToken, token);

    /// <summary>The host is the first seat — they run the game from their phone when there is no monitor.</summary>
    public SessionPlayer? Host => Players.FirstOrDefault();
}
