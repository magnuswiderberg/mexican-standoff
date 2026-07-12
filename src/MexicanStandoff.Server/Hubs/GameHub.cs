using MexicanStandoff.Server.Contracts;
using MexicanStandoff.Server.Games;
using Microsoft.AspNetCore.SignalR;

namespace MexicanStandoff.Server.Hubs;

/// <summary>
/// Thin SignalR endpoint: group membership + delegation to <see cref="GameService"/>.
/// Player identity travels as a secret token with each call (stored client-side),
/// so reconnects from any connection just work.
/// </summary>
public sealed class GameHub(GameService games) : Hub<IGameClient>
{
    public async Task<string> CreateGame(CreateGameSettings? settings = null)
    {
        var code = games.CreateGame(settings);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return code;
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
    /// </summary>
    public async Task<GameView> WatchAsMonitor(string gameCode)
    {
        var code = Normalize(gameCode);
        var view = games.GetView(code, token: null);
        Context.Items[MonitorCodeKey] = code;
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        await games.MonitorConnectedAsync(code, Context.ConnectionId);
        return view;
    }

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

    /// <summary>Host kicks with their token; the monitor kicks with none (like StartGame).</summary>
    public Task KickPlayer(string gameCode, string? playerToken, string targetPlayerId) =>
        games.KickAsync(Normalize(gameCode), playerToken, targetPlayerId, Context.ConnectionId);

    /// <summary>Dev-only, config-gated: the host (or the monitor, with no token) adds a bot seat.</summary>
    public Task AddBot(string gameCode, string? playerToken = null) =>
        games.AddBotAsync(Normalize(gameCode), playerToken);

    public Task StartGame(string gameCode) => games.StartAsync(Normalize(gameCode));

    public Task SubmitAction(string gameCode, string playerToken, ActionDto action) =>
        games.SubmitActionAsync(Normalize(gameCode), playerToken, action);

    public Task SubmitDuelSequence(string gameCode, string playerToken, ActionDto[] sequence) =>
        games.SubmitDuelSequenceAsync(Normalize(gameCode), playerToken, sequence);

    /// <summary>Resign: dodge out the current round, then leave the game (eliminated, gold lost).</summary>
    public Task Resign(string gameCode, string playerToken) =>
        games.ResignAsync(Normalize(gameCode), playerToken);

    public Task Rematch(string gameCode) => games.RematchAsync(Normalize(gameCode), Context.ConnectionId);

    /// <summary>Monitor button: ends the game for everyone (no token, like StartGame).</summary>
    public Task StopGame(string gameCode) => games.StopAsync(Normalize(gameCode));

    /// <summary>Context.Items key: the game code this connection watches as a monitor.</summary>
    private const string MonitorCodeKey = "monitorCode";

    private static string Normalize(string gameCode) => (gameCode ?? "").Trim().ToUpperInvariant();
}
