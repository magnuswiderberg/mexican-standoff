using MexicanStandoff.Bots;
using MexicanStandoff.Engine;

namespace MexicanStandoff.Simulation;

public sealed record GameOutcome(
    IReadOnlyList<string> WinnerStrategies,
    WinReason? Reason,
    int Rounds,
    bool TimedOut,
    int GoldLostToRounding,
    int Heals);

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

        // Gold lost to the loot split rounding down (excludes abandoned resigner gold).
        var goldLostToRounding = 0;
        var heals = 0;

        while (true)
        {
            if (state.RoundNumber >= maxRounds)
                return new GameOutcome([], null, state.RoundNumber, TimedOut: true, goldLostToRounding, heals);

            var result = state.IsDuel
                ? DuelResolver.Resolve(state, state.AlivePlayers.ToDictionary(
                    p => p.Id,
                    p => BotPlay.BuildDuelSequence(state, p.Id, botBySeat[p.Id], rng)))
                : RoundResolver.Resolve(state, state.AlivePlayers.ToDictionary(
                    p => p.Id,
                    p => BotPlay.ChooseSafe(state, p.Id, botBySeat[p.Id], rng)));

            goldLostToRounding += result.Reveal.OfType<RevealStep.PlayerEliminated>().Sum(e => e.GoldLost);
            heals += result.Reveal.OfType<RevealStep.PlayerHealed>().Count();

            state = result.NewState;
            if (result.IsGameOver)
            {
                var winners = result.WinnerIds!.Select(id => botBySeat[id].StrategyName).ToList();
                return new GameOutcome(winners, result.WinReason, state.RoundNumber, TimedOut: false, goldLostToRounding, heals);
            }
        }
    }

}
