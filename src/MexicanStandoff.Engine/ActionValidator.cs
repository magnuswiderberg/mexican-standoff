namespace MexicanStandoff.Engine;

public static class ActionValidator
{
    /// <summary>Returns null when the action is legal, otherwise a human-readable reason.</summary>
    public static string? Validate(GameState state, string playerId, PlayerAction action)
    {
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return "unknown player";
        if (!player.IsAlive)
            return "player is eliminated";

        return action switch
        {
            PlayerAction.Dodge => null,

            PlayerAction.Load => player.Bullets >= state.Parameters.MaxBullets
                ? "gun is already full"
                : null,

            PlayerAction.Attack attack => ValidateAttack(state, player, attack),

            PlayerAction.OpenChest chest => chest.ChestIndex < 0 || chest.ChestIndex >= state.ChestCount
                ? $"no chest with index {chest.ChestIndex}"
                : null,

            _ => "unknown action",
        };
    }

    private static string? ValidateAttack(GameState state, PlayerState shooter, PlayerAction.Attack attack)
    {
        if (shooter.Bullets < 1)
            return "gun is empty";
        if (attack.TargetId == shooter.Id)
            return "cannot target yourself";

        var target = state.Players.FirstOrDefault(p => p.Id == attack.TargetId);
        if (target is null)
            return "unknown target";
        if (!target.IsAlive)
            return "target is already eliminated";

        return null;
    }
}
