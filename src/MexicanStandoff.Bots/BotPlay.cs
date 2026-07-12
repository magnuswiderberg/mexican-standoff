using MexicanStandoff.Engine;

namespace MexicanStandoff.Bots;

/// <summary>
/// Drives an <see cref="IBot"/> against live game state, shared by the simulation
/// harness and the server's dev lobby bots: guards round actions against bot bugs
/// and programs Final Duel sequences step by step.
/// </summary>
public static class BotPlay
{
    /// <summary>A bot returning an illegal action is a bot bug; fall back to a random legal one.</summary>
    public static PlayerAction ChooseSafe(GameState state, string playerId, IBot bot, Random rng)
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
    public static IReadOnlyList<PlayerAction> BuildDuelSequence(GameState state, string playerId, IBot bot, Random rng)
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

        // The engine has the final say; all-Dodge is always a legal fallback.
        return DuelResolver.ValidateSequence(state, playerId, sequence) is null
            ? sequence
            : Enumerable
                .Repeat((PlayerAction)PlayerAction.Dodge.Instance, state.Parameters.DuelSequenceLength)
                .ToList();
    }
}
