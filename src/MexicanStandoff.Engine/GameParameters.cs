namespace MexicanStandoff.Engine;

/// <summary>
/// Tunable game parameters. Defaults follow docs/game-design.md; the simulation
/// harness sweeps these to find values that give a fun, quick game.
/// </summary>
public sealed record GameParameters
{
    public int StartingHp { get; init; } = 2;

    /// <summary>
    /// HP ceiling. Only matters when <see cref="HealingEnabled"/> is on (nothing
    /// else raises HP); defaults to the starting HP, so healing without raising
    /// this is just a "patch back to full" toggle.
    /// </summary>
    public int MaxHp { get; init; } = 2;
    public int MaxBullets { get; init; } = 2;

    /// <summary>Whether the Heal action card is available (v2 experiment; off by default).</summary>
    public bool HealingEnabled { get; init; }

    /// <summary>Gold bars spent per Heal.</summary>
    public int HealCost { get; init; } = 2;

    /// <summary>HP restored per Heal (capped at <see cref="MaxHp"/>).</summary>
    public int HealAmount { get; init; } = 1;

    /// <summary>
    /// When a heal is cancelled by a hit, whether the gold is refunded (true, the
    /// Load-like treatment) or spent anyway (false, the default — healing under
    /// fire is a gamble, which is the tension the mechanic is there for).
    /// </summary>
    public bool HealCostRefundedOnCancel { get; init; }

    /// <summary>Bullets in the gun at game start (capped at <see cref="MaxBullets"/>).</summary>
    public int StartingBullets { get; init; }
    public int GoldToWin { get; init; } = 6;

    /// <summary>
    /// Gold bars gained per successful chest grab. Scaled 2-per-grab / 6-to-win
    /// (same three grabs as 1/3) so the common 2-shooter loot split is exact —
    /// see docs/simulation-results.md.
    /// </summary>
    public int GoldPerChest { get; init; } = 2;
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
