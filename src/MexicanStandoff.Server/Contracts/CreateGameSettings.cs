namespace MexicanStandoff.Server.Contracts;

/// <summary>
/// Per-game settings chosen when hosting (the home page's expandable panel).
/// Every field is optional — null falls back to the server's configured default.
/// </summary>
public sealed record CreateGameSettings(int? SelectionTimerSeconds = null, bool? Healing = null);
