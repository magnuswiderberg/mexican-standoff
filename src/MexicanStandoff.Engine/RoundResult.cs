namespace MexicanStandoff.Engine;

/// <summary>Result of resolving a round (or a full Final Duel sequence).</summary>
public sealed record RoundResult
{
    public required GameState NewState { get; init; }
    public required IReadOnlyList<RevealStep> Reveal { get; init; }
    public IReadOnlyList<string>? WinnerIds { get; init; }
    public WinReason? WinReason { get; init; }

    public bool IsGameOver => WinnerIds is not null;
}
