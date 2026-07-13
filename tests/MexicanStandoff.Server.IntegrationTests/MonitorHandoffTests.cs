using MexicanStandoff.Server.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace MexicanStandoff.Server.IntegrationTests;

/// <summary>
/// The phone → TV handoff (docs/host-without-monitor.md): a game started on a phone
/// keeps its monitor token on that phone, so a big screen joining later has no way to
/// prove it is the board. It asks; the host allows; the token travels to that screen
/// alone. Every test here starts a monitor-less game — the phone hosts from its seat.
/// </summary>
public class MonitorHandoffTests : IClassFixture<StandoffServerFactory>
{
    private readonly StandoffServerFactory _factory;

    public MonitorHandoffTests(StandoffServerFactory factory) => _factory = factory;

    [Fact]
    public async Task TheHostAllowsTheScreen_AndItBecomesTheMonitor()
    {
        await using var phone = await GameClient.ConnectAsync(_factory);
        await using var tv = await GameClient.ConnectAsync(_factory);
        var code = await phone.CreateGame();
        var anna = await phone.Join(code, "Anna"); // first seat = host
        await phone.Join(code, "Bob");

        // The TV never created this game, so it holds no monitor token.
        var request = await tv.RequestMonitor(code);
        Assert.Equal(4, request.PairCode.Length);

        // The host's phone is told a screen is waiting, and by which code — that is
        // what lets them check they are approving the TV in the room.
        var prompt = await phone.NextMonitorRequest();
        Assert.Equal(request.PairCode, prompt?.PairCode);
        // A host that reloads mid-request still finds the prompt, with time left on it.
        var pending = (await phone.Watch(code)).PendingMonitor;
        Assert.Equal(request.PairCode, pending?.PairCode);
        Assert.InRange(pending!.ExpiresInSeconds, 1, 120);

        // The host allows it with their *seat* token — there is no monitor to defer to.
        await phone.DecideMonitor(code, anna.PlayerToken, allow: true);

        var decision = await tv.NextMonitorDecision();
        Assert.True(decision.Granted);
        Assert.Equal(phone.MonitorToken, decision.MonitorToken); // the very token the phone got

        // The prompt is spent, and the TV can now take the board.
        Assert.Null(await phone.NextMonitorRequest());
        await tv.WatchAsMonitor(code, decision.MonitorToken);
        Assert.True(await phone.NextMonitorPresence());

        // And the rematch moves to the big screen, like any monitor game.
        Assert.True((await phone.Watch(code)).HasMonitor);
    }

    [Fact]
    public async Task TheHostDeclines_AndTheScreenGetsNoToken()
    {
        await using var phone = await GameClient.ConnectAsync(_factory);
        await using var tv = await GameClient.ConnectAsync(_factory);
        var code = await phone.CreateGame();
        var anna = await phone.Join(code, "Anna");

        await tv.RequestMonitor(code);
        await phone.NextMonitorRequest();
        await phone.DecideMonitor(code, anna.PlayerToken, allow: false);

        var decision = await tv.NextMonitorDecision();
        Assert.False(decision.Granted);
        Assert.Null(decision.MonitorToken);
        Assert.Null(await phone.NextMonitorRequest()); // the prompt is gone

        // The request is spent: answering it twice is not possible.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => phone.DecideMonitor(code, anna.PlayerToken, allow: true));
        Assert.Contains("no longer waiting", ex.Message);
    }

    [Fact]
    public async Task AnyPlayerMayAsk_ButOnlyTheHostMayAnswer()
    {
        await using var phone = await GameClient.ConnectAsync(_factory);
        await using var tv = await GameClient.ConnectAsync(_factory);
        var code = await phone.CreateGame();
        await phone.Join(code, "Anna"); // host
        var bob = await phone.Join(code, "Bob");

        await tv.RequestMonitor(code);
        await phone.NextMonitorRequest();

        // Otherwise any phone in the room could hand itself the controls by opening
        // the monitor page and approving its own request.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => phone.DecideMonitor(code, bob.PlayerToken, allow: true));
        Assert.Contains("host", ex.Message);

        // Asking without answering leaks nothing: an unapproved screen has no token,
        // and the monitor page will not let it in without one.
        ex = await Assert.ThrowsAsync<HubException>(() => tv.WatchAsMonitor(code, "not-the-token"));
        Assert.Contains("not the monitor", ex.Message);
    }

