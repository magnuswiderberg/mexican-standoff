namespace MexicanStandoff.Server.Games;

public sealed class GameOptions
{
    /// <summary>Seconds players get to pick an action; 0 or less disables the timer.</summary>
    public int SelectionTimerSeconds { get; set; } = 30;

    /// <summary>Idle sessions older than this are pruned.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(2);
}
