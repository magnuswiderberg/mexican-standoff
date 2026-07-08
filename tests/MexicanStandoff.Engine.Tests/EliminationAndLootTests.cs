namespace MexicanStandoff.Engine.Tests;

public class EliminationAndLootTests
{
    [Fact]
    public void ReachingZeroHp_Eliminates()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 1, 0, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("b")), ("b", TestGame.Load), ("c", TestGame.Dodge));

        Assert.False(result.NewState.Player("b").IsAlive);
        Assert.Contains(result.Reveal, s => s is RevealStep.PlayerEliminated { PlayerId: "b" });
    }

    [Fact]
    public void SingleKiller_TakesAllTheGold()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 1, 0, 2), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("b")), ("b", TestGame.Load), ("c", TestGame.Dodge));

        Assert.Equal(2, result.NewState.Player("a").Gold);
        Assert.Equal(0, result.NewState.Player("b").Gold);
    }

    [Fact]
    public void TwoKillers_SplitRoundedDown_RemainderIsLost()
    {
        // Victim has 3 gold, two shooters hit → 1 each, 1 lost.
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 1, 0), ("c", 2, 0, 3), ("d", 2, 0, 0));
        var result = TestGame.Resolve(
            state,
            ("a", TestGame.Attack("c")),
            ("b", TestGame.Attack("c")),
            ("c", TestGame.Load),
            ("d", TestGame.Dodge));

        Assert.Equal(1, result.NewState.Player("a").Gold);
        Assert.Equal(1, result.NewState.Player("b").Gold);
        var elimination = Assert.IsType<RevealStep.PlayerEliminated>(
            result.Reveal.Single(s => s is RevealStep.PlayerEliminated));
        Assert.Equal(1, elimination.GoldPerLooter);
        Assert.Equal(1, elimination.GoldLost);
    }

    [Fact]
    public void LootedGold_CanWinTheGameInstantly()
    {
        var state = TestGame.State(("a", 2, 1, 2), ("b", 1, 0, 1), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("b")), ("b", TestGame.Load), ("c", TestGame.Dodge));

        Assert.True(result.IsGameOver);
        Assert.Equal(["a"], result.WinnerIds);
        Assert.Equal(WinReason.GoldTarget, result.WinReason);
    }

    [Fact]
    public void KillerWhoDodgedNothing_StillLootsWhenVictimHadNoGold()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 1, 0, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("b")), ("b", TestGame.Load), ("c", TestGame.Dodge));

        var elimination = Assert.IsType<RevealStep.PlayerEliminated>(
            result.Reveal.Single(s => s is RevealStep.PlayerEliminated));
        Assert.Equal(0, elimination.GoldPerLooter);
        Assert.Equal(0, elimination.GoldLost);
        Assert.Equal(0, result.NewState.Player("a").Gold);
    }
}
