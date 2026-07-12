using MexicanStandoff.Server.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace MexicanStandoff.Server.IntegrationTests;

/// <summary>
/// Dev bot seats: config-gated, host-only, and games with bots always resolve
/// because the server auto-submits a legal action/sequence for every bot each
/// selection phase (rounds, the duel, and after a rematch).
/// </summary>
public class BotTests
{
    [Fact]
    public async Task AddBot_IsRejectedWhenBotsAreDisabled()
    {
        using var factory = new StandoffServerFactory(); // bots off by default
        await using var client = await GameClient.ConnectAsync(factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");

        var ex = await Assert.ThrowsAsync<HubException>(() => client.AddBot(code, anna.PlayerToken));
        Assert.Contains("not enabled", ex.Message);
    }

    [Fact]
    public async Task AddBot_HostOnly_BotsGetSeatsWithFreshAvatars()
    {
        using var factory = new StandoffServerFactory { BotsEnabled = true };
        await using var client = await GameClient.ConnectAsync(factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna"); // first seat = host
        var bob = await client.Join(code, "Bob");

        await Assert.ThrowsAsync<HubException>(() => client.AddBot(code, bob.PlayerToken));

        await client.AddBot(code, anna.PlayerToken);
        LobbyView lobby = null!;
        for (var i = 0; i < 3; i++) // 2 joins + 1 bot
            lobby = await client.NextLobby();

        Assert.True(lobby.BotsEnabled);
        Assert.Equal(3, lobby.Players.Count);
        Assert.All(lobby.Players.Take(2), p => Assert.False(p.IsBot));
        var bot = lobby.Players[^1];
        Assert.True(bot.IsBot);
        Assert.StartsWith("Bot ", bot.Name);
        Assert.Equal(lobby.Players.Count, lobby.Players.Select(p => p.Avatar).Distinct().Count());
    }

    [Fact]
    public async Task Monitor_AddsBotsWithItsOwnToken_AndABotsOnlyGameAutoPlays()
    {
        using var factory = new StandoffServerFactory { BotsEnabled = true };
        await using var monitor = await GameClient.ConnectAsync(factory);
        var code = await monitor.CreateGame();

        await monitor.AddBot(code); // the monitor token it got from CreateGame
        await monitor.AddBot(code);
        LobbyView lobby = null!;
        for (var i = 0; i < 2; i++)
            lobby = await monitor.NextLobby();
        Assert.All(lobby.Players, p => Assert.True(p.IsBot));

        // Nobody human is seated; the bots play the round entirely on their own.
        await monitor.Start(code);
        await monitor.NextRound();
        var resolved = await monitor.NextResolved();
        Assert.True(resolved.Snapshot.DuelVolley == 1 || resolved.WinnerIds is not null);
    }

    [Fact]
    public async Task HumansAndBots_PlayARoundToResolution()
    {
        using var factory = new StandoffServerFactory { BotsEnabled = true };
        await using var client = await GameClient.ConnectAsync(factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        var bob = await client.Join(code, "Bob");
        await client.AddBot(code, anna.PlayerToken);
        await client.AddBot(code, anna.PlayerToken);

        await client.Start(code);
        var round = await client.NextRound();
        Assert.Equal(4, round.Snapshot.Players.Count);

        // Only the humans act; the bots lock in on their own.
        await client.Submit(code, anna.PlayerToken, Actions.Load);
        await client.Submit(code, bob.PlayerToken, Actions.Load);

        var resolved = await client.NextResolved();
        Assert.Equal(1, resolved.Snapshot.RoundNumber);
        Assert.Contains(resolved.Reveal, s => s.Type == "actionsRevealed");
    }

    [Fact]
    public async Task HumanVsBot_PlaysToGameOver_AndTheBotRejoinsTheRematch()
    {
        using var factory = new StandoffServerFactory { BotsEnabled = true };
        await using var client = await GameClient.ConnectAsync(factory);
        var code = await client.CreateGame();
        var anna = await client.Join(code, "Anna");
        await client.AddBot(code, anna.PlayerToken);

        await client.Start(code);
        var round = await client.NextRound();
        Assert.True(round.Snapshot.IsDuel);

        var final = await PlayVolleysUntilGameOver(client, code, anna, round.Snapshot);
        Assert.NotNull(final.WinnerIds);

        // Rematch returns to the lobby with the bot's seat intact; starting from
        // there begins the fresh game.
        await client.Rematch(code);
        var lobby = await client.NextReturnedToLobby();
        Assert.True(lobby.Players.Single(p => p.Name != "Anna").IsBot);
        await client.Start(code);
        var rematch = await client.NextRound();
        Assert.Equal(2, rematch.Snapshot.Players.Count);
        Assert.All(rematch.Snapshot.Players, p => Assert.True(p.IsAlive));

        // The bot re-engages after the rematch: one more volley resolves.
        await client.SubmitSequence(code, anna.PlayerToken, AggressiveSequence(rematch.Snapshot, anna.PlayerId));
        var next = await client.NextResolved();
        Assert.Equal(1, next.Snapshot.DuelVolley);
    }

    /// <summary>The human fires every volley, so the duel ends by a kill (either side) quickly.</summary>
    private static async Task<RoundResolvedView> PlayVolleysUntilGameOver(
        GameClient client, string code, JoinResult human, GameSnapshot snapshot)
    {
        for (var volley = 0; volley < 60; volley++)
        {
            await client.SubmitSequence(code, human.PlayerToken, AggressiveSequence(snapshot, human.PlayerId));
            var resolved = await client.NextResolved();
            if (resolved.WinnerIds is not null)
                return resolved;
            snapshot = resolved.Snapshot;
        }

        Assert.Fail("The duel did not finish within 60 volleys.");
        return null!; // unreachable
    }

    /// <summary>A legal shoot-as-much-as-possible sequence for the human's current bullets.</summary>
    private static ActionDto[] AggressiveSequence(GameSnapshot snapshot, string humanId)
    {
        var me = snapshot.Players.Single(p => p.Id == humanId);
        var target = snapshot.Players.Single(p => p.Id != humanId).Id;
        // Mirror the engine's sequence projection: sudden death grants a bullet up front.
        var bullets = Math.Min(me.Bullets + (snapshot.SuddenDeath ? 1 : 0), snapshot.MaxBullets);
        return bullets switch
        {
            0 => [Actions.Load, Actions.Attack(target), Actions.Load],
            1 => [Actions.Attack(target), Actions.Load, Actions.Attack(target)],
            _ => [Actions.Attack(target), Actions.Attack(target), Actions.Dodge],
        };
    }
}
