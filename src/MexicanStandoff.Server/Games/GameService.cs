using MexicanStandoff.Bots;
using MexicanStandoff.Engine;
using MexicanStandoff.Server.Contracts;
using MexicanStandoff.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace MexicanStandoff.Server.Games;

/// <summary>
/// Orchestrates game sessions: lobby, selection phases with timeout, resolution
/// via the engine, and broadcasts to the game's SignalR group. State mutation
/// happens under the session lock; broadcasts happen after it is released.
/// </summary>
public sealed class GameService(
    IGameStore store,
    IHubContext<GameHub, IGameClient> hub,
    IOptions<GameOptions> options,
    ILogger<GameService> logger)
{
    private static readonly GameParameters Parameters = GameParameters.Default;

    /// <summary>Strategies dealt round-robin to added bots (stateless, so instances are shared).</summary>
    private static readonly IBot[] BotStrategies =
        [new AdaptiveBot(), new AggressiveBot(), new ChestRusherBot(), new RandomBot(), new TurtleBot()];

    /// <summary>Lobby names for added bots; falls back to "Bot N" when exhausted.</summary>
    private static readonly string[] BotNames =
        ["Bot Sancho", "Bot Rosita", "Bot Dolores", "Bot Paco", "Bot Lupe", "Bot Chuy", "Bot Ramona"];

    public CreateGameResult CreateGame(CreateGameSettings? settings = null)
    {
        var session = store.Create();
        var seconds = settings?.SelectionTimerSeconds ?? options.Value.SelectionTimerSeconds;
        session.SelectionTimerSeconds = Math.Clamp(seconds, 0, 600);
        return new CreateGameResult(session.Code, session.MonitorToken);
    }

    public async Task<JoinResult> JoinAsync(string code, string name, string? preferredAvatar = null)
    {
        var session = Require(code);
        JoinResult result;
        lock (session.Lock)
        {
            ThrowIfStopped(session);
            if (session.Phase != GamePhase.Lobby)
                throw new HubException("The game has already started.");
            if (session.Players.Count >= Parameters.MaxPlayers)
                throw new HubException("The game is full.");

            name = (name ?? "").Trim();
            if (name.Length == 0)
                throw new HubException("A name is required.");
            if (name.Length > 20)
                name = name[..20];

            var player = new SessionPlayer
            {
                Id = $"p{++session.SeatsIssued}",
                Token = Tokens.New(),
                Name = name,
                Avatar = Avatars.Assign(preferredAvatar, session.Players.Select(p => p.Avatar)),
            };
            session.Players.Add(player);
            result = new JoinResult(player.Id, player.Token, LobbyOf(session));
        }

        await hub.Clients.Group(session.Code).LobbyUpdated(result.Lobby);
        return result;
    }

    /// <summary>A player gives up their seat while the game is still in the lobby.</summary>
    public async Task LeaveAsync(string code, string token)
    {
        var session = Require(code);
        LobbyView lobby;
        lock (session.Lock)
        {
            ThrowIfStopped(session);
            if (session.Phase != GamePhase.Lobby)
                throw new HubException("The game has already started.");
            var player = session.PlayerByToken(token) ?? throw new HubException("Unknown player token.");
            session.Players.Remove(player);
            lobby = LobbyOf(session);
        }

        await hub.Clients.Group(session.Code).LobbyUpdated(lobby);
    }

    /// <summary>
    /// Dev-only (see <see cref="BotOptions"/>): adds a bot seat to the lobby.
    /// Needs a control token (host seat or monitor), like the other game controls.
    /// </summary>
    public async Task AddBotAsync(string code, string? controlToken)
    {
        var session = Require(code);
        LobbyView lobby;
        lock (session.Lock)
        {
            ThrowIfStopped(session);
            if (!options.Value.Bots.Enabled)
                throw new HubException("Bots are not enabled on this server.");
            RequireController(session, controlToken, "add bots");
            if (session.Phase != GamePhase.Lobby)
                throw new HubException("The game has already started.");
            if (session.Players.Count >= Parameters.MaxPlayers)
                throw new HubException("The game is full.");

            var taken = session.Players.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var botCount = session.Players.Count(p => p.IsBot);
            var bot = new SessionPlayer
            {
                Id = $"p{++session.SeatsIssued}",
                Token = Tokens.New(),
                Name = BotNames.FirstOrDefault(n => !taken.Contains(n)) ?? $"Bot {session.SeatsIssued}",
                Avatar = Avatars.Assign(null, session.Players.Select(p => p.Avatar)),
                Brain = BotStrategies[botCount % BotStrategies.Length],
            };
            session.Players.Add(bot);
            lobby = LobbyOf(session);
        }

        await hub.Clients.Group(session.Code).LobbyUpdated(lobby);
    }

    /// <summary>
    /// Removes a player. Needs a control token — the host's seat token or the
    /// monitor token; the monitor can kick anyone, including the host, while the
    /// host cannot kick themselves. In the lobby the seat is freed; mid-game a
    /// kick is a forced resign (dodge out the current round, then eliminated) —
    /// for players who lost their connection or walked away.
    /// </summary>
    public async Task KickAsync(string code, string? controlToken, string targetPlayerId)
    {
        var session = Require(code);
        LobbyView? lobby = null;
        PlayerLockedView? locked = null;
        ResolveOutcome? outcome = null;
        lock (session.Lock)
        {
            ThrowIfStopped(session);
            if (session.Phase != GamePhase.Lobby && session.Phase != GamePhase.Selecting)
                throw new HubException("Kicking is only possible in the lobby or during a round.");
            if (RequireController(session, controlToken, "kick players") == Controller.Host
                && session.Host!.Id == targetPlayerId)
                throw new HubException("The host cannot kick themselves — leave or resign instead.");

            var target = session.Players.FirstOrDefault(p => p.Id == targetPlayerId)
                ?? throw new HubException("That player is not in the game.");

            if (session.Phase == GamePhase.Lobby)
            {
                session.Players.Remove(target);
                lobby = LobbyOf(session);
            }
            else
            {
                if (!session.State!.Player(target.Id).IsAlive)
                    throw new HubException("That player is already out.");
                if (!target.Resigned)
                    (locked, outcome) = ResignLocked(session, target);
            }
        }

        if (lobby is not null)
            await hub.Clients.Group(session.Code).LobbyUpdated(lobby);
        if (locked is not null)
            await hub.Clients.Group(session.Code).PlayerLocked(locked);
        await BroadcastResolvedAsync(session, outcome);
    }

    /// <summary>Starts the first round. The host's phone or the monitor screen runs this.</summary>
    public async Task StartAsync(string code, string? controlToken)
    {
        var session = Require(code);
        RoundStartedView view;
        int nonce;
        lock (session.Lock)
        {
            ThrowIfStopped(session);
            RequireController(session, controlToken, "start the game");
            if (session.Phase != GamePhase.Lobby)
                throw new HubException("The game has already started.");
            if (session.Players.Count < Parameters.MinPlayers)
                throw new HubException($"At least {Parameters.MinPlayers} players are needed.");

            foreach (var player in session.Players)
                player.Resigned = false;
            session.State = GameState.New(Parameters, session.Players.Select(p => (p.Id, p.Name)).ToArray());
            session.WinnerIds = null;
            session.WinReason = null;
            nonce = BeginSelectionLocked(session);
            view = new RoundStartedView(SnapshotOf(session), session.Deadline);
        }

        ScheduleTimeout(session, nonce);
        ScheduleAutoActions(session, nonce);
        await hub.Clients.Group(session.Code).RoundStarted(view);
    }

    public async Task SubmitActionAsync(string code, string token, ActionDto dto)
    {
        var session = Require(code);
        var action = dto.ToAction();
        PlayerLockedView locked;
        ResolveOutcome? outcome = null;
        lock (session.Lock)
        {
            var (player, state) = RequireSelectingPlayer(session, token);
            if (state.IsDuel)
                throw new HubException("The Final Duel needs a full action sequence.");
            if (ActionValidator.Validate(state, player.Id, action) is { } reason)
                throw new HubException($"Illegal action: {reason}.");

            session.PendingActions[player.Id] = action;
            locked = LockedViewOf(session, state, player.Id);
            if (session.PendingActions.Count == state.AliveCount)
                outcome = ResolveLocked(session);
        }

        await hub.Clients.Group(session.Code).PlayerLocked(locked);
        await BroadcastResolvedAsync(session, outcome);
    }

    public async Task SubmitDuelSequenceAsync(string code, string token, ActionDto[] dtos)
    {
        var session = Require(code);
        var sequence = dtos.Select(d => d.ToAction()).ToArray();
        PlayerLockedView locked;
        ResolveOutcome? outcome = null;
        lock (session.Lock)
        {
            var (player, state) = RequireSelectingPlayer(session, token);
            if (!state.IsDuel)
                throw new HubException("Not in the Final Duel — submit a single action.");
            if (DuelResolver.ValidateSequence(state, player.Id, sequence) is { } reason)
                throw new HubException($"Illegal sequence: {reason}.");

            session.PendingSequences[player.Id] = sequence;
            locked = LockedViewOf(session, state, player.Id);
            if (session.PendingSequences.Count == state.AliveCount)
                outcome = ResolveLocked(session);
        }

        await hub.Clients.Group(session.Code).PlayerLocked(locked);
        await BroadcastResolvedAsync(session, outcome);
    }

    /// <summary>
    /// The player gives up: their action this round becomes a Dodge (replacing
    /// anything already locked in) and the engine eliminates them when the round
    /// resolves — no looters, gold abandoned (docs/game-design.md, Resigning).
    /// </summary>
    public async Task ResignAsync(string code, string token)
    {
        var session = Require(code);
        PlayerLockedView? locked = null;
        ResolveOutcome? outcome = null;
        lock (session.Lock)
        {
            ThrowIfStopped(session);
            if (session.Phase != GamePhase.Selecting)
                throw new HubException("Resigning is only possible during a round.");
            var player = session.PlayerByToken(token) ?? throw new HubException("Unknown player token.");
            if (!session.State!.Player(player.Id).IsAlive)
                throw new HubException("Eliminated players cannot resign.");
            if (player.Resigned)
                return; // double-tap

            (locked, outcome) = ResignLocked(session, player);
        }

        if (locked is not null)
            await hub.Clients.Group(session.Code).PlayerLocked(locked);
        await BroadcastResolvedAsync(session, outcome);
    }

    /// <summary>
    /// Marks the player resigned and locks in their dodge-out, resolving the
    /// round if they were the last one pending. Used by a player's own resign
    /// and by a mid-game kick. Always returns a lock view — even when the player
    /// had already locked in, everyone should see the 🏳️ immediately (a kicker
    /// especially needs the feedback). Caller must hold the session lock.
    /// </summary>
    private static (PlayerLockedView? Locked, ResolveOutcome? Outcome) ResignLocked(GameSession session, SessionPlayer player)
    {
        var state = session.State!;

        player.Resigned = true;
        if (state.IsDuel)
            session.PendingSequences[player.Id] = DodgeSequence(state);
        else
            session.PendingActions[player.Id] = PlayerAction.Dodge.Instance;

        var locked = LockedViewOf(session, state, player.Id);
        var submitted = state.IsDuel ? session.PendingSequences.Count : session.PendingActions.Count;
        var outcome = submitted == state.AliveCount ? ResolveLocked(session) : null;
        return (locked, outcome);
    }

    /// <summary>Current lock progress + resigned flags. Caller must hold the session lock.</summary>
    private static PlayerLockedView LockedViewOf(GameSession session, GameState state, string playerId)
    {
        var lockedIds = state.IsDuel
            ? session.PendingSequences.Keys.ToArray()
            : session.PendingActions.Keys.ToArray();
        return new PlayerLockedView(
            playerId,
            lockedIds.Length,
            state.AliveCount,
            lockedIds,
            session.Players.Where(p => p.Resigned).Select(p => p.Id).ToArray());
    }

    /// <summary>
    /// After a game ends, a rematch returns everyone to the lobby (seats and bots
    /// intact) instead of force-starting a new game — humans get to leave (or be
    /// kicked) before the host starts the next one. While a monitor is watching,
    /// only the monitor can trigger it (phones hide the button, this backs that up).
    /// </summary>
    public async Task RematchAsync(string code, string? controlToken)
    {
        var session = Require(code);
        LobbyView lobby;
        lock (session.Lock)
        {
            ThrowIfStopped(session);
            if (session.Phase != GamePhase.GameOver)
                throw new HubException("A rematch is only possible after the game has ended.");
            if (RequireController(session, controlToken, "start the next game") != Controller.Monitor
                && session.MonitorConnections.Count > 0)
                throw new HubException("The next game starts from the monitor.");

            session.Phase = GamePhase.Lobby;
            session.State = null;
            session.WinnerIds = null;
            session.WinReason = null;
            session.Deadline = null;
            session.SelectionNonce++; // invalidate any running timer
            lobby = LobbyOf(session);
        }

        await hub.Clients.Group(session.Code).ReturnedToLobby(lobby);
    }

    /// <summary>
    /// A monitor page subscribed; broadcast when the first one appears. Registering
    /// takes the monitor token — otherwise any phone could claim the big screen's
    /// authority (and block the phones' own rematch button).
    /// </summary>
    public async Task MonitorConnectedAsync(string code, string monitorToken, string connectionId)
    {
        var session = Require(code);
        bool appeared;
        lock (session.Lock)
        {
            if (!session.IsMonitorToken(monitorToken))
                throw new HubException("This screen is not the monitor for that game.");
            appeared = session.MonitorConnections.Count == 0;
            session.MonitorConnections.Add(connectionId);
        }

        if (appeared)
            await hub.Clients.Group(session.Code).MonitorPresence(true);
    }

    /// <summary>A monitor connection dropped; broadcast when the last one is gone.</summary>
    public async Task MonitorDisconnectedAsync(string code, string connectionId)
    {
        var session = store.Get(code);
        if (session is null)
            return; // the game is already gone
        bool vanished;
        lock (session.Lock)
        {
            vanished = session.MonitorConnections.Remove(connectionId) && session.MonitorConnections.Count == 0;
        }

        if (vanished)
            await hub.Clients.Group(session.Code).MonitorPresence(false);
    }

    /// <summary>Stops the game for everyone (monitor button). Terminal — the session is dead.</summary>
    public async Task StopAsync(string code, string? controlToken)
    {
        var session = Require(code);
        lock (session.Lock)
        {
            RequireController(session, controlToken, "stop the game");
            if (session.Phase == GamePhase.Stopped)
                return; // two stop clicks racing — the first one already ended it
            session.Phase = GamePhase.Stopped;
            session.State = null;
            session.Deadline = null;
            session.SelectionNonce++; // invalidate any running timer
        }

        await hub.Clients.Group(session.Code).GameStopped();
    }

    /// <summary>Current full state for the monitor page or a reconnecting player.</summary>
    public GameView GetView(string code, string? token)
    {
        var session = Require(code);
        lock (session.Lock)
        {
            var player = token is null ? null : session.PlayerByToken(token);
            if (token is not null && player is null)
                throw new HubException("Unknown player token.");

            var hasSubmitted = player is not null
                && (session.PendingActions.ContainsKey(player.Id) || session.PendingSequences.ContainsKey(player.Id));

            return new GameView(
                session.Phase.ToString(),
                LobbyOf(session),
                session.State is null ? null : SnapshotOf(session),
                session.Deadline,
                player?.Id,
                hasSubmitted,
                session.WinnerIds,
                session.WinReason?.ToString(),
                session.MonitorConnections.Count > 0);
        }
    }

    // ---- internals ----------------------------------------------------------

    private sealed record ResolveOutcome(RoundResolvedView View, int? NextNonce);

    /// <summary>Who may run a game: the big screen that created it, or the host seat.</summary>
    private enum Controller
    {
        Host,
        Monitor,
    }

    /// <summary>
    /// Gate for the game controls (start, stop, kick, rematch, add bot): the caller
    /// must prove the monitor token or the host's seat token. Every device in the
    /// room knows the game code and can reach the hub, so the code proves nothing —
    /// without this a player could kick their rivals out and win by elimination.
    /// Caller must hold the session lock.
    /// </summary>
    private static Controller RequireController(GameSession session, string? controlToken, string what)
    {
        if (session.IsMonitorToken(controlToken))
            return Controller.Monitor;
        if (session.Host is { } host && Tokens.Equal(host.Token, controlToken))
            return Controller.Host;
        throw new HubException($"Only the host or the monitor can {what}.");
    }

    private GameSession Require(string code)
    {
        var session = store.Get(code) ?? throw new HubException($"No game with code '{code}'.");
        session.LastActivity = DateTimeOffset.UtcNow;
        return session;
    }

    /// <summary>Stopped is terminal — mutating calls get a clear error instead of a phase mismatch.</summary>
    private static void ThrowIfStopped(GameSession session)
    {
        if (session.Phase == GamePhase.Stopped)
            throw new HubException("The game has been stopped.");
    }

    private static (SessionPlayer Player, GameState State) RequireSelectingPlayer(GameSession session, string token)
    {
        if (session.Phase != GamePhase.Selecting)
            throw new HubException("The game is not waiting for actions.");
        var player = session.PlayerByToken(token) ?? throw new HubException("Unknown player token.");
        var state = session.State!;
        if (!state.Player(player.Id).IsAlive)
            throw new HubException("Eliminated players cannot act.");
        if (player.Resigned)
            throw new HubException("You have resigned.");
        return (player, state);
    }

    /// <summary>The all-Dodge duel sequence played for resigned, timed-out, and absent players.</summary>
    private static PlayerAction[] DodgeSequence(GameState state) =>
        Enumerable.Repeat((PlayerAction)PlayerAction.Dodge.Instance, state.Parameters.DuelSequenceLength).ToArray();

    /// <summary>Starts a new selection phase. Caller must hold the session lock.</summary>
    private static int BeginSelectionLocked(GameSession session)
    {
        session.Phase = GamePhase.Selecting;
        session.PendingActions.Clear();
        session.PendingSequences.Clear();
        session.SelectionNonce++;
        session.Deadline = session.SelectionTimerSeconds > 0
            ? DateTimeOffset.UtcNow.AddSeconds(session.SelectionTimerSeconds)
            : null;
        return session.SelectionNonce;
    }

    /// <summary>Resolves the round/sequence. Caller must hold the session lock.</summary>
    private static ResolveOutcome ResolveLocked(GameSession session)
    {
        var state = session.State!;
        var resigned = session.Players.Where(p => p.Resigned).Select(p => p.Id).ToHashSet();
        var result = state.IsDuel
            ? DuelResolver.Resolve(state, session.PendingSequences, resigned)
            : RoundResolver.Resolve(state, session.PendingActions, resigned);
        session.State = result.NewState;

        int? nextNonce = null;
        if (result.IsGameOver)
        {
            session.Phase = GamePhase.GameOver;
            session.Deadline = null;
            session.SelectionNonce++; // invalidate any running timer
            session.WinnerIds = result.WinnerIds;
            session.WinReason = result.WinReason;
        }
        else
        {
            nextNonce = BeginSelectionLocked(session);
        }

        var view = new RoundResolvedView(
            result.Reveal.Select(RevealStepDto.From).ToList(),
            SnapshotOf(session),
            session.Deadline,
            result.WinnerIds,
            result.WinReason?.ToString());
        return new ResolveOutcome(view, nextNonce);
    }

    private async Task BroadcastResolvedAsync(GameSession session, ResolveOutcome? outcome)
    {
        if (outcome is null)
            return;
        if (outcome.NextNonce is { } nonce)
        {
            ScheduleTimeout(session, nonce);
            ScheduleAutoActions(session, nonce);
        }
        await hub.Clients.Group(session.Code).RoundResolved(outcome.View);
    }

    private void ScheduleTimeout(GameSession session, int nonce)
    {
        if (session.SelectionTimerSeconds <= 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(session.SelectionTimerSeconds));
                await HandleTimeoutAsync(session, nonce);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Selection timeout handling failed for game {Code}", session.Code);
            }
        });
    }

    /// <summary>Deadline hit: absent players auto-play Dodge and the round resolves.</summary>
    private async Task HandleTimeoutAsync(GameSession session, int nonce)
    {
        ResolveOutcome? outcome;
        lock (session.Lock)
        {
            if (session.Phase != GamePhase.Selecting || session.SelectionNonce != nonce)
                return; // the round resolved (or the game moved on) before the deadline

            var state = session.State!;
            foreach (var player in state.AlivePlayers)
            {
                if (state.IsDuel)
                {
                    if (!session.PendingSequences.ContainsKey(player.Id))
                        session.PendingSequences[player.Id] = DodgeSequence(state);
                }
                else if (!session.PendingActions.ContainsKey(player.Id))
                {
                    session.PendingActions[player.Id] = PlayerAction.Dodge.Instance;
                }
            }

            outcome = ResolveLocked(session);
        }

        await BroadcastResolvedAsync(session, outcome);
    }

    /// <summary>Bots pick their actions shortly after a selection phase begins.</summary>
    private void ScheduleAutoActions(GameSession session, int nonce)
    {
        if (!options.Value.Bots.Enabled)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                if (options.Value.Bots.ThinkMilliseconds > 0)
                    await Task.Delay(options.Value.Bots.ThinkMilliseconds);
                await SubmitBotActionsAsync(session, nonce);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Bot action submission failed for game {Code}", session.Code);
            }
        });
    }

    private async Task SubmitBotActionsAsync(GameSession session, int nonce)
    {
        var lockedViews = new List<PlayerLockedView>();
        ResolveOutcome? outcome = null;
        lock (session.Lock)
        {
            if (session.Phase != GamePhase.Selecting || session.SelectionNonce != nonce)
                return; // the round resolved (or the game moved on) before the bots acted

            var state = session.State!;
            foreach (var bot in session.Players.Where(p => p.Brain is not null))
            {
                if (!state.Player(bot.Id).IsAlive)
                    continue;
                if (state.IsDuel)
                {
                    if (!session.PendingSequences.ContainsKey(bot.Id))
                    {
                        session.PendingSequences[bot.Id] = BotPlay.BuildDuelSequence(state, bot.Id, bot.Brain!, session.BotRng);
                        lockedViews.Add(LockedViewOf(session, state, bot.Id));
                    }
                }
                else if (!session.PendingActions.ContainsKey(bot.Id))
                {
                    session.PendingActions[bot.Id] = BotPlay.ChooseSafe(state, bot.Id, bot.Brain!, session.BotRng);
                    lockedViews.Add(LockedViewOf(session, state, bot.Id));
                }
            }

            var submitted = state.IsDuel ? session.PendingSequences.Count : session.PendingActions.Count;
            if (submitted == state.AliveCount)
                outcome = ResolveLocked(session);
        }

        foreach (var view in lockedViews)
            await hub.Clients.Group(session.Code).PlayerLocked(view);
        await BroadcastResolvedAsync(session, outcome);
    }

    private LobbyView LobbyOf(GameSession session) => new(
        session.Code,
        session.Players.Select(p => new LobbyPlayer(p.Id, p.Name, p.Avatar, p.IsBot)).ToList(),
        session.Players.Count >= Parameters.MinPlayers && session.Phase == GamePhase.Lobby,
        options.Value.Bots.Enabled);

    private static GameSnapshot SnapshotOf(GameSession session)
    {
        var state = session.State!;
        var avatars = session.Players.ToDictionary(p => p.Id, p => p.Avatar);
        var resigned = session.Players.Where(p => p.Resigned).Select(p => p.Id).ToHashSet();
        return new GameSnapshot(
            session.Code,
            session.Phase.ToString(),
            state.RoundNumber,
            state.IsDuel,
            state.DuelVolleysCompleted,
            state.SuddenDeath,
            state.ChestCount,
            state.Parameters.GoldToWin,
            state.Parameters.MaxBullets,
            state.Parameters.StartingHp,
            state.Parameters.DuelSequenceLength,
            state.Players
                .Select(p => new PlayerSnapshot(
                    p.Id, p.Name, avatars.GetValueOrDefault(p.Id, ""), p.Hp, p.Bullets, p.Gold, p.IsAlive,
                    resigned.Contains(p.Id)))
                .ToList());
    }
}
