namespace MexicanStandoff.Engine.Tests;

/// <summary>
/// Resigning (docs/game-design.md, Elimination → Resigning): the resigner
/// dodges through the volley, then walks away — eliminated with no looters,
/// gold abandoned.
/// </summary>
public class ResignTests
{
    private static RoundResult Resolve(
        GameState state,
        IReadOnlyCollection<string> resigned,
        params (string PlayerId, PlayerAction Action)[] actions) =>
        RoundResolver.Resolve(state, actions.ToDictionary(a => a.PlayerId, a => a.Action), resigned);

    [Fact]
    public void Resigner_IsEliminatedAtRoundEnd_GoldAbandoned()
    {
        var state = TestGame.State(("a", 2, 0, 2), ("b", 2, 0, 0), ("c", 2, 0, 0));

        var result = Resolve(state, ["a"],
            ("a", TestGame.Dodge), ("b", TestGame.Load), ("c", TestGame.Load));

        var a = result.NewState.Player("a");
        Assert.False(a.IsAlive);
        Assert.Equal(0, a.Gold);
        // Nobody looted the abandoned gold.
        Assert.All(result.NewState.AlivePlayers, p => Assert.Equal(0, p.Gold));
        var step = Assert.Single(result.Reveal.OfType<RevealStep.PlayerResigned>());
        Assert.Equal("a", step.PlayerId);
        Assert.Equal(2, step.GoldLost);
        Assert.Null(result.WinnerIds); // b and c play on
    }

    [Fact]
    public void Resigner_StillDodgesTheVolley()
    {
        var state = TestGame.State(("a", 1, 0, 0), ("b", 2, 1, 0), ("c", 2, 0, 0));

        var result = Resolve(state, ["a"],
            ("a", TestGame.Dodge), ("b", TestGame.Attack("a")), ("c", TestGame.Dodge));

        // The shot missed (dodge), so the exit is a resignation, not a kill —
        // b loots nothing.
        var shot = Assert.Single(result.Reveal.OfType<RevealStep.ShotFired>());
        Assert.False(shot.Hit);
        Assert.Empty(result.Reveal.OfType<RevealStep.PlayerEliminated>());
        Assert.Single(result.Reveal.OfType<RevealStep.PlayerResigned>());
        Assert.False(result.NewState.Player("a").IsAlive);
    }

    [Fact]
    public void Resign_LeavingOnePlayer_EndsTheGameLastStanding()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 1), ("c", 2, 0, 0));

        var result = Resolve(state, ["a", "b"],
            ("a", TestGame.Dodge), ("b", TestGame.Dodge), ("c", TestGame.Load));

        Assert.Equal(["c"], result.WinnerIds);
        Assert.Equal(WinReason.LastStanding, result.WinReason);
    }

    [Fact]
    public void Duel_ResignerWalksAfterTheFirstStep_OpponentWins()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 2));
        var dodges = Enumerable.Repeat(TestGame.Dodge, state.Parameters.DuelSequenceLength).ToArray();

        var result = DuelResolver.Resolve(state, new Dictionary<string, IReadOnlyList<PlayerAction>>
        {
            ["a"] = dodges,
            ["b"] = dodges,
        }, resignedIds: ["a"]);

        Assert.Equal(["b"], result.WinnerIds);
        Assert.Equal(WinReason.LastStanding, result.WinReason);
        // The duel ended on step 1: one volley reveal, then the resignation.
        Assert.Single(result.Reveal.OfType<RevealStep.PlayerResigned>());
        Assert.Single(result.Reveal.OfType<RevealStep.ActionsRevealed>());
    }

    [Fact]
    public void AllRemainingPlayersResign_NoWinner()
    {
        var state = TestGame.State(("a", 2, 0, 1), ("b", 2, 0, 0));

        var result = Resolve(state, ["a", "b"],
            ("a", TestGame.Dodge), ("b", TestGame.Dodge));

        // Degenerate case from the spec: everyone walked, so the game ends
        // with no winner — mutual destruction.
        Assert.True(result.IsGameOver);
        Assert.Empty(result.WinnerIds!);
        Assert.Equal(WinReason.MutualDestruction, result.WinReason);
    }

    [Fact]
    public void ShotResignerLootedNormally_NoResignStep()
    {
        // A resigner who is somehow hit anyway (not dodging) dies by the shot:
        // normal elimination with loot takes precedence over the resignation.
        var state = TestGame.State(("a", 1, 0, 2), ("b", 2, 1, 0), ("c", 2, 0, 0));

        var result = Resolve(state, ["a"],
            ("a", TestGame.Load), ("b", TestGame.Attack("a")), ("c", TestGame.Dodge));

        Assert.Empty(result.Reveal.OfType<RevealStep.PlayerResigned>());
        var elim = Assert.Single(result.Reveal.OfType<RevealStep.PlayerEliminated>());
        Assert.Equal(["b"], elim.LooterIds);
        Assert.Equal(2, result.NewState.Player("b").Gold);
    }
}
