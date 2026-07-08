using Microsoft.AspNetCore.SignalR;

namespace MexicanStandoff.Server.IntegrationTests;

/// <summary>
/// Full flows through the real SignalR hub, in-process. One connection can seat
/// several players (identity travels by token), which keeps orchestration simple;
/// multi-connection broadcasting is covered by <see cref="Join_BroadcastsLobbyToAllConnections"/>.
/// </summary>
public class GameHubTests : IClassFixture<StandoffServerFactory>
{
    private readonly StandoffServerFactory _factory;

    public GameHubTests(StandoffServerFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateGame_ReturnsWellFormedCode()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();

        Assert.Equal(4, code.Length);
        Assert.All(code, c => Assert.Contains(c, "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"));
    }

    [Fact]
    public async Task Join_BroadcastsLobbyToAllConnections()
    {
        await using var monitor = await GameClient.ConnectAsync(_factory);
        await using var phone = await GameClient.ConnectAsync(_factory);

        var code = await monitor.CreateGame();
        await phone.Join(code, "Anna");
        var lobbyOnMonitor = await monitor.NextLobby();
        Assert.Single(lobbyOnMonitor.Players);
        Assert.False(lobbyOnMonitor.CanStart);

        await phone.Join(code, "Bob");
        lobbyOnMonitor = await monitor.NextLobby();
        Assert.Equal(2, lobbyOnMonitor.Players.Count);
        Assert.True(lobbyOnMonitor.CanStart);
        Assert.Equal(["Anna", "Bob"], lobbyOnMonitor.Players.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task StartGame_BroadcastsFirstRound()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        await client.Join(code, "Anna");
        await client.Join(code, "Bob");
        await client.Join(code, "Cleo");

        await client.Start(code);
        var round = await client.NextRound();

        Assert.Equal("Selecting", round.Snapshot.Phase);
        Assert.Equal(0, round.Snapshot.RoundNumber);
        Assert.False(round.Snapshot.IsDuel);
        Assert.Equal(1, round.Snapshot.ChestCount);
        Assert.All(round.Snapshot.Players, p => Assert.Equal(2, p.Hp));
        Assert.Null(round.Deadline); // timer disabled in tests
    }

    [Fact]
    public async Task Round_ResolvesWhenAllHaveSubmitted()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        var cleo = await client.Join(code, "Cleo");
        await client.Start(code);
        await client.NextRound();

        await client.Submit(code, anna.PlayerToken, Actions.Load);
        var locked = await client.NextLock();
        Assert.Equal(1, locked.LockedCount);
        Assert.Equal(3, locked.TotalExpected);

        await client.Submit(code, bob.PlayerToken, Actions.Load);
        await client.Submit(code, cleo.PlayerToken, Actions.Chest(0));

        var resolved = await client.NextResolved();
        Assert.Equal(1, resolved.Snapshot.RoundNumber);
        Assert.Null(resolved.WinnerIds);
        Assert.All(resolved.Snapshot.Players.Where(p => p.Id != cleo.PlayerId),
            p => Assert.Equal(1, p.Bullets));
        Assert.Equal(1, resolved.Snapshot.Players.Single(p => p.Id == cleo.PlayerId).Gold);
        Assert.Contains(resolved.Reveal, s => s.Type == "actionsRevealed");
        Assert.Contains(resolved.Reveal, s => s.Type == "chestResolved" && s.ChestWinnerId == cleo.PlayerId);
    }

    [Fact]
    public async Task IllegalAction_IsRejected()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.Join(code, "Cleo");
        await client.Start(code);
        await client.NextRound();

