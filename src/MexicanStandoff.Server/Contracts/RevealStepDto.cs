using MexicanStandoff.Engine;

namespace MexicanStandoff.Server.Contracts;

public sealed record RevealActionDto(string PlayerId, ActionDto Action);

/// <summary>
/// Flat wire format for one reveal step; <see cref="Type"/> decides which fields
/// are set. Devices animate these in order, in lockstep.
/// </summary>
public sealed record RevealStepDto(
    string Type,
    IReadOnlyList<RevealActionDto>? Actions = null,
    string? PlayerId = null,
    string? ShooterId = null,
    string? TargetId = null,
    bool? Hit = null,
    ActionDto? Action = null,
    int? BulletsNow = null,
    int? ChestIndex = null,
    IReadOnlyList<string>? ContenderIds = null,
    string? ChestWinnerId = null,
    int? GoldGained = null,
    IReadOnlyList<string>? LooterIds = null,
    int? GoldPerLooter = null,
    int? GoldLost = null,
    IReadOnlyList<string>? WinnerIds = null,
    string? WinReason = null)
{
    public static RevealStepDto From(RevealStep step) => step switch
    {
        RevealStep.ActionsRevealed s => new RevealStepDto("actionsRevealed",
            Actions: s.Actions.Select(a => new RevealActionDto(a.PlayerId, ActionDto.From(a.Action))).ToList()),

        RevealStep.ShotFired s => new RevealStepDto("shotFired",
            ShooterId: s.ShooterId, TargetId: s.TargetId, Hit: s.Hit),

        RevealStep.ActionCancelled s => new RevealStepDto("actionCancelled",
            PlayerId: s.PlayerId, Action: ActionDto.From(s.Action)),

        RevealStep.GunLoaded s => new RevealStepDto("gunLoaded",
            PlayerId: s.PlayerId, BulletsNow: s.BulletsNow),

        RevealStep.SuddenDeathBullet s => new RevealStepDto("suddenDeathBullet",
            PlayerId: s.PlayerId, BulletsNow: s.BulletsNow),

        RevealStep.ChestResolved s => new RevealStepDto("chestResolved",
            ChestIndex: s.ChestIndex, ContenderIds: s.ContenderIds, ChestWinnerId: s.WinnerId, GoldGained: s.GoldGained),

        RevealStep.PlayerEliminated s => new RevealStepDto("playerEliminated",
            PlayerId: s.PlayerId, LooterIds: s.LooterIds, GoldPerLooter: s.GoldPerLooter, GoldLost: s.GoldLost),

        RevealStep.PlayerResigned s => new RevealStepDto("playerResigned",
            PlayerId: s.PlayerId, GoldLost: s.GoldLost),

        RevealStep.ActionFizzled s => new RevealStepDto("actionFizzled",
            PlayerId: s.PlayerId, Action: ActionDto.From(s.Original)),

        RevealStep.GameEnded s => new RevealStepDto("gameEnded",
            WinnerIds: s.WinnerIds, WinReason: s.Reason.ToString()),

        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };
}
