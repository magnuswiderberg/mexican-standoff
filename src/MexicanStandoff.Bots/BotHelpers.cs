using MexicanStandoff.Engine;

namespace MexicanStandoff.Bots;

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
        // Heal is a multiplayer-round tactic only — the Final Duel is a pure
        // programmed shootout, so it is never offered inside a duel sequence.
        if (!state.IsDuel)
            candidates.Add(PlayerAction.Heal.Instance);

        return candidates.Where(a => ActionValidator.Validate(state, myId, a) is null).ToList();
    }

    public static List<PlayerState> Opponents(GameState state, string myId) =>
        state.AlivePlayers.Where(p => p.Id != myId).ToList();

    public static PlayerAction RandomChest(GameState state, Random rng) =>
        new PlayerAction.OpenChest(rng.Next(state.ChestCount));

    public static T Pick<T>(this IReadOnlyList<T> items, Random rng) => items[rng.Next(items.Count)];

    /// <summary>
    /// Whether Heal is legal right now (enabled, below max HP, gold to spare) and
    /// applicable — never inside a Final Duel, where sequences can't carry a Heal.
    /// </summary>
    public static bool CanHeal(GameState state, string myId) =>
        !state.IsDuel && ActionValidator.Validate(state, myId, PlayerAction.Heal.Instance) is null;

    /// <summary>
    /// Whether healing is worth it this round: legal and nobody can shoot it off
    /// (a hit cancels the heal, so healing under threat just burns the turn).
    /// </summary>
    public static bool ShouldHeal(GameState state, string myId) =>
        CanHeal(state, myId) && Opponents(state, myId).All(p => p.Bullets == 0);

    /// <summary>Fallback when a strategy's preferred action is illegal: load if possible, else dodge.</summary>
    public static PlayerAction LoadOrDodge(GameState state, string myId) =>
        ActionValidator.Validate(state, myId, PlayerAction.Load.Instance) is null
            ? PlayerAction.Load.Instance
            : PlayerAction.Dodge.Instance;
}
