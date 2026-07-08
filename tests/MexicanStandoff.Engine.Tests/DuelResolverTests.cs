namespace MexicanStandoff.Engine.Tests;

public class DuelResolverTests
{
    private static IReadOnlyDictionary<string, IReadOnlyList<PlayerAction>> Seqs(
        params (string PlayerId, PlayerAction[] Sequence)[] sequences) =>
        sequences.ToDictionary(s => s.PlayerId, s => (IReadOnlyList<PlayerAction>)s.Sequence);

    [Fact]
    public void Sequences_ResolveStepByStep()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Load, TestGame.Load, TestGame.Attack("b")]),
            ("b", [TestGame.Chest(), TestGame.Chest(), TestGame.Chest()])));

        // Steps 1-2: A loads twice, B grabs two bars. Step 3: A shoots, B's chest is cancelled.
        Assert.False(result.IsGameOver);
        Assert.Equal(1, result.NewState.Player("a").Bullets);
        Assert.Equal(2, result.NewState.Player("b").Gold);
        Assert.Equal(1, result.NewState.Player("b").Hp);
        Assert.Equal(3, result.NewState.RoundNumber);
    }

    [Fact]
    public void IllegalActionAtItsStep_FizzlesIntoDodge()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Attack("b"), TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge])));

        Assert.Contains(result.Reveal, s => s is RevealStep.ActionFizzled("a", PlayerAction.Attack));
        Assert.Equal(2, result.NewState.Player("b").Hp);
    }

    [Fact]
    public void CancelledLoad_MakesLaterAttackFizzle()
    {
        // A plans Load→Attack; B shoots A at step 1, cancelling the load, so A's attack fizzles.
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 1, 0));
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Load, TestGame.Attack("b"), TestGame.Dodge]),
            ("b", [TestGame.Attack("a"), TestGame.Dodge, TestGame.Dodge])));

        Assert.Contains(result.Reveal, s => s is RevealStep.ActionCancelled("a", PlayerAction.Load));
        Assert.Contains(result.Reveal, s => s is RevealStep.ActionFizzled("a", PlayerAction.Attack));
        Assert.Equal(2, result.NewState.Player("b").Hp);
    }

    [Fact]
    public void Elimination_EndsTheSequenceEarly()
    {
        var state = TestGame.State(("a", 2, 2, 0), ("b", 2, 0, 0));
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Attack("b"), TestGame.Attack("b"), TestGame.Dodge]),
            ("b", [TestGame.Load, TestGame.Load, TestGame.Chest()])));

        Assert.True(result.IsGameOver);
        Assert.Equal(["a"], result.WinnerIds);
        Assert.Equal(WinReason.LastStanding, result.WinReason);
        Assert.Equal(2, result.NewState.RoundNumber); // third step never resolved
    }

    [Fact]
    public void GoldTarget_EndsTheSequenceEarly()
    {
        var state = TestGame.State(("a", 2, 0, 2), ("b", 2, 0, 0));
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Chest(), TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Load, TestGame.Load, TestGame.Dodge])));

        Assert.True(result.IsGameOver);
        Assert.Equal(["a"], result.WinnerIds);
        Assert.Equal(WinReason.GoldTarget, result.WinReason);
        Assert.Equal(1, result.NewState.RoundNumber);
    }

    [Fact]
    public void SequenceWithoutProgress_IncrementsStalemateCounter()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge])));

        Assert.Equal(1, result.NewState.DuelSequencesWithoutProgress);
        Assert.False(result.NewState.SuddenDeath);
    }

    [Fact]
    public void ReachingStalemateThreshold_TriggersSuddenDeath()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0))
            with { DuelSequencesWithoutProgress = 2 };
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge])));

        Assert.Equal(3, result.NewState.DuelSequencesWithoutProgress);
        Assert.True(result.NewState.SuddenDeath);
    }

    [Fact]
    public void GoldGained_ResetsStalemateCounter()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0))
            with { DuelSequencesWithoutProgress = 2 };
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Chest(), TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge])));

        Assert.Equal(0, result.NewState.DuelSequencesWithoutProgress);
        Assert.False(result.NewState.SuddenDeath);
    }

    [Fact]
    public void SuddenDeath_GrantsAFreeBulletAtSequenceStart()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0)) with { SuddenDeath = true };
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge])));

        Assert.Equal(2, result.Reveal.OfType<RevealStep.SuddenDeathBullet>().Count());
        Assert.Equal(1, result.NewState.Player("a").Bullets);
        Assert.Equal(1, result.NewState.Player("b").Bullets);
        Assert.True(result.NewState.SuddenDeath); // stays on
    }

    [Fact]
    public void SuddenDeath_RemovesTheChest_SoChestActionsFizzle()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0)) with { SuddenDeath = true };
        var result = DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Chest(), TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge])));

        Assert.Contains(result.Reveal, s => s is RevealStep.ActionFizzled("a", PlayerAction.OpenChest));
        Assert.Equal(0, result.NewState.Player("a").Gold);
    }

    [Fact]
    public void WrongSequenceLength_Throws()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        Assert.Throws<InvalidActionException>(() => DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]))));
    }

    [Fact]
    public void MissingSequence_Throws()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));
        Assert.Throws<InvalidActionException>(() => DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]))));
    }

    [Fact]
    public void MoreThanTwoAlivePlayers_Throws()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0), ("c", 2, 0, 0));
        Assert.Throws<InvalidOperationException>(() => DuelResolver.Resolve(state, Seqs(
            ("a", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]),
            ("b", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]))));
    }
}

