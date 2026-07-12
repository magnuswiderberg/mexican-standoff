namespace MexicanStandoff.Engine.Tests;

public class WinEvaluatorTests
{
    [Fact]
    public void ReachingGoldTarget_Wins()
    {
        var state = TestGame.State(("a", 2, 0, 4), ("b", 2, 0, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Chest()), ("b", TestGame.Dodge), ("c", TestGame.Dodge));

        Assert.Equal(["a"], result.WinnerIds);
        Assert.Equal(WinReason.GoldTarget, result.WinReason);
        Assert.Contains(result.Reveal, s => s is RevealStep.GameEnded);
    }

    [Fact]
    public void LastPlayerStanding_Wins()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 1, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Load));

        Assert.Equal(["a"], result.WinnerIds);
        Assert.Equal(WinReason.LastStanding, result.WinReason);
    }

    [Fact]
    public void GameContinues_WhenNoWinCondition()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Load), ("b", TestGame.Load), ("c", TestGame.Dodge));

        Assert.False(result.IsGameOver);
        Assert.Null(result.WinnerIds);
        Assert.DoesNotContain(result.Reveal, s => s is RevealStep.GameEnded);
    }

    [Fact]
    public void SeveralOverTarget_MostGoldWins()
    {
        var state = TestGame.State(("a", 2, 0, 6), ("b", 2, 0, 7), ("c", 2, 0, 0));
        var (winners, reason) = WinEvaluator.Evaluate(state);

        Assert.Equal(["b"], winners);
        Assert.Equal(WinReason.GoldTarget, reason);
    }

    [Fact]
    public void GoldTie_BreaksOnHp()
    {
        var state = TestGame.State(("a", 1, 0, 6), ("b", 2, 0, 6), ("c", 2, 0, 0));
        var (winners, _) = WinEvaluator.Evaluate(state);

        Assert.Equal(["b"], winners);
    }

    [Fact]
    public void GoldAndHpTie_BreaksOnBullets()
    {
        var state = TestGame.State(("a", 2, 2, 6), ("b", 2, 1, 6), ("c", 2, 0, 0));
        var (winners, _) = WinEvaluator.Evaluate(state);

        Assert.Equal(["a"], winners);
    }

    [Fact]
    public void FullTie_IsASharedVictory()
    {
        var state = TestGame.State(("a", 2, 1, 6), ("b", 2, 1, 6), ("c", 2, 0, 0));
        var (winners, _) = WinEvaluator.Evaluate(state);

        Assert.Equal(["a", "b"], winners!.OrderBy(id => id).ToList());
    }

    [Fact]
    public void MutualDestruction_EndsGameWithNoWinner()
    {
        // Both at 1 HP shoot each other: nobody left, nobody wins.
        var state = TestGame.State(("a", 1, 2, 0), ("b", 1, 1, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Attack("a")));

        Assert.Equal(0, result.NewState.AliveCount);
        Assert.True(result.IsGameOver);
        Assert.Empty(result.WinnerIds!);
        Assert.Equal(WinReason.MutualDestruction, result.WinReason);
        var ended = Assert.Single(result.Reveal.OfType<RevealStep.GameEnded>());
        Assert.Empty(ended.WinnerIds);
    }

    [Fact]
    public void DyingWithWinningGold_DoesNotWin()
    {
        // A loots B up to the gold target but dies in the same volley: dead
        // players never win, so mutual destruction still means no winner.
        var parameters = GameParameters.Default with { GoldToWin = 2 };
        var state = TestGame.State(parameters, ("a", 1, 1, 0), ("b", 1, 1, 2));
        var result = TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Attack("a")));

        Assert.Equal(0, result.NewState.AliveCount);
        Assert.Empty(result.WinnerIds!);
        Assert.Equal(WinReason.MutualDestruction, result.WinReason);
    }
}
