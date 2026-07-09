namespace MexicanStandoff.Server.Games;

/// <summary>
/// The fixed avatar roster. Keys travel over the wire; the frontend maps them
/// to portrait images and accent colors (src/web/src/avatars.ts mirrors this
/// list). Ten characters for at most eight seats, so every player in a full
/// game still has a choice.
/// </summary>
public static class Avatars
{
    public static readonly IReadOnlyList<string> All =
    [
        "forastero",
        "viuda",
        "enterrador",
        "cascabel",
        "lobo",
        "tahura",
        "predicador",
        "cazadora",
        "gambusino",
        "contrabandista",
    ];

    /// <summary>The preferred avatar if it is valid and free, otherwise the first free one.</summary>
    public static string Assign(string? preferred, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        preferred = preferred?.Trim().ToLowerInvariant();
        if (preferred is not null && All.Contains(preferred) && !used.Contains(preferred))
            return preferred;
        // The roster is larger than MaxPlayers, so a free avatar always exists;
        // the modulo fallback keeps this safe if that invariant ever changes.
        return All.FirstOrDefault(a => !used.Contains(a)) ?? All[used.Count % All.Count];
    }
}
