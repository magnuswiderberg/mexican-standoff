using MexicanStandoff.Engine;

namespace MexicanStandoff.Bots;

/// <summary>
/// Heuristic all-rounder: races to the chest when close to winning, hunts the
/// gold leader when armed, keeps its gun loaded, and dodges under pressure.
/// </summary>
public sealed class AdaptiveBot : IBot
{
    public string StrategyName => "adaptive";

    public PlayerAction ChooseAction(GameState state, string myId, Random rng)
    {
        var me = state.Player(myId);
        var opponents = BotHelpers.Opponents(state, myId);
        var armedOpponents = opponents.Count(p => p.Bullets > 0);
        var goldToWin = state.Parameters.GoldToWin;
        var oneGrabAway = goldToWin - state.Parameters.GoldPerChest;

        // One grab from winning: go for it, but hedge against being shot off the chest.
        if (me.Gold >= oneGrabAway && state.ChestCount > 0)
            return armedOpponents == 0 || rng.NextDouble() < 0.6
                ? BotHelpers.RandomChest(state, rng)
                : PlayerAction.Dodge.Instance;

        // Someone else is about to win: stop them if we can.
        var leader = opponents.Where(p => p.Gold >= oneGrabAway).OrderByDescending(p => p.Gold).FirstOrDefault();
        if (leader is not null && me.Bullets > 0)
            return new PlayerAction.Attack(leader.Id);

        // Under heavy threat: mostly dodge.
        if (armedOpponents >= Math.Max(1, opponents.Count / 2) && rng.NextDouble() < 0.5)
            return PlayerAction.Dodge.Instance;

        if (me.Bullets == 0)
            return PlayerAction.Load.Instance;

        // Otherwise mix it up: chest, shoot the gold leader, or top up the gun.
        var roll = rng.NextDouble();
        if (roll < 0.45 && state.ChestCount > 0)
            return BotHelpers.RandomChest(state, rng);
        if (roll < 0.8)
        {
            var maxGold = opponents.Max(p => p.Gold);
            var targets = opponents.Where(p => p.Gold == maxGold).ToList();
            return new PlayerAction.Attack(targets.Pick(rng).Id);
        }

        return BotHelpers.LoadOrDodge(state, myId);
    }
}
