using MexicanStandoff.Server.Contracts;
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
    public async Task Join_AssignsAvatars_PreferredWhenFree_OtherwiseFirstFree()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();

        await client.Join(code, "Anna", "cascabel");
        await client.Join(code, "Bob", "cascabel"); // taken → first free ("forastero")
        await client.Join(code, "Cleo"); // no preference → next free ("viuda")
        var lobby = await client.NextLobby();
        lobby = await client.NextLobby();
        lobby = await client.NextLobby();

        Assert.Equal(["cascabel", "forastero", "viuda"], lobby.Players.Select(p => p.Avatar).ToArray());
        Assert.Equal(lobby.Players.Count, lobby.Players.Select(p => p.Avatar).Distinct().Count());
    }

    [Fact]
    public async Task Join_Simultaneous_SamePreferredAvatar_NeverDuplicates()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();

        // A full game's worth of connections race for the same avatar. Joins
        // serialize on the session lock, so exactly one wins the preference and
        // the rest fall back to a free portrait — no duplicates, ever.
        await Task.WhenAll(Enumerable.Range(1, 8).Select(async i =>
        {
            await using var phone = await GameClient.ConnectAsync(_factory);
            await phone.Join(code, $"Player {i}", "viuda");
        }));

        var view = await client.Watch(code);
        var avatars = view.Lobby.Players.Select(p => p.Avatar).ToArray();
        Assert.Equal(8, avatars.Length);
        Assert.Equal(8, avatars.Distinct().Count());
        Assert.Contains("viuda", avatars);
    }

    [Fact]
    public async Task CreateGame_WithSelectionTimer_OverridesConfigAndAutoResolves()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        // The factory's configured timer is 0 (off) — the per-game setting must win.
        var code = await client.CreateGame(new CreateGameSettings(SelectionTimerSeconds: 1));
        await client.Join(code, "Anna");
        await client.Join(code, "Bob");

        await client.Start(code);
        var round = await client.NextRound();
        Assert.NotNull(round.Deadline);

        // Nobody submits: the deadline hits and the round resolves with auto-Dodge.
        var resolved = await client.NextResolved();
        Assert.All(resolved.Snapshot.Players, p => Assert.True(p.IsAlive));
        Assert.NotNull(resolved.NextDeadline);
    }

    [Fact]
    public async Task Leave_RemovesTheSeat_AndLaterJoinsGetFreshIds()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");

        await client.Leave(code, bob.PlayerToken);
        LobbyView lobby = null!;
        for (var i = 0; i < 3; i++) // 2 joins + 1 leave
            lobby = await client.NextLobby();
        Assert.Equal(["Anna"], lobby.Players.Select(p => p.Name).ToArray());

        // The freed seat's token is dead and its id is never reissued.
        await Assert.ThrowsAsync<HubException>(() => client.Leave(code, bob.PlayerToken));
        var cleo = await client.Join(code, "Cleo");
        Assert.NotEqual(bob.PlayerId, cleo.PlayerId);
    }

    [Fact]
    public async Task Kick_OnlyTheHostCanRemoveOtherPlayers()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna"); // first seat = host
        var bob = await client.Join(code, "Bob");
        var cleo = await client.Join(code, "Cleo");

        await Assert.ThrowsAsync<HubException>(() => client.Kick(code, bob.PlayerToken, cleo.PlayerId));
        await Assert.ThrowsAsync<HubException>(() => client.Kick(code, anna.PlayerToken, anna.PlayerId));

        await client.Kick(code, anna.PlayerToken, bob.PlayerId);
        LobbyView lobby = null!;
        for (var i = 0; i < 4; i++) // 3 joins + 1 kick
            lobby = await client.NextLobby();
        Assert.Equal(["Anna", "Cleo"], lobby.Players.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Kick_FromTheMonitor_NeedsTheMonitorToken_AndCanRemoveTheHost()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna"); // host
        await client.Join(code, "Bob");

        // Knowing the game code buys nothing: a kick needs a control token, and
        // watching as a monitor is itself gated by the monitor token.
        await Assert.ThrowsAsync<HubException>(() => client.Kick(code, null, anna.PlayerId));

        await client.Kick(code, client.MonitorToken, anna.PlayerId);
        LobbyView lobby = null!;
        for (var i = 0; i < 3; i++) // 2 joins + 1 kick
            lobby = await client.NextLobby();
        Assert.Equal(["Bob"], lobby.Players.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Kick_MidGame_ForcesAResign_AndOnlyHostOrMonitorMayDoIt()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna"); // host
        var bob = await client.Join(code, "Bob");
        var cleo = await client.Join(code, "Cleo");
        await client.Start(code);
        await client.NextRound();

        // A non-host player cannot kick mid-game — otherwise kicking your rivals
        // out one by one would be a way to win.
        var ex = await Assert.ThrowsAsync<HubException>(() => client.Kick(code, bob.PlayerToken, cleo.PlayerId));
        Assert.Contains("host", ex.Message);

        // Nor can anyone kick tokenless by pretending to be the monitor.
        await Assert.ThrowsAsync<HubException>(() => client.Kick(code, null, cleo.PlayerId));

        // The monitor kicks Cleo: her dodge-out locks in immediately, and the
        // broadcast flags her resigned so every device shows it at once.
        await client.Kick(code, client.MonitorToken, cleo.PlayerId);
        var locked = await client.NextLock();
        Assert.Equal(1, locked.LockedCount);
        Assert.Contains(cleo.PlayerId, locked.ResignedPlayerIds);

        await client.Submit(code, anna.PlayerToken, Actions.Load);
        await client.Submit(code, bob.PlayerToken, Actions.Load);
        var resolved = await client.NextResolved();

        // Kicked = forced resign: eliminated at resolution, gold abandoned.
        Assert.Contains(resolved.Reveal, s => s.Type == "playerResigned" && s.PlayerId == cleo.PlayerId);
        Assert.False(resolved.Snapshot.Players.Single(p => p.Id == cleo.PlayerId).IsAlive);
        Assert.True(resolved.Snapshot.IsDuel); // Anna and Bob fight on
    }

    [Fact]
    public async Task Leave_AfterStart_IsRejected()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.Start(code);
        await client.NextRound();

        // Seats are fixed once the game starts — leaving mid-game is resigning
        // (and kicking is a forced resign, covered by the mid-game kick test).
        await Assert.ThrowsAsync<HubException>(() => client.Leave(code, bob.PlayerToken));
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
        Assert.Equal(2, resolved.Snapshot.Players.Single(p => p.Id == cleo.PlayerId).Gold);
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
        Assert.Equal(0, round.Snapshot.DuelVolley);

        // Sequence 1: Anna arms up and lands a hit; Bob farms the chest.
        await client.SubmitSequence(code, anna.PlayerToken, Actions.Load, Actions.Load, Actions.Attack(bob.PlayerId));
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Chest(0), Actions.Chest(0), Actions.Chest(0));
        var resolved = await client.NextResolved();
        Assert.Null(resolved.WinnerIds);
        Assert.Equal(1, resolved.Snapshot.DuelVolley);
        Assert.Equal(4, resolved.Snapshot.Players.Single(p => p.Id == bob.PlayerId).Gold);
        Assert.Equal(1, resolved.Snapshot.Players.Single(p => p.Id == bob.PlayerId).Hp);

        // Sequence 2: Anna finishes it before Bob's chest run reaches the target.
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

    /// <summary>
    /// The game code is public — it is on the big screen and read aloud — so it
    /// must authorize nothing. A player who claimed the monitor role could kick
    /// their rivals out of the round one by one and win by elimination.
    /// </summary>
    [Fact]
    public async Task GameCode_AloneGrantsNoControl_OverSomeoneElsesGame()
    {
        await using var host = await GameClient.ConnectAsync(_factory);
        var code = await host.CreateGame();
        var anna = await host.Join(code, "Anna"); // host seat
        var bob = await host.Join(code, "Bob");

        // A phone on the same wifi: it knows the code (that is how it joined) and
        // nothing else.
        await using var intruder = await GameClient.ConnectAsync(_factory);
        var mallory = await intruder.Join(code, "Mallory");

        // It cannot claim the big screen's authority — not with no token, not
        // with a seat token, not by guessing.
        await Assert.ThrowsAsync<HubException>(() => intruder.WatchAsMonitor(code, monitorToken: ""));
        await Assert.ThrowsAsync<HubException>(() => intruder.WatchAsMonitor(code, mallory.PlayerToken));

        // ...and so every control stays shut, whatever token it tries.
        foreach (var token in new string?[] { null, "", mallory.PlayerToken, bob.PlayerToken })
        {
            await Assert.ThrowsAsync<HubException>(() => intruder.Kick(code, token, anna.PlayerId));
            await Assert.ThrowsAsync<HubException>(() => intruder.Start(code, token));
            await Assert.ThrowsAsync<HubException>(() => intruder.Stop(code, token));
        }

        // Watching is still open to anyone with the code — that part is the point.
        var view = await intruder.Watch(code);
        Assert.Equal("Lobby", view.Phase);
        Assert.Equal(3, view.Lobby.Players.Count);
    }

    /// <summary>
    /// Plays a 2-player game to a winner (Anna kills Bob over two duel volleys).
    /// <paramref name="fromHostSeat"/> starts it on Anna's seat token instead of the
    /// monitor token — the monitor-less "Host &amp; play" game.
    /// </summary>
    private static async Task<(JoinResult Anna, JoinResult Bob)> PlayTwoPlayerGameToWinner(
        GameClient client, string code, bool fromHostSeat = false)
    {
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.Start(code, fromHostSeat ? anna.PlayerToken : null);
        await client.NextRound();

        await client.SubmitSequence(code, anna.PlayerToken, Actions.Load, Actions.Load, Actions.Attack(bob.PlayerId));
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Dodge, Actions.Dodge, Actions.Load);
        await client.NextResolved();
        await client.SubmitSequence(code, anna.PlayerToken, Actions.Attack(bob.PlayerId), Actions.Load, Actions.Load);
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Load, Actions.Load, Actions.Dodge);
        var final = await client.NextResolved();
        Assert.NotNull(final.WinnerIds);
        return (anna, bob);
    }

    /// <summary>
    /// The "Host &amp; play" flow (docs/host-without-monitor.md): the phone that created
    /// the game takes the first seat instead of opening a monitor page, so the whole
    /// game — start, rematch, start again — runs on the host's own seat token and no
    /// monitor token is ever proved.
    /// </summary>
    [Fact]
    public async Task HostAndPlay_RunsTheWholeGame_FromTheHostSeat_WithNoMonitor()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var (anna, _) = await PlayTwoPlayerGameToWinner(client, code, fromHostSeat: true);
        Assert.False((await client.Watch(code)).HasMonitor);

        // No big screen to defer to: the rematch is the host's to call.
        await client.Rematch(code, anna.PlayerToken);
        var lobby = await client.NextReturnedToLobby();
        Assert.True(lobby.CanStart);

        await client.Start(code, anna.PlayerToken);
        var round = await client.NextRound();
        Assert.Equal(0, round.Snapshot.RoundNumber);
    }

    [Fact]
    public async Task StartGame_FromANonHostSeat_IsRejected()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        await client.Join(code, "Anna"); // first seat = host
        var bob = await client.Join(code, "Bob");

        // Starting is not something any player may spring on the room.
        var ex = await Assert.ThrowsAsync<HubException>(() => client.Start(code, bob.PlayerToken));
        Assert.Contains("host", ex.Message);
    }

    [Fact]
    public async Task Rematch_ReturnsEveryoneToTheLobby_WhereLeavingIsPossible()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var (_, bob) = await PlayTwoPlayerGameToWinner(client, code);

        await client.Rematch(code);
        var lobby = await client.NextReturnedToLobby();
        Assert.Equal(["Anna", "Bob"], lobby.Players.Select(p => p.Name).ToArray());
        Assert.True(lobby.CanStart);

        // Nobody is forced into the next game — Bob opts out.
        await client.Leave(code, bob.PlayerToken);
        for (var i = 0; i < 3; i++) // 2 joins + 1 leave
            lobby = await client.NextLobby();
        Assert.Equal(["Anna"], lobby.Players.Select(p => p.Name).ToArray());
        Assert.False(lobby.CanStart);
    }

    [Fact]
    public async Task Rematch_ThenStart_BeginsAFreshGame()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        await PlayTwoPlayerGameToWinner(client, code);

        await client.Rematch(code);
        await client.NextReturnedToLobby();
        await client.Start(code);
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

    [Fact]
    public async Task Rematch_IsMonitorOnly_WhileAMonitorIsWatching()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var (anna, _) = await PlayTwoPlayerGameToWinner(client, code);

        await using var monitor = await GameClient.ConnectAsync(_factory);
        await monitor.WatchAsMonitor(code, client.MonitorToken);
        Assert.True(await client.NextMonitorPresence());
        Assert.True((await client.Watch(code)).HasMonitor); // late hydrators see it too

        // The host's phone can no longer trigger the rematch...
        var ex = await Assert.ThrowsAsync<HubException>(() => client.Rematch(code, anna.PlayerToken));
        Assert.Contains("monitor", ex.Message);

        // ...but the monitor can.
        await monitor.Rematch(code, client.MonitorToken);
        await client.NextReturnedToLobby();
    }

    [Fact]
    public async Task Rematch_FromTheHostsPhone_WorksAgain_AfterTheMonitorDisconnects()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var (anna, bob) = await PlayTwoPlayerGameToWinner(client, code);

        var monitor = await GameClient.ConnectAsync(_factory);
        await monitor.WatchAsMonitor(code, client.MonitorToken);
        Assert.True(await client.NextMonitorPresence());

        await monitor.DisposeAsync();
        Assert.False(await client.NextMonitorPresence());

        // With the big screen gone the host runs the game — but only the host:
        // a rematch is not something any player may force on the room.
        await Assert.ThrowsAsync<HubException>(() => client.Rematch(code, bob.PlayerToken));

        await client.Rematch(code, anna.PlayerToken);
        await client.NextReturnedToLobby();
    }

    [Fact]
    public async Task StopGame_BroadcastsToEveryone_AndKillsTheSession()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        await client.Join(code, "Bob");
        await client.Join(code, "Cleo");
        await client.Start(code);
        await client.NextRound();

        await client.Stop(code);
        await client.NextStopped();

        // The session is terminal: no more actions, no joins, no rematch.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => client.Submit(code, anna.PlayerToken, Actions.Load));
        Assert.Contains("not waiting for actions", ex.Message);
        ex = await Assert.ThrowsAsync<HubException>(() => client.Join(code, "Dora"));
        Assert.Contains("stopped", ex.Message);
        ex = await Assert.ThrowsAsync<HubException>(() => client.Rematch(code));
        Assert.Contains("stopped", ex.Message);

        // A late device hydrating the game sees the stopped phase.
        var view = await client.Watch(code);
        Assert.Equal("Stopped", view.Phase);

        // A second stop click racing the first is a no-op, not an error.
        await client.Stop(code);
    }

    [Fact]
    public async Task Resign_DodgesOutTheRound_ThenLeavesTheGame()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        var cleo = await client.Join(code, "Cleo");
        await client.Start(code);
        await client.NextRound();

        await client.Resign(code, anna.PlayerToken);
        var locked = await client.NextLock();
        Assert.Equal(1, locked.LockedCount);

        // Resigned players cannot act anymore.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => client.Submit(code, anna.PlayerToken, Actions.Load));
        Assert.Contains("resigned", ex.Message);

        await client.Submit(code, bob.PlayerToken, Actions.Load);
        await client.Submit(code, cleo.PlayerToken, Actions.Load);
        var resolved = await client.NextResolved();

        // Anna dodged the volley, then walked: eliminated, no future rounds.
        Assert.Contains(resolved.Reveal, s => s.Type == "playerResigned" && s.PlayerId == anna.PlayerId);
        Assert.False(resolved.Snapshot.Players.Single(p => p.Id == anna.PlayerId).IsAlive);
        Assert.True(resolved.Snapshot.IsDuel); // Bob and Cleo fight on
    }

    [Fact]
    public async Task Resign_DuringTheDuel_HandsTheOpponentTheWin()
    {
        await using var client = await GameClient.ConnectAsync(_factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.Start(code);
        var round = await client.NextRound();
        Assert.True(round.Snapshot.IsDuel);

        // Anna resigns; her all-Dodge sequence is locked in, so Bob's submission
        // resolves the volley. She walks after step 1 — Bob is last standing.
        await client.Resign(code, anna.PlayerToken);
        await client.SubmitSequence(code, bob.PlayerToken, Actions.Chest(0), Actions.Chest(0), Actions.Chest(0));
        var resolved = await client.NextResolved();

        Assert.Equal([bob.PlayerId], resolved.WinnerIds);
        Assert.Equal("LastStanding", resolved.WinReason);
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