public class DuelSequenceValidatorTests
{
    private static readonly GameState Duel = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0));

    [Fact]
    public void LoadThenAttack_WithEmptyGun_IsValid() =>
        Assert.Null(DuelResolver.ValidateSequence(
            Duel, "a", [TestGame.Load, TestGame.Attack("b"), TestGame.Dodge]));

    [Fact]
    public void AttackFirst_WithEmptyGun_IsInvalid() =>
        Assert.NotNull(DuelResolver.ValidateSequence(
            Duel, "a", [TestGame.Attack("b"), TestGame.Load, TestGame.Dodge]));

    [Fact]
    public void Attack_NotTargetingOpponent_IsInvalid() =>
        Assert.NotNull(DuelResolver.ValidateSequence(
            Duel, "a", [TestGame.Load, TestGame.Attack("ghost"), TestGame.Dodge]));

    [Fact]
    public void LoadingBeyondGunCapacity_IsInvalid() =>
        Assert.NotNull(DuelResolver.ValidateSequence(
            Duel, "a", [TestGame.Load, TestGame.Load, TestGame.Load]));

    [Fact]
    public void Chest_DuringSuddenDeath_IsInvalid()
    {
        var suddenDeath = Duel with { SuddenDeath = true };
        Assert.NotNull(DuelResolver.ValidateSequence(
            suddenDeath, "a", [TestGame.Chest(), TestGame.Dodge, TestGame.Dodge]));
    }

    [Fact]
    public void SuddenDeathFreeBullet_CountsInProjection()
    {
        var suddenDeath = Duel with { SuddenDeath = true };
        Assert.Null(DuelResolver.ValidateSequence(
            suddenDeath, "a", [TestGame.Attack("b"), TestGame.Dodge, TestGame.Dodge]));
    }

    [Fact]
    public void WrongLength_IsInvalid() =>
        Assert.NotNull(DuelResolver.ValidateSequence(Duel, "a", [TestGame.Dodge]));

    [Fact]
    public void EliminatedPlayer_IsInvalid()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0), ("dead", 0, 0, 0));
        Assert.NotNull(DuelResolver.ValidateSequence(
            state, "dead", [TestGame.Dodge, TestGame.Dodge, TestGame.Dodge]));
    }
}
