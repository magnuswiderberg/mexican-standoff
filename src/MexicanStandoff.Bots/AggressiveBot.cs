using MexicanStandoff.Engine;

namespace MexicanStandoff.Bots;

/// <summary>Never touches a chest: loads, then shoots the biggest threat. Wins only by elimination.</summary>
public sealed class AggressiveBot : IBot
{
    public string StrategyName => "aggressive";

    public PlayerAction ChooseAction(GameState state, string myId, Random rng)
    {
        var me = state.Player(myId);
        if (me.Bullets > 0)
        {
            // Shoot the opponent closest to winning: most gold, then most bullets.
            var opponents = BotHelpers.Opponents(state, myId);
            var maxGold = opponents.Max(p => p.Gold);
            var maxBullets = opponents.Where(p => p.Gold == maxGold).Max(p => p.Bullets);
            var targets = opponents.Where(p => p.Gold == maxGold && p.Bullets == maxBullets).ToList();
            return new PlayerAction.Attack(targets.Pick(rng).Id);
        }

        return BotHelpers.LoadOrDodge(state, myId);
    }
}
