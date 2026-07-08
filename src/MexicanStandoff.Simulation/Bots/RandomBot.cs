using MexicanStandoff.Engine;

namespace MexicanStandoff.Simulation.Bots;

/// <summary>Uniformly random among legal actions. The balance baseline.</summary>
public sealed class RandomBot : IBot
{
    public string StrategyName => "random";

    public PlayerAction ChooseAction(GameState state, string myId, Random rng) =>
        BotHelpers.LegalActions(state, myId).Pick(rng);
}
