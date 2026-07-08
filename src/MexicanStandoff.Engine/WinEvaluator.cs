namespace MexicanStandoff.Engine;

public static class WinEvaluator
{
    /// <summary>
    /// Checks win conditions after a round has resolved. Returns the winner ids
    /// (several on a shared victory) or null when the game continues.
    /// Ties break by gold, then HP, then bullets; still tied → shared win.
    /// </summary>
    public static (IReadOnlyList<string>? Winners, WinReason? Reason) Evaluate(
        GameState state,
        IReadOnlyCollection<string> eliminatedThisRound)
    {
        var alive = state.AlivePlayers.ToList();

        var goldWinners = alive.Where(p => p.Gold >= state.Parameters.GoldToWin).ToList();
        if (goldWinners.Count > 0)
            return (TieBreak(goldWinners), WinReason.GoldTarget);

        if (alive.Count == 1)
            return ([alive[0].Id], WinReason.LastStanding);

        if (alive.Count == 0)
        {
            var lastRoundVictims = state.Players.Where(p => eliminatedThisRound.Contains(p.Id)).ToList();
            return (TieBreak(lastRoundVictims), WinReason.MutualDestruction);
        }

        return (null, null);
    }

    private static IReadOnlyList<string> TieBreak(IReadOnlyList<PlayerState> candidates)
    {
        var ranked = candidates
            .OrderByDescending(p => p.Gold)
            .ThenByDescending(p => p.Hp)
            .ThenByDescending(p => p.Bullets)
            .ToList();
        var best = ranked[0];
        return ranked
            .TakeWhile(p => p.Gold == best.Gold && p.Hp == best.Hp && p.Bullets == best.Bullets)
            .Select(p => p.Id)
            .ToList();
    }
}