    [Fact]
    public async Task AScreenCannotAskMidGame_ButMayAskOnceItEnds()
    {
        await using var phone = await GameClient.ConnectAsync(_factory);
        await using var tv = await GameClient.ConnectAsync(_factory);
        var code = await phone.CreateGame();
        var anna = await phone.Join(code, "Anna");
        var bob = await phone.Join(code, "Bob");
        await phone.Start(code, anna.PlayerToken);
        await phone.NextRound();

        // Nobody sets up a TV during a duel, and the prompt would land on the host's
        // phone on top of the action picker.
        var ex = await Assert.ThrowsAsync<HubException>(() => tv.RequestMonitor(code));
        Assert.Contains("between games", ex.Message);

        // The game ends (Anna walks, Bob is last standing); now the board is welcome.
        await phone.Resign(code, anna.PlayerToken);
        await phone.SubmitSequence(code, bob.PlayerToken, Actions.Dodge, Actions.Dodge, Actions.Dodge);
        var final = await phone.NextResolved();
        Assert.Equal([bob.PlayerId], final.WinnerIds);

        var request = await tv.RequestMonitor(code);
        Assert.Equal(request.PairCode, (await phone.NextMonitorRequest())?.PairCode);
    }

    [Fact]
    public async Task StartingTheGame_TellsAWaitingScreen_InsteadOfLeavingItHanging()
    {
        await using var phone = await GameClient.ConnectAsync(_factory);
        await using var tv = await GameClient.ConnectAsync(_factory);
        var code = await phone.CreateGame();
        var anna = await phone.Join(code, "Anna");
        await phone.Join(code, "Bob");

        await tv.RequestMonitor(code);
        await phone.NextMonitorRequest();

        // The host ignores the prompt and starts. The screen must not sit on
        // "waiting for the host" for the length of a whole game.
        await phone.Start(code, anna.PlayerToken);
        await phone.NextRound();

        var decision = await tv.NextMonitorDecision();
        Assert.False(decision.Granted);
        Assert.Null(decision.MonitorToken);
        Assert.Contains("game started", decision.Message);
        Assert.Null(await phone.NextMonitorRequest()); // and the prompt is gone
    }

    /// <summary>
    /// Nobody answers. The request has to die on its own, or the waiting screen sits
    /// on "waiting for the host" forever and the host keeps an Allow button that can
    /// only fail. Both sides run the server's clock down — hence the seconds it ships.
    /// </summary>
    [Fact]
    public async Task AnUnansweredRequest_Expires()
    {
        using var factory = new StandoffServerFactory { MonitorRequestLifetime = TimeSpan.FromSeconds(1) };
        await using var phone = await GameClient.ConnectAsync(factory);
        await using var tv = await GameClient.ConnectAsync(factory);
        var code = await phone.CreateGame();
        var anna = await phone.Join(code, "Anna");

        await tv.RequestMonitor(code);
        await phone.NextMonitorRequest();

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        // The host never noticed. Their prompt is gone from a fresh hydrate...
        Assert.Null((await phone.Watch(code)).PendingMonitor);
        // ...and a late tap on a stale Allow is refused rather than handing out the token.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => phone.DecideMonitor(code, anna.PlayerToken, allow: true));
        Assert.Contains("no longer waiting", ex.Message);
    }

    [Fact]
    public async Task ASecondScreenReplacesTheFirst_SoPromptsCannotStackUpOnTheHost()
    {
        await using var phone = await GameClient.ConnectAsync(_factory);
        await using var first = await GameClient.ConnectAsync(_factory);
        await using var second = await GameClient.ConnectAsync(_factory);
        var code = await phone.CreateGame();
        var anna = await phone.Join(code, "Anna");

        var firstRequest = await first.RequestMonitor(code);
        var secondRequest = await second.RequestMonitor(code);
        Assert.NotEqual(firstRequest.PairCode, secondRequest.PairCode);

        // The host is looking at the second screen's code, so that is who they allow.
        Assert.Equal(firstRequest.PairCode, (await phone.NextMonitorRequest())?.PairCode);
        Assert.Equal(secondRequest.PairCode, (await phone.NextMonitorRequest())?.PairCode);
        await phone.DecideMonitor(code, anna.PlayerToken, allow: true);

        var decision = await second.NextMonitorDecision();
        Assert.True(decision.Granted);
        await second.WatchAsMonitor(code, decision.MonitorToken);
        Assert.True(await phone.NextMonitorPresence());
    }
}
