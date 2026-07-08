namespace MexicanStandoff.Engine;

/// <summary>
/// Thrown when a submitted action breaks the rules. The server validates at
/// submission time with <see cref="ActionValidator"/>, so reaching this from the
/// resolver indicates a server-side bug rather than bad player input.
/// </summary>
public sealed class InvalidActionException(string playerId, string reason)
    : Exception($"Invalid action for player '{playerId}': {reason}")
{
    public string PlayerId { get; } = playerId;
    public string Reason { get; } = reason;
}
