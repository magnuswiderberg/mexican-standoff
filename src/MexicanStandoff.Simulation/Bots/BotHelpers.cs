using MexicanStandoff.Engine;

namespace MexicanStandoff.Simulation.Bots;

internal static class BotHelpers
{
    /// <summary>Every legal action for the player in the given state.</summary>
    public static List<PlayerAction> LegalActions(GameState state, string myId)
    {
        var candidates = new List<PlayerAction> { PlayerAction.Dodge.Instance, PlayerAction.Load.Instance };
        candidates.AddRange(state.AlivePlayers
            .Where(p => p.Id != myId)
            .Select(p => (PlayerAction)new PlayerAction.Attack(p.Id)));
        candidates.AddRange(Enumerable.Range(0, state.ChestCount)
            .Select(i => (PlayerAction)new PlayerAction.OpenChest(i)));

        return candidates.Where(a => ActionValidator.Validate(state, myId, a) is null).ToList();
    }

    public static List<PlayerState> Opponents(GameState state, string myId) =>
        state.AlivePlayers.Where(p => p.Id != myId).ToList();

    public static PlayerAction RandomChest(GameState state, Random rng) =>
        new PlayerAction.OpenChest(rng.Next(state.ChestCount));

    public static T Pick<T>(this IReadOnlyList<T> items, Random rng) => items[rng.Next(items.Count)];

    /// <summary>Fallback when a strategy's preferred action is illegal: load if possible, else dodge.</summary>
    public static PlayerAction LoadOrDodge(GameState state, string myId) =>
        ActionValidator.Validate(state, myId, PlayerAction.Load.Instance) is null
            ? PlayerAction.Load.Instance
            : PlayerAction.Dodge.Instance;
}
