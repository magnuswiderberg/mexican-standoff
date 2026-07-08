namespace MexicanStandoff.Engine.Tests;

public class ActionValidatorTests
{
    private static readonly GameState State = TestGame.State(
        ("armed", 2, 2, 0), ("empty", 2, 0, 0), ("full", 2, 2, 0), ("dead", 0, 0, 0));

    [Fact]
    public void Dodge_IsAlwaysValid() =>
        Assert.Null(ActionValidator.Validate(State, "empty", TestGame.Dodge));

    [Fact]
    public void Load_WithRoomInGun_IsValid() =>
        Assert.Null(ActionValidator.Validate(State, "empty", TestGame.Load));

    [Fact]
    public void Load_WithFullGun_IsInvalid() =>
        Assert.NotNull(ActionValidator.Validate(State, "full", TestGame.Load));

    [Fact]
    public void Attack_WithBullet_IsValid() =>
        Assert.Null(ActionValidator.Validate(State, "armed", TestGame.Attack("empty")));

    [Fact]
    public void Attack_WithEmptyGun_IsInvalid() =>
        Assert.NotNull(ActionValidator.Validate(State, "empty", TestGame.Attack("armed")));

    [Fact]
    public void Attack_Self_IsInvalid() =>
        Assert.NotNull(ActionValidator.Validate(State, "armed", TestGame.Attack("armed")));

    [Fact]
    public void Attack_UnknownTarget_IsInvalid() =>
        Assert.NotNull(ActionValidator.Validate(State, "armed", TestGame.Attack("ghost")));

    [Fact]
    public void Attack_EliminatedTarget_IsInvalid() =>
        Assert.NotNull(ActionValidator.Validate(State, "armed", TestGame.Attack("dead")));

    [Fact]
    public void Chest_ExistingIndex_IsValid() =>
        Assert.Null(ActionValidator.Validate(State, "empty", TestGame.Chest(0)));

    [Fact]
    public void Chest_IndexOutOfRange_IsInvalid()
    {
        // 3 alive → 1 chest, so index 1 does not exist.
        Assert.NotNull(ActionValidator.Validate(State, "empty", TestGame.Chest(1)));
        Assert.NotNull(ActionValidator.Validate(State, "empty", TestGame.Chest(-1)));
    }

    [Fact]
    public void EliminatedPlayer_CannotAct() =>
        Assert.NotNull(ActionValidator.Validate(State, "dead", TestGame.Dodge));

    [Fact]
    public void UnknownPlayer_CannotAct() =>
        Assert.NotNull(ActionValidator.Validate(State, "ghost", TestGame.Dodge));
}
