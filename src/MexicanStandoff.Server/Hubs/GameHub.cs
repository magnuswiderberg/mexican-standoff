using MexicanStandoff.Server.Contracts;
using MexicanStandoff.Server.Games;
using Microsoft.AspNetCore.SignalR;

namespace MexicanStandoff.Server.Hubs;

/// <summary>
/// Thin SignalR endpoint: group membership + delegation to <see cref="GameService"/>.
/// Player identity travels as a secret token with each call (stored client-side),
/// so reconnects from any connection just work. The game controls take a control
/// token the same way — the host's seat token or the monitor token from
/// <see cref="CreateGame"/> — because the game code is public and authorizes nothing.
/// </summary>
public sealed class GameHub(GameService games) : Hub<IGameClient>
{
    public async Task<CreateGameResult> CreateGame(CreateGameSettings? settings = null)
    {
        var result = games.CreateGame(settings);
        await Groups.AddToGroupAsync(Context.ConnectionId, result.Code);
        return result;
    }

    public async Task<JoinResult> JoinGame(string gameCode, string playerName, string? preferredAvatar = null)
    {
        var code = Normalize(gameCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return await games.JoinAsync(code, playerName, preferredAvatar);
    }

    /// <summary>Subscribe to a game without being a player (join form's live lobby).</summary>
    public async Task<GameView> WatchGame(string gameCode)
    {
        var code = Normalize(gameCode);
        var view = games.GetView(code, token: null);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return view;
    }

    /// <summary>
    /// Monitor page: like <see cref="WatchGame"/>, but registers as the game's
    /// monitor — while one is connected, rematches start from the monitor only.
    /// Takes the monitor token from <see cref="CreateGame"/>: the big screen is a
    /// role, not a URL anyone can open.
    /// </summary>
    public async Task<GameView> WatchAsMonitor(string gameCode, string monitorToken)
    {
        var code = Normalize(gameCode);
        await games.MonitorConnectedAsync(code, monitorToken, Context.ConnectionId);
        Context.Items[MonitorCodeKey] = code;
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return games.GetView(code, token: null);
    }

    /// <summary>
    /// Phone → TV handoff: a screen with no monitor token asks the host to make it
    /// the board. It joins the group so it hears the game while it waits (and so a
    /// grant lands on a connection already subscribed).
    /// </summary>
    public async Task<MonitorRequestView> RequestMonitor(string gameCode)
    {
        var code = Normalize(gameCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return await games.RequestMonitorAsync(code, Context.ConnectionId);
    }

    /// <summary>The host (or a monitor already up) answers the waiting screen.</summary>
    public Task DecideMonitor(string gameCode, string? controlToken, bool allow) =>
        games.DecideMonitorAsync(Normalize(gameCode), controlToken, allow);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(MonitorCodeKey, out var code) && code is string monitorCode)
            await games.MonitorDisconnectedAsync(monitorCode, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Rebind a (new) connection to an existing seat and get the current state.</summary>
    public async Task<GameView> Reconnect(string gameCode, string playerToken)
    {
        var code = Normalize(gameCode);
        var view = games.GetView(code, playerToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return view;
    }

    public Task LeaveGame(string gameCode, string playerToken) =>
        games.LeaveAsync(Normalize(gameCode), playerToken);

    /// <summary>Host kicks with their seat token, the monitor with the monitor token.</summary>
    public Task KickPlayer(string gameCode, string? controlToken, string targetPlayerId) =>
        games.KickAsync(Normalize(gameCode), controlToken, targetPlayerId);

    /// <summary>Dev-only, config-gated: the host or the monitor adds a bot seat.</summary>
    public Task AddBot(string gameCode, string? controlToken = null) =>
        games.AddBotAsync(Normalize(gameCode), controlToken);

    public Task StartGame(string gameCode, string? controlToken) =>
        games.StartAsync(Normalize(gameCode), controlToken);

    public Task SubmitAction(string gameCode, string playerToken, ActionDto action) =>
        games.SubmitActionAsync(Normalize(gameCode), playerToken, action);

    public Task SubmitDuelSequence(string gameCode, string playerToken, ActionDto[] sequence) =>
        games.SubmitDuelSequenceAsync(Normalize(gameCode), playerToken, sequence);

    /// <summary>Resign: dodge out the current round, then leave the game (eliminated, gold lost).</summary>
    public Task Resign(string gameCode, string playerToken) =>
        games.ResignAsync(Normalize(gameCode), playerToken);

    public Task Rematch(string gameCode, string? controlToken) =>
        games.RematchAsync(Normalize(gameCode), controlToken);

    /// <summary>Monitor button: ends the game for everyone.</summary>
    public Task StopGame(string gameCode, string? controlToken) =>
        games.StopAsync(Normalize(gameCode), controlToken);

    /// <summary>Context.Items key: the game code this connection watches as a monitor.</summary>
    private const string MonitorCodeKey = "monitorCode";

    private static string Normalize(string gameCode) => (gameCode ?? "").Trim().ToUpperInvariant();
}
