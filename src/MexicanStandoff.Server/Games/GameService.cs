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

    public string CreateGame() => store.Create().Code;

    public async Task<JoinResult> JoinAsync(string code, string name, string? preferredColor = null)
    {
        var session = Require(code);
        JoinResult result;
        lock (session.Lock)
        {
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
                Id = $"p{session.Players.Count + 1}",
                Token = Guid.NewGuid().ToString("N"),
                Name = name,
                Color = AvatarColors.Assign(preferredColor, session.Players.Select(p => p.Color)),
            };
            session.Players.Add(player);
            result = new JoinResult(player.Id, player.Token, LobbyOf(session));
        }

        await hub.Clients.Group(session.Code).LobbyUpdated(result.Lobby);
        return result;
    }

    public async Task StartAsync(string code)
    {
        var session = Require(code);
        RoundStartedView view;
        int nonce;
        lock (session.Lock)
        {
            if (session.Phase != GamePhase.Lobby)
                throw new HubException("The game has already started.");
            if (session.Players.Count < Parameters.MinPlayers)
                throw new HubException($"At least {Parameters.MinPlayers} players are needed.");

            session.State = GameState.New(Parameters, session.Players.Select(p => (p.Id, p.Name)).ToArray());
            session.WinnerIds = null;
            session.WinReason = null;
            nonce = BeginSelectionLocked(session);
            view = new RoundStartedView(SnapshotOf(session), session.Deadline);
        }

        ScheduleTimeout(session, nonce);
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
            locked = new PlayerLockedView(player.Id, session.PendingActions.Count, state.AliveCount);
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
            locked = new PlayerLockedView(player.Id, session.PendingSequences.Count, state.AliveCount);
            if (session.PendingSequences.Count == state.AliveCount)
                outcome = ResolveLocked(session);
        }

        await hub.Clients.Group(session.Code).PlayerLocked(locked);
        await BroadcastResolvedAsync(session, outcome);
    }

    public async Task RematchAsync(string code)
    {
        var session = Require(code);
        RoundStartedView view;
        int nonce;
        lock (session.Lock)
        {
            if (session.Phase != GamePhase.GameOver)
                throw new HubException("A rematch is only possible after the game has ended.");

            session.State = GameState.New(Parameters, session.Players.Select(p => (p.Id, p.Name)).ToArray());
            session.WinnerIds = null;
            session.WinReason = null;
            nonce = BeginSelectionLocked(session);
            view = new RoundStartedView(SnapshotOf(session), session.Deadline);
        }

        ScheduleTimeout(session, nonce);
        await hub.Clients.Group(session.Code).RoundStarted(view);
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
                session.WinReason?.ToString());
        }
    }

    // ---- internals ----------------------------------------------------------

    private sealed record ResolveOutcome(RoundResolvedView View, int? NextNonce);

    private GameSession Require(string code)
    {
        var session = store.Get(code) ?? throw new HubException($"No game with code '{code}'.");
        session.LastActivity = DateTimeOffset.UtcNow;
        return session;
    }

    private static (SessionPlayer Player, GameState State) RequireSelectingPlayer(GameSession session, string token)
    {
        if (session.Phase != GamePhase.Selecting)
            throw new HubException("The game is not waiting for actions.");
        var player = session.PlayerByToken(token) ?? throw new HubException("Unknown player token.");
        var state = session.State!;
        if (!state.Player(player.Id).IsAlive)
            throw new HubException("Eliminated players cannot act.");
        return (player, state);
    }

    /// <summary>Starts a new selection phase. Caller must hold the session lock.</summary>
    private int BeginSelectionLocked(GameSession session)
    {
        session.Phase = GamePhase.Selecting;
        session.PendingActions.Clear();
        session.PendingSequences.Clear();
        session.SelectionNonce++;
        session.Deadline = options.Value.SelectionTimerSeconds > 0
            ? DateTimeOffset.UtcNow.AddSeconds(options.Value.SelectionTimerSeconds)
            : null;
        return session.SelectionNonce;
    }

    /// <summary>Resolves the round/sequence. Caller must hold the session lock.</summary>
    private ResolveOutcome ResolveLocked(GameSession session)
    {
        var state = session.State!;
        var result = state.IsDuel
            ? DuelResolver.Resolve(state, session.PendingSequences)
            : RoundResolver.Resolve(state, session.PendingActions);
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
            ScheduleTimeout(session, nonce);
        await hub.Clients.Group(session.Code).RoundResolved(outcome.View);
    }

    private void ScheduleTimeout(GameSession session, int nonce)
    {
        if (options.Value.SelectionTimerSeconds <= 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.SelectionTimerSeconds));
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
                        session.PendingSequences[player.Id] = Enumerable
                            .Repeat((PlayerAction)PlayerAction.Dodge.Instance, state.Parameters.DuelSequenceLength)
                            .ToArray();
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

    private static LobbyView LobbyOf(GameSession session) => new(
        session.Code,
        session.Players.Select(p => new LobbyPlayer(p.Id, p.Name, p.Color)).ToList(),
        session.Players.Count >= Parameters.MinPlayers && session.Phase == GamePhase.Lobby);

    private static GameSnapshot SnapshotOf(GameSession session)
    {
        var state = session.State!;
        var colors = session.Players.ToDictionary(p => p.Id, p => p.Color);
        return new GameSnapshot(
            session.Code,
            session.Phase.ToString(),
            state.RoundNumber,
            state.IsDuel,
            state.SuddenDeath,
            state.ChestCount,
            state.Parameters.GoldToWin,
            state.Parameters.MaxBullets,
            state.Parameters.StartingHp,
            state.Parameters.DuelSequenceLength,
            state.Players
                .Select(p => new PlayerSnapshot(p.Id, p.Name, colors.GetValueOrDefault(p.Id, ""), p.Hp, p.Bullets, p.Gold, p.IsAlive))
                .ToList());
    }
}
