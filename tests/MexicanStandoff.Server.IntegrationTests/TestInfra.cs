using System.Threading.Channels;
using MexicanStandoff.Server.Contracts;
using MexicanStandoff.Server.Games;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace MexicanStandoff.Server.IntegrationTests;

/// <summary>In-process server. Selection timer is off by default so tests control pacing.</summary>
public sealed class StandoffServerFactory : WebApplicationFactory<Program>
{
    public int SelectionTimerSeconds { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.Configure<GameOptions>(o => o.SelectionTimerSeconds = SelectionTimerSeconds));
}

/// <summary>
/// A SignalR client wired to the in-process server. Broadcasts land in per-event
/// channels so tests can await them; hub methods are exposed as typed calls.
/// </summary>
public sealed class GameClient : IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly Channel<LobbyView> _lobby = Channel.CreateUnbounded<LobbyView>();
    private readonly Channel<RoundStartedView> _rounds = Channel.CreateUnbounded<RoundStartedView>();
    private readonly Channel<PlayerLockedView> _locks = Channel.CreateUnbounded<PlayerLockedView>();
    private readonly Channel<RoundResolvedView> _resolved = Channel.CreateUnbounded<RoundResolvedView>();

    private GameClient(HubConnection hub)
    {
        _hub = hub;
        hub.On<LobbyView>("LobbyUpdated", v => _lobby.Writer.TryWrite(v));
        hub.On<RoundStartedView>("RoundStarted", v => _rounds.Writer.TryWrite(v));
        hub.On<PlayerLockedView>("PlayerLocked", v => _locks.Writer.TryWrite(v));
        hub.On<RoundResolvedView>("RoundResolved", v => _resolved.Writer.TryWrite(v));
    }

    public static async Task<GameClient> ConnectAsync(StandoffServerFactory factory)
    {
        var hub = new HubConnectionBuilder()
            .WithUrl("http://localhost/hub/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        var client = new GameClient(hub);
        await hub.StartAsync();
        return client;
    }

    public Task<string> CreateGame() => _hub.InvokeAsync<string>("CreateGame");
    public Task<JoinResult> Join(string code, string name) => _hub.InvokeAsync<JoinResult>("JoinGame", code, name);
    public Task<GameView> Watch(string code) => _hub.InvokeAsync<GameView>("WatchGame", code);
    public Task<GameView> Reconnect(string code, string token) => _hub.InvokeAsync<GameView>("Reconnect", code, token);
    public Task Start(string code) => _hub.InvokeAsync("StartGame", code);
    public Task Submit(string code, string token, ActionDto action) =>
        _hub.InvokeAsync("SubmitAction", code, token, action);
    public Task SubmitSequence(string code, string token, params ActionDto[] sequence) =>
        _hub.InvokeAsync("SubmitDuelSequence", code, token, sequence);
    public Task Rematch(string code) => _hub.InvokeAsync("Rematch", code);

    public Task<LobbyView> NextLobby() => Next(_lobby);
    public Task<RoundStartedView> NextRound() => Next(_rounds);
    public Task<PlayerLockedView> NextLock() => Next(_locks);
    public Task<RoundResolvedView> NextResolved() => Next(_resolved);

    private static async Task<T> Next<T>(Channel<T> channel)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await channel.Reader.ReadAsync(cts.Token);
    }

    public ValueTask DisposeAsync() => _hub.DisposeAsync();
}

public static class Actions
{
    public static ActionDto Dodge => new("dodge");
    public static ActionDto Load => new("load");
    public static ActionDto Attack(string targetId) => new("attack", TargetId: targetId);
    public static ActionDto Chest(int index = 0) => new("chest", ChestIndex: index);
}
