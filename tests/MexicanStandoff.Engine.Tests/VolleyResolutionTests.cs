namespace MexicanStandoff.Engine.Tests;

/// <summary>The simultaneous-volley model: dodge, attack, cancellation, load.</summary>
public class VolleyResolutionTests
{
    [Fact]
    public void Load_AddsOneBullet()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Load), ("b", TestGame.Dodge));

        Assert.Equal(1, result.NewState.Player("a").Bullets);
        Assert.Contains(result.Reveal, s => s is RevealStep.GunLoaded("a", 1));
    }

    [Fact]
    public void Attack_ConsumesBullet_EvenWhenTargetDodges()
    {
        var state = TestGame.State(("a", 2, 2, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Dodge));

        Assert.Equal(1, result.NewState.Player("a").Bullets);
        Assert.Equal(2, result.NewState.Player("b").Hp);
        Assert.Contains(result.Reveal, s => s is RevealStep.ShotFired("a", "b", false));
    }

    [Fact]
    public void Hit_DealsOneDamage()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Load));

        Assert.Equal(1, result.NewState.Player("b").Hp);
        Assert.Contains(result.Reveal, s => s is RevealStep.ShotFired("a", "b", true));
    }

    [Fact]
    public void TwoShooters_SameTarget_DealTwoDamage()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 1, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("c")), ("b", TestGame.Attack("c")), ("c", TestGame.Load));

        Assert.Equal(0, result.NewState.Player("c").Hp);
        Assert.False(result.NewState.Player("c").IsAlive);
    }

    [Fact]
    public void BeingHit_NeverCancelsAnAttack()
    {
        // A shoots B while B shoots C: both shots land (simultaneous volley).
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 1, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("b")), ("b", TestGame.Attack("c")), ("c", TestGame.Load));

        Assert.Equal(1, result.NewState.Player("b").Hp);
        Assert.Equal(1, result.NewState.Player("c").Hp);
    }

    [Fact]
    public void MutualShootout_BothHit()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 1, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("b")), ("b", TestGame.Attack("a")), ("c", TestGame.Dodge));

        Assert.Equal(1, result.NewState.Player("a").Hp);
        Assert.Equal(1, result.NewState.Player("b").Hp);
    }

    [Fact]
    public void Hit_CancelsLoad()
    {
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Load));

        Assert.Equal(0, result.NewState.Player("b").Bullets);
        Assert.Contains(result.Reveal, s => s is RevealStep.ActionCancelled("b", _));
    }

    [Fact]
    public void DodgedShot_DoesNotCancel()
    {
        // A shoots at C who is loading, but C was not hit... C IS hit here; use dodge:
        // A shoots B; B dodges; B's dodge means nothing to cancel and no damage.
        var state = TestGame.State(("a", 2, 1, 0), ("b", 2, 0, 0), ("c", 2, 0, 0));
        var result = TestGame.Resolve(
            state, ("a", TestGame.Attack("b")), ("b", TestGame.Dodge), ("c", TestGame.Load));

        Assert.Equal(2, result.NewState.Player("b").Hp);
        Assert.Equal(1, result.NewState.Player("c").Bullets);
        Assert.DoesNotContain(result.Reveal, s => s is RevealStep.ActionCancelled);
    }

    [Fact]
    public void RoundNumber_Increments()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Dodge), ("b", TestGame.Dodge));

        Assert.Equal(1, result.NewState.RoundNumber);
    }

    [Fact]
    public void Reveal_StartsWithAllActionsRevealed()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", TestGame.Load), ("b", TestGame.Dodge));

        var first = Assert.IsType<RevealStep.ActionsRevealed>(result.Reveal[0]);
        Assert.Equal(2, first.Actions.Count);
    }

    [Fact]
    public void MissingAction_Throws()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var ex = Assert.Throws<InvalidActionException>(
            () => TestGame.Resolve(state, ("a", TestGame.Dodge)));
        Assert.Equal("b", ex.PlayerId);
    }

    [Fact]
    public void ActionFromEliminatedPlayer_Throws()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0), ("dead", 0, 0, 0));
        Assert.Throws<InvalidActionException>(() => TestGame.Resolve(
            state, ("a", TestGame.Dodge), ("b", TestGame.Dodge), ("dead", TestGame.Dodge)));
    }

    [Fact]
    public void IllegalAction_Throws()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        Assert.Throws<InvalidActionException>(
            () => TestGame.Resolve(state, ("a", TestGame.Attack("b")), ("b", TestGame.Dodge)));
    }
}
