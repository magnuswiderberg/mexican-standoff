using MexicanStandoff.Engine;

namespace MexicanStandoff.Bots;

/// <summary>Goes for a chest every round; arms itself only when no chest exists (sudden death).</summary>
public sealed class ChestRusherBot : IBot
{
    public string StrategyName => "chest-rusher";

    public PlayerAction ChooseAction(GameState state, string myId, Random rng)
    {
        if (state.ChestCount > 0)
            return BotHelpers.RandomChest(state, rng);

        var me = state.Player(myId);
        if (me.Bullets > 0)
            return new PlayerAction.Attack(BotHelpers.Opponents(state, myId).Pick(rng).Id);

        return BotHelpers.LoadOrDodge(state, myId);
    }
}