        // Attack with an empty gun.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => client.Submit(code, anna.PlayerToken, Actions.Attack(bob.PlayerId)));
        Assert.Contains("gun is empty", ex.Message);
    }

    [Fact]
    public async Task Elimination_TransitionsToFinalDuel()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        var cleo = await client.Join(code, "Cleo");
        await client.Start(code);
        await client.NextRound();

        // Round 1: everyone loads.
        await client.Submit(code, anna.PlayerToken, Actions.Load);
        await client.Submit(code, bob.PlayerToken, Actions.Load);
        await client.Submit(code, cleo.PlayerToken, Actions.Load);
        await client.NextResolved();

        // Round 2: Anna and Bob both shoot Cleo (2 HP -> dead).
        await client.Submit(code, anna.PlayerToken, Actions.Attack(cleo.PlayerId));
        await client.Submit(code, bob.PlayerToken, Actions.Attack(cleo.PlayerId));
        await client.Submit(code, cleo.PlayerToken, Actions.Load);
        var resolved = await client.NextResolved();

        Assert.False(resolved.Snapshot.Players.Single(p => p.Id == cleo.PlayerId).IsAlive);
        Assert.True(resolved.Snapshot.IsDuel);
        Assert.Null(resolved.WinnerIds);

        // Single actions are no longer accepted — the duel wants sequences.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => client.Submit(code, anna.PlayerToken, Actions.Dodge));
        Assert.Contains("sequence", ex.Message);
    }

    [Fact]
    public async Task TwoPlayerGame_PlaysTheDuelToAWinner()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.Start(code);
        var round = await client.NextRound();
        Assert.True(round.Snapshot.IsDuel);

        // Sequence 1: Anna arms up and lands a hit; Bob farms the chest.
        await client.SubmitSequence(code, anna.PlayerToken, Actions.Load, Actions.Load, Actions.Attack(bob.PlayerId));
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Chest(0), Actions.Chest(0), Actions.Chest(0));
        var resolved = await client.NextResolved();
        Assert.Null(resolved.WinnerIds);
        Assert.Equal(2, resolved.Snapshot.Players.Single(p => p.Id == bob.PlayerId).Gold);
        Assert.Equal(1, resolved.Snapshot.Players.Single(p => p.Id == bob.PlayerId).Hp);

        // Sequence 2: Anna finishes it before Bob can grab his third bar.
        await client.SubmitSequence(code, anna.PlayerToken, Actions.Attack(bob.PlayerId), Actions.Load, Actions.Load);
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Chest(0), Actions.Chest(0), Actions.Chest(0));
        resolved = await client.NextResolved();

        Assert.Equal([anna.PlayerId], resolved.WinnerIds);
        Assert.Equal("LastStanding", resolved.WinReason);
        Assert.Contains(resolved.Reveal, s => s.Type == "gameEnded");
    }

    [Fact]
    public async Task Reconnect_RestoresCurrentState()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.Join(code, "Cleo");
        await client.Start(code);
        await client.NextRound();
        await client.Submit(code, anna.PlayerToken, Actions.Load);

        // A fresh connection (phone refresh) rebinds with the stored token.
        await using var reconnected = await GameClient.ConnectAsync(_factory);
        var view = await reconnected.Reconnect(code, anna.PlayerToken);

        Assert.Equal("Selecting", view.Phase);
        Assert.Equal(anna.PlayerId, view.PlayerId);
        Assert.True(view.HasSubmitted);
        Assert.NotNull(view.Snapshot);

        var bobView = await reconnected.Reconnect(code, bob.PlayerToken);
        Assert.False(bobView.HasSubmitted);
    }

    [Fact]
    public async Task Watch_ReturnsLobbyForMonitor()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        await client.Join(code, "Anna");

        await using var monitor = await GameClient.ConnectAsync(_factory);
        var view = await monitor.Watch(code);

        Assert.Equal("Lobby", view.Phase);
        Assert.Null(view.PlayerId);
        Assert.Single(view.Lobby.Players);
    }

    [Fact]
    public async Task UnknownGameCode_IsRejected()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        await Assert.ThrowsAsync<HubException>(() => client.Watch("XXXX"));
    }

    [Fact]
    public async Task Rematch_StartsAFreshGameWithTheSameSeats()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.Start(code);
        await client.NextRound();

        // Fast finish: Anna kills Bob over two sequences.
        await client.SubmitSequence(code, anna.PlayerToken, Actions.Load, Actions.Load, Actions.Attack(bob.PlayerId));
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Dodge, Actions.Dodge, Actions.Load);
        await client.NextResolved();
        await client.SubmitSequence(code, anna.PlayerToken, Actions.Attack(bob.PlayerId), Actions.Load, Actions.Load);
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Load, Actions.Load, Actions.Dodge);
        var final = await client.NextResolved();
        Assert.NotNull(final.WinnerIds);

        await client.Rematch(code);
        var round = await client.NextRound();

        Assert.Equal(0, round.Snapshot.RoundNumber);
        Assert.All(round.Snapshot.Players, p =>
        {
            Assert.True(p.IsAlive);
            Assert.Equal(2, p.Hp);
            Assert.Equal(0, p.Bullets);
            Assert.Equal(0, p.Gold);
        });
    }
}

/// <summary>Timeout behavior needs a real (short) timer, so it gets its own server.</summary>
public class SelectionTimeoutTests
{
    [Fact]
    public async Task Deadline_AutoDodgesMissingPlayersAndResolves()
    {
        using var factory = new StandoffServerFactory { SelectionTimerSeconds = 1 };
        await using var client = await GameClient.ConnectAsync(factory);

        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        await client.Join(code, "Bob");
        await client.Join(code, "Cleo");
        await client.Start(code);
        var round = await client.NextRound();
        Assert.NotNull(round.Deadline);

        // Only Anna acts; the other two sleep through the deadline.
        await client.Submit(code, anna.PlayerToken, Actions.Load);

        var resolved = await client.NextResolved();
        Assert.Equal(1, resolved.Snapshot.Players.Single(p => p.Id == anna.PlayerId).Bullets);
        Assert.All(resolved.Snapshot.Players.Where(p => p.Id != anna.PlayerId),
            p => Assert.Equal(0, p.Bullets));
        Assert.NotNull(resolved.NextDeadline); // next round's timer is running
    }
}
