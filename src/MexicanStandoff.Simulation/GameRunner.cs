using MexicanStandoff.Engine;
using MexicanStandoff.Simulation.Bots;

namespace MexicanStandoff.Simulation;

public sealed record GameOutcome(
    IReadOnlyList<string> WinnerStrategies,
    WinReason? Reason,
    int Rounds,
    bool TimedOut);

/// <summary>
/// Plays one full game with a bot per seat, orchestrating normal rounds and
/// Final Duel sequences the same way the server will.
/// </summary>
public static class GameRunner
{
    public static GameOutcome Play(GameParameters parameters, IReadOnlyList<IBot> bots, int seed, int maxRounds = 100)
    {
        var rng = new Random(seed);
        var seats = bots.Select((b, i) => (Id: $"p{i}", Bot: b)).ToArray();
        var botBySeat = seats.ToDictionary(s => s.Id, s => s.Bot);
        var state = GameState.New(parameters, seats.Select(s => (s.Id, s.Bot.StrategyName)).ToArray());

        while (true)
        {
            if (state.RoundNumber >= maxRounds)
                return new GameOutcome([], null, state.RoundNumber, TimedOut: true);

            var result = state.IsDuel
                ? DuelResolver.Resolve(state, state.AlivePlayers.ToDictionary(
                    p => p.Id,
                    p => BuildDuelSequence(state, p.Id, botBySeat[p.Id], rng)))
                : RoundResolver.Resolve(state, state.AlivePlayers.ToDictionary(
                    p => p.Id,
                    p => ChooseSafe(state, p.Id, botBySeat[p.Id], rng)));

            state = result.NewState;
            if (result.IsGameOver)
            {
                var winners = result.WinnerIds!.Select(id => botBySeat[id].StrategyName).ToList();
                return new GameOutcome(winners, result.WinReason, state.RoundNumber, TimedOut: false);
            }
        }
    }

    /// <summary>A bot returning an illegal action is a bot bug; fall back to a random legal one.</summary>
    private static PlayerAction ChooseSafe(GameState state, string playerId, IBot bot, Random rng)
    {
        var action = bot.ChooseAction(state, playerId, rng);
        return ActionValidator.Validate(state, playerId, action) is null
            ? action
            : BotHelpers.LegalActions(state, playerId).Pick(rng);
    }

    /// <summary>
    /// Programs a duel sequence by asking the bot step by step against a projected
    /// state (own bullets updated optimistically, sudden-death bullet included).
    /// </summary>
    private static IReadOnlyList<PlayerAction> BuildDuelSequence(GameState state, string playerId, IBot bot, Random rng)
    {
        var me = state.Player(playerId);
        var bullets = me.Bullets;
        if (state.SuddenDeath)
            bullets = Math.Min(bullets + 1, state.Parameters.MaxBullets);

        var sequence = new List<PlayerAction>();
        for (var step = 0; step < state.Parameters.DuelSequenceLength; step++)
        {
            var projected = state with
            {
                Players = state.Players
                    .Select(p => p.Id == playerId ? p with { Bullets = bullets } : p)
                    .ToArray(),
            };

            var action = bot.ChooseAction(projected, playerId, rng);
            if (ActionValidator.Validate(projected, playerId, action) is not null)
                action = BotHelpers.LoadOrDodge(projected, playerId);

            switch (action)
            {
                case PlayerAction.Attack:
                    bullets--;
                    break;
                case PlayerAction.Load:
                    bullets = Math.Min(bullets + 1, state.Parameters.MaxBullets);
                    break;
            }

            sequence.Add(action);
        }

        return sequence;
    }
}
