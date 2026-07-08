namespace MexicanStandoff.Engine;

/// <summary>
/// An action card a player can play in a round. Closed hierarchy — the resolver
/// exhaustively handles every subtype.
/// </summary>
public abstract record PlayerAction
{
    private PlayerAction() { }

    /// <summary>Cannot be hit by shots this round.</summary>
    public sealed record Dodge : PlayerAction
    {
        public static readonly Dodge Instance = new();
    }

    /// <summary>Fire one bullet at a player. Always consumes the bullet, even against a dodge.</summary>
    public sealed record Attack(string TargetId) : PlayerAction;

    /// <summary>Add one bullet to the gun.</summary>
    public sealed record Load : PlayerAction
    {
        public static readonly Load Instance = new();
    }

    /// <summary>Attempt to take a gold bar from the given chest.</summary>
    public sealed record OpenChest(int ChestIndex) : PlayerAction;
}
