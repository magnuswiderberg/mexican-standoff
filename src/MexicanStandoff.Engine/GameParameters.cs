namespace MexicanStandoff.Engine;

/// <summary>
/// Tunable game parameters. Defaults follow docs/game-design.md; the simulation
/// harness sweeps these to find values that give a fun, quick game.
/// </summary>
public sealed record GameParameters
{
    public int StartingHp { get; init; } = 2;
    public int MaxBullets { get; init; } = 2;
    public int GoldToWin { get; init; } = 3;
    public int MinPlayers { get; init; } = 2;
    public int MaxPlayers { get; init; } = 8;

    /// <summary>Alive-player count from which two chests are in play.</summary>
    public int TwoChestsFromPlayers { get; init; } = 5;

    /// <summary>Alive-player count from which three chests are in play (disabled by default).</summary>
    public int ThreeChestsFromPlayers { get; init; } = int.MaxValue;

    /// <summary>Number of actions each player programs per Final Duel sequence.</summary>
    public int DuelSequenceLength { get; init; } = 3;

    /// <summary>Completed duel sequences without progress before sudden death kicks in.</summary>
    public int DuelStalemateSequences { get; init; } = 3;

    public static GameParameters Default { get; } = new();

    public int ChestCountFor(int alivePlayers) =>
        alivePlayers >= ThreeChestsFromPlayers ? 3
        : alivePlayers >= TwoChestsFromPlayers ? 2
        : 1;
}
