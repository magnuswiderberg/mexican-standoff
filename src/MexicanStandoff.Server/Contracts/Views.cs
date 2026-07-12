namespace MexicanStandoff.Server.Contracts;

public sealed record PlayerSnapshot(
    string Id, string Name, string Avatar, int Hp, int Bullets, int Gold, bool IsAlive, bool IsResigned = false);

public sealed record GameSnapshot(
    string Code,
    string Phase,
    int RoundNumber,
    bool IsDuel,
    int DuelVolley,
    bool SuddenDeath,
    int ChestCount,
    int GoldToWin,
    int MaxBullets,
    int StartingHp,
    int DuelSequenceLength,
    IReadOnlyList<PlayerSnapshot> Players);

public sealed record LobbyPlayer(string Id, string Name, string Avatar, bool IsBot = false);

public sealed record LobbyView(
    string Code,
    IReadOnlyList<LobbyPlayer> Players,
    bool CanStart,
    bool BotsEnabled = false);

public sealed record JoinResult(string PlayerId, string PlayerToken, LobbyView Lobby);

/// <summary>
/// The new game, plus the secret that authorizes running it. The token goes back
/// to the creating screen only (it keeps it for its monitor page) — never to the
/// game group, or any phone on the wifi could kick the room and start the game.
/// </summary>
public sealed record CreateGameResult(string Code, string MonitorToken);

public sealed record RoundStartedView(GameSnapshot Snapshot, DateTimeOffset? Deadline);

public sealed record PlayerLockedView(
    string PlayerId,
    int LockedCount,
    int TotalExpected,
    IReadOnlyList<string> LockedPlayerIds,
    IReadOnlyList<string> ResignedPlayerIds);

public sealed record RoundResolvedView(
    IReadOnlyList<RevealStepDto> Reveal,
    GameSnapshot Snapshot,
    DateTimeOffset? NextDeadline,
    IReadOnlyList<string>? WinnerIds,
    string? WinReason);

/// <summary>Full current state, for the monitor page and reconnecting players.</summary>
public sealed record GameView(
    string Phase,
    LobbyView Lobby,
    GameSnapshot? Snapshot,
    DateTimeOffset? Deadline,
    string? PlayerId,
    bool HasSubmitted,
    IReadOnlyList<string>? WinnerIds,
    string? WinReason,
    bool HasMonitor = false);
