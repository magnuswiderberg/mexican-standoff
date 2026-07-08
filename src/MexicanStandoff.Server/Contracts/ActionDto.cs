using MexicanStandoff.Engine;
using Microsoft.AspNetCore.SignalR;

namespace MexicanStandoff.Server.Contracts;

/// <summary>Wire format for an action card: dodge | load | attack | chest.</summary>
public sealed record ActionDto(string Type, string? TargetId = null, int? ChestIndex = null)
{
    public PlayerAction ToAction() => Type?.ToLowerInvariant() switch
    {
        "dodge" => PlayerAction.Dodge.Instance,
        "load" => PlayerAction.Load.Instance,
        "attack" when !string.IsNullOrEmpty(TargetId) => new PlayerAction.Attack(TargetId),
        "chest" when ChestIndex is not null => new PlayerAction.OpenChest(ChestIndex.Value),
        _ => throw new HubException($"Malformed action '{Type}'."),
    };

    public static ActionDto From(PlayerAction action) => action switch
    {
        PlayerAction.Dodge => new ActionDto("dodge"),
        PlayerAction.Load => new ActionDto("load"),
        PlayerAction.Attack a => new ActionDto("attack", TargetId: a.TargetId),
        PlayerAction.OpenChest c => new ActionDto("chest", ChestIndex: c.ChestIndex),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };
}
