namespace MexicanStandoff.Engine.Tests;

/// <summary>The Heal action (v2): spend gold to restore HP, cancelled by a hit like Load.</summary>
public class HealTests
{
    private static readonly GameParameters Healing =
        GameParameters.Default with { HealingEnabled = true, MaxHp = 3, HealCost = 2, HealAmount = 1 };

    private static PlayerAction Heal => PlayerAction.Heal.Instance;

    [Fact]
    public void Heal_RestoresHp_AndSpendsGold()
    {
        var state = TestGame.State(Healing, ("a", 1, 0, 2), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", Heal), ("b", TestGame.Dodge));

        Assert.Equal(2, result.NewState.Player("a").Hp);
        Assert.Equal(0, result.NewState.Player("a").Gold);
        Assert.Contains(result.Reveal, s => s is RevealStep.PlayerHealed("a", 2, 2));
    }

    [Fact]
    public void Heal_CanBankAboveStartingHp_UpToMaxHp()
    {
        // Start at the default 2 HP, heal up to the raised ceiling of 3.
        var state = TestGame.State(Healing, ("a", 2, 0, 2), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", Heal), ("b", TestGame.Dodge));

        Assert.Equal(3, result.NewState.Player("a").Hp);
    }

    [Fact]
    public void Heal_NeverExceedsMaxHp()
    {
        var bigHeal = Healing with { HealAmount = 5 };
        var state = TestGame.State(bigHeal, ("a", 2, 0, 2), ("b", 2, 0, 0));
        var result = TestGame.Resolve(state, ("a", Heal), ("b", TestGame.Dodge));

        Assert.Equal(3, result.NewState.Player("a").Hp);
    }

    [Fact]
    public void Heal_CancelledByHit_SpendsGold_ByDefault()
    {
        // a heals while b shoots it: the heal is cancelled and, by default, the
        // gold is spent anyway — healing under fire is a gamble.
        var state = TestGame.State(Healing, ("a", 2, 0, 2), ("b", 2, 1, 0));
        var result = TestGame.Resolve(state, ("a", Heal), ("b", TestGame.Attack("a")));

        var a = result.NewState.Player("a");
        Assert.Equal(1, a.Hp);   // took the hit, no heal
        Assert.Equal(0, a.Gold); // gold gone
        Assert.Contains(result.Reveal, s => s is RevealStep.ActionCancelled("a", PlayerAction.Heal) { GoldLost: 2 });
        Assert.DoesNotContain(result.Reveal, s => s is RevealStep.PlayerHealed);
    }

    [Fact]
    public void Heal_CancelledByHit_RefundsGold_WhenRefundEnabled()
    {
        // Opt-in Load-like treatment: a cancelled heal gives the gold back.
        var refund = Healing with { HealCostRefundedOnCancel = true };
        var state = TestGame.State(refund, ("a", 2, 0, 2), ("b", 2, 1, 0));
        var result = TestGame.Resolve(state, ("a", Heal), ("b", TestGame.Attack("a")));

        var a = result.NewState.Player("a");
        Assert.Equal(1, a.Hp);   // took the hit, no heal
        Assert.Equal(2, a.Gold); // gold refunded
        Assert.Contains(result.Reveal, s => s is RevealStep.ActionCancelled("a", PlayerAction.Heal) { GoldLost: 0 });
        Assert.DoesNotContain(result.Reveal, s => s is RevealStep.PlayerHealed);
    }

    [Fact]
    public void Heal_IsIllegal_WhenHealingDisabled()
    {
        var state = TestGame.State(("a", 1, 0, 5), ("b", 2, 0, 0)); // default params: healing off
        Assert.NotNull(ActionValidator.Validate(state, "a", Heal));
    }

    [Fact]
    public void Heal_IsIllegal_WithoutEnoughGold()
    {
        var state = TestGame.State(Healing, ("a", 1, 0, 1), ("b", 2, 0, 0));
        Assert.NotNull(ActionValidator.Validate(state, "a", Heal));
    }

    [Fact]
    public void Heal_IsIllegal_AtFullHp()
    {
        var state = TestGame.State(Healing, ("a", 3, 0, 5), ("b", 2, 0, 0));
        Assert.NotNull(ActionValidator.Validate(state, "a", Heal));
    }

    [Fact]
    public void Heal_IsLegal_WhenWoundedWithGold()
    {
        var state = TestGame.State(Healing, ("a", 1, 0, 2), ("b", 2, 0, 0));
        Assert.Null(ActionValidator.Validate(state, "a", Heal));
    }
}
