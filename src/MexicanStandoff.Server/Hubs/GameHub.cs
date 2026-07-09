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
    public async Task<string> CreateGame()
    {
        var code = games.CreateGame();
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return code;
    }

    public async Task<JoinResult> JoinGame(string gameCode, string playerName, string? preferredAvatar = null)
    {
        var code = Normalize(gameCode);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return await games.JoinAsync(code, playerName, preferredAvatar);
    }

    /// <summary>Monitor page: subscribe to a game without being a player.</summary>
    public async Task<GameView> WatchGame(string gameCode)
    {
        var code = Normalize(gameCode);
        var view = games.GetView(code, token: null);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return view;
    }

    /// <summary>Rebind a (new) connection to an existing seat and get the current state.</summary>
    public async Task<GameView> Reconnect(string gameCode, string playerToken)
    {
        var code = Normalize(gameCode);
        var view = games.GetView(code, playerToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return view;
    }

    public Task StartGame(string gameCode) => games.StartAsync(Normalize(gameCode));

    public Task SubmitAction(string gameCode, string playerToken, ActionDto action) =>
        games.SubmitActionAsync(Normalize(gameCode), playerToken, action);

    public Task SubmitDuelSequence(string gameCode, string playerToken, ActionDto[] sequence) =>
        games.SubmitDuelSequenceAsync(Normalize(gameCode), playerToken, sequence);

    public Task Rematch(string gameCode) => games.RematchAsync(Normalize(gameCode));

    private static string Normalize(string gameCode) => (gameCode ?? "").Trim().ToUpperInvariant();
}
