namespace MexicanStandoff.Engine.Tests;

/// <summary>Builders and shorthands for arranging game states in tests.</summary>
internal static class TestGame
{
    /// <summary>Builds a state directly (bypassing GameState.New) so tests can set any HP/bullets/gold.</summary>
    public static GameState State(params (string Id, int Hp, int Bullets, int Gold)[] players) =>
        State(GameParameters.Default, players);

    public static GameState State(GameParameters parameters, params (string Id, int Hp, int Bullets, int Gold)[] players) =>
        new()
        {
            Parameters = parameters,
            Players = players
                .Select(p => new PlayerState { Id = p.Id, Name = p.Id, Hp = p.Hp, Bullets = p.Bullets, Gold = p.Gold })
                .ToArray(),
        };

    public static PlayerAction Dodge => PlayerAction.Dodge.Instance;
    public static PlayerAction Load => PlayerAction.Load.Instance;
    public static PlayerAction Attack(string targetId) => new PlayerAction.Attack(targetId);
    public static PlayerAction Chest(int index = 0) => new PlayerAction.OpenChest(index);

    public static RoundResult Resolve(GameState state, params (string PlayerId, PlayerAction Action)[] actions) =>
        RoundResolver.Resolve(state, actions.ToDictionary(a => a.PlayerId, a => a.Action));
}
