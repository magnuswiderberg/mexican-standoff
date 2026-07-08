namespace MexicanStandoff.Server.Games;

/// <summary>
/// The fixed avatar palette. Keys travel over the wire; the frontend maps them
/// to actual colors. Eight keys — one per possible seat — so every player in a
/// full game gets a distinct color.
/// </summary>
public static class AvatarColors
{
    public static readonly IReadOnlyList<string> All =
        ["red", "orange", "yellow", "green", "teal", "blue", "purple", "pink"];

    /// <summary>The preferred color if it is valid and free, otherwise the first free one.</summary>
    public static string Assign(string? preferred, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        preferred = preferred?.Trim().ToLowerInvariant();
        if (preferred is not null && All.Contains(preferred) && !used.Contains(preferred))
            return preferred;
        // Palette size equals MaxPlayers, so a free color always exists; the
        // modulo fallback keeps this safe if that invariant ever changes.
        return All.FirstOrDefault(c => !used.Contains(c)) ?? All[used.Count % All.Count];
    }
}
