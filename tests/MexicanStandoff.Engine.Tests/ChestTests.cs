namespace MexicanStandoff.Engine.Tests;

public class ChestTests
{
    [Fact]
    public void AloneOnChest_GetsGoldBar()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Chest()), ("b", TestGame.Dodge));

        Assert.Equal(1, result.NewState.Player("a").Gold);
        Assert.Contains(result.Reveal, s => s is RevealStep.ChestResolved(0, _, "a"));
    }

    [Fact]
    public void ContestedChest_NobodyGetsGold()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Chest()), ("b", TestGame.Chest()), ("c", TestGame.Dodge));

        Assert.Equal(0, result.NewState.Player("a").Gold);
        Assert.Equal(0, result.NewState.Player("b").Gold);
        Assert.Contains(result.Reveal, s => s is RevealStep.ChestResolved(0, _, null));
    }

    [Fact]
    public void ContestedChest_ButOtherContenderShot_SurvivorGetsGold()
    {
        // A and B both go for the chest; C shoots A → A is cancelled, B is alone.
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0), ("c", 2, 1, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Chest()), ("b", TestGame.Chest()), ("c", TestGame.Attack("a")));

        Assert.Equal(0, result.NewState.Player("a").Gold);
        Assert.Equal(1, result.NewState.Player("b").Gold);
    }

    [Fact]
    public void ShotChestGoer_TakesDamageAndGetsNoGold()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Chest()));

        var b = result.NewState.Player("b");
        Assert.Equal(1, b.Hp);
        Assert.Equal(0, b.Gold);
    }

    [Fact]
    public void TwoChests_ResolveIndependently()
    {
        // 5 alive → 2 chests. A alone on chest 0; B and C contest chest 1.
        var state = TestGame.State(
            ("a", 2, 0, 0), ("b", 2, 0, 0), ("c", 2, 0, 0), ("d", 2, 0, 0), ("e", 2, 0, 0));
        var result = TestGame.Resolve(
            state,
            ("a", TestGame.Chest(0)),
            ("b", TestGame.Chest(1)),
            ("c", TestGame.Chest(1)),
            ("d", TestGame.Dodge),
            ("e", TestGame.Dodge));

        Assert.Equal(1, result.NewState.Player("a").Gold);
        Assert.Equal(0, result.NewState.Player("b").Gold);
        Assert.Equal(0, result.NewState.Player("c").Gold);
    }
}
