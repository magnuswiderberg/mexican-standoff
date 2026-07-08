namespace MexicanStandoff.Engine.Tests;

public class GameStateTests
{
    private static readonly (string, string)[] FourPlayers =
        [("a", "Anna"), ("b", "Bob"), ("c", "Cleo"), ("d", "Dan")];

    [Fact]
    public void New_GivesEveryPlayerStartingStats()
    {
        var state = GameState.New(GameParameters.Default, FourPlayers);

        Assert.All(state.Players, p =>
        {
            Assert.Equal(2, p.Hp);
            Assert.Equal(0, p.Bullets);
            Assert.Equal(0, p.Gold);
            Assert.True(p.IsAlive);
        });
        Assert.Equal(0, state.RoundNumber);
    }

    [Fact]
    public void New_StartingBullets_AppliedAndCappedAtGunSize()
    {
        var parameters = GameParameters.Default with { StartingBullets = 5, MaxBullets = 2 };
        var state = GameState.New(parameters, FourPlayers);

        Assert.All(state.Players, p => Assert.Equal(2, p.Bullets));
    }

    [Fact]
    public void New_TooFewPlayers_Throws() =>
        Assert.Throws<ArgumentException>(() => GameState.New(GameParameters.Default, [("a", "Anna")]));

    [Fact]
    public void New_TooManyPlayers_Throws()
    {
        var nine = Enumerable.Range(1, 9).Select(i => ($"p{i}", $"Player {i}")).ToArray();
        Assert.Throws<ArgumentException>(() => GameState.New(GameParameters.Default, nine));
    }

    [Fact]
    public void New_DuplicatePlayerIds_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            GameState.New(GameParameters.Default, [("a", "Anna"), ("a", "Also Anna")]));

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(8, 2)]
    public void ChestCount_FollowsAlivePlayerCount(int players, int expectedChests)
    {
        var seats = Enumerable.Range(1, players).Select(i => ($"p{i}", 2, 0, 0)).ToArray();
        Assert.Equal(expectedChests, TestGame.State(seats).ChestCount);
    }

    [Fact]
    public void ChestCount_CountsOnlyAlivePlayers()
    {
        // 6 seats but 2 eliminated → 4 alive → 1 chest.
        var state = TestGame.State(
            ("a", 2, 0, 0), ("b", 2, 0, 0), ("c", 2, 0, 0), ("d", 2, 0, 0), ("e", 0, 0, 0), ("f", 0, 0, 0));
        Assert.Equal(1, state.ChestCount);
    }

    [Fact]
    public void ChestCount_IsZeroInSuddenDeath()
    {
        var state = TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0)) with { SuddenDeath = true };
        Assert.Equal(0, state.ChestCount);
    }

    [Fact]
    public void Player_UnknownId_Throws() =>
        Assert.Throws<ArgumentException>(() => TestGame.State(("a", 2, 0, 0), ("b", 2, 0, 0)).Player("nope"));
}
