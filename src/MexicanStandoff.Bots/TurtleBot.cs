using MexicanStandoff.Engine;

namespace MexicanStandoff.Bots;

/// <summary>Dodges whenever anyone is armed; loads and chests only when it is safe.</summary>
public sealed class TurtleBot : IBot
{
    public string StrategyName => "turtle";

    public PlayerAction ChooseAction(GameState state, string myId, Random rng)
    {
        var me = state.Player(myId);
        var opponents = BotHelpers.Opponents(state, myId);

        if (opponents.Any(p => p.Bullets > 0))
            return PlayerAction.Dodge.Instance;

        // Nobody armed: a turtle banks durability to the ceiling before anything else.
        if (BotHelpers.CanHeal(state, myId))
            return PlayerAction.Heal.Instance;

        if (me.Bullets < state.Parameters.MaxBullets)
            return PlayerAction.Load.Instance;

        if (state.ChestCount > 0)
            return BotHelpers.RandomChest(state, rng);

        // Gun full, nobody armed, no chest: pick off the weakest.
        var minHp = opponents.Min(p => p.Hp);
        return new PlayerAction.Attack(opponents.Where(p => p.Hp == minHp).ToList().Pick(rng).Id);
    }
}
