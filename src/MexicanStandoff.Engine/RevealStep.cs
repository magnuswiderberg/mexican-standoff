namespace MexicanStandoff.Engine;

public enum WinReason
{
    /// <summary>A player reached the gold target.</summary>
    GoldTarget,

    /// <summary>Only one player left alive.</summary>
    LastStanding,

    /// <summary>All remaining players were eliminated in the same round.</summary>
    MutualDestruction,
}

/// <summary>
/// One step of the reveal script. The engine emits an ordered list of these per
/// round; the server broadcasts it and every device (phone or monitor) animates
/// it in lockstep.
/// </summary>
public abstract record RevealStep
{
    private RevealStep() { }

    /// <summary>All cards flip at once. Ordered by seat.</summary>
    public sealed record ActionsRevealed(IReadOnlyList<(string PlayerId, PlayerAction Action)> Actions) : RevealStep;

    /// <summary>A shot is fired; <paramref name="Hit"/> is false when the target dodged.</summary>
    public sealed record ShotFired(string ShooterId, string TargetId, bool Hit) : RevealStep;

    /// <summary>A hit player's Load/Chest action is cancelled.</summary>
    public sealed record ActionCancelled(string PlayerId, PlayerAction Action) : RevealStep;

    public sealed record GunLoaded(string PlayerId, int BulletsNow) : RevealStep;

    /// <summary>Sudden death (Final Duel stalemate guard): a free bullet at sequence start.</summary>
    public sealed record SuddenDeathBullet(string PlayerId, int BulletsNow) : RevealStep;

    /// <summary>Chest outcome. Winner is null when contested or when the only contender was hit.</summary>
    public sealed record ChestResolved(int ChestIndex, IReadOnlyList<string> ContenderIds, string? WinnerId) : RevealStep;

    /// <summary>
    /// A player is wounded and out. Their gold is split evenly among the players
    /// whose shots hit them this round (rounded down); the remainder is lost.
    /// </summary>
    public sealed record PlayerEliminated(
        string PlayerId,
        IReadOnlyList<string> LooterIds,
        int GoldPerLooter,
        int GoldLost) : RevealStep;

    /// <summary>Final Duel only: a programmed action became illegal and fizzled into a Dodge.</summary>
    public sealed record ActionFizzled(string PlayerId, PlayerAction Original) : RevealStep;

    public sealed record GameEnded(IReadOnlyList<string> WinnerIds, WinReason Reason) : RevealStep;
}
