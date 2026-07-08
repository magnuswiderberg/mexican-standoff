namespace MexicanStandoff.Engine;

public sealed record PlayerState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Hp { get; init; }
    public int Bullets { get; init; }
    public int Gold { get; init; }

    public bool IsAlive => Hp > 0;
}
