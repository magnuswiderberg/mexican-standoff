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
    int MaxHp,
    bool HealingEnabled,
    int HealCost,
    int DuelSequenceLength,
    IReadOnlyList<PlayerSnapshot> Players);

/// <summary>
/// Default rule numbers for the standalone "How to play" page, served from
/// <see cref="MexicanStandoff.Engine.GameParameters.Default"/> so the page
/// never drifts from the engine.
/// </summary>
public sealed record RulesView(int StartingHp, int MaxBullets, int GoldToWin, int GoldPerChest, int DuelSequenceLength);

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

/// <summary>
/// A screen is asking to become the board. Broadcast to the game group, because
/// it carries no authority — only the host's control token can answer it. The pair
/// code is on the asking screen, so the host approves the TV they can actually see.
/// <para>
/// The remaining life travels as seconds, not as an instant: a phone with a skewed
/// clock would otherwise retire the prompt early — or sit on a dead one. Both sides
/// run it down (the screen stops waiting, the host's prompt goes away), so neither
/// is left staring at a request the server has already forgotten.
/// </para>
/// </summary>
public sealed record MonitorRequestView(string PairCode, int ExpiresInSeconds);

/// <summary>
/// The answer to a waiting screen, sent to it alone — it carries the monitor token
/// on approval, which must never reach the game group. A refusal carries why: the
/// host said no, or the game started before they got to it.
/// </summary>
public sealed record MonitorDecisionView(bool Granted, string? MonitorToken, string? Message = null);

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
    bool HasMonitor = false,
    /// <summary>A screen waiting on the host right now — so a host who reloads
    /// mid-request still sees the prompt, with its real remaining life.</summary>
    MonitorRequestView? PendingMonitor = null);
