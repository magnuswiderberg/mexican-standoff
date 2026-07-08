using MexicanStandoff.Engine;

namespace MexicanStandoff.Simulation.Bots;

/// <summary>
/// A bot strategy. Bots choose one action per round; the runner reuses the same
/// method step-by-step (against a projected state) to program duel sequences.
/// </summary>
public interface IBot
{
    string StrategyName { get; }

    PlayerAction ChooseAction(GameState state, string myId, Random rng);
}
