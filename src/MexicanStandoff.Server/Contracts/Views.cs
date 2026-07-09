namespace MexicanStandoff.Server.Contracts;

public sealed record PlayerSnapshot(string Id, string Name, string Avatar, int Hp, int Bullets, int Gold, bool IsAlive);

public sealed record GameSnapshot(
    string Code,
    string Phase,
    int RoundNumber,
    bool IsDuel,
    bool SuddenDeath,
    int ChestCount,
    int GoldToWin,
    int MaxBullets,
    int StartingHp,
    int DuelSequenceLength,
    IReadOnlyList<PlayerSnapshot> Players);

public sealed record LobbyPlayer(string Id, string Name, string Avatar);

public sealed record LobbyView(string Code, IReadOnlyList<LobbyPlayer> Players, bool CanStart);

public sealed record JoinResult(string PlayerId, string PlayerToken, LobbyView Lobby);

public sealed record RoundStartedView(GameSnapshot Snapshot, DateTimeOffset? Deadline);

public sealed record PlayerLockedView(string PlayerId, int LockedCount, int TotalExpected);

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
    string? WinReason);
