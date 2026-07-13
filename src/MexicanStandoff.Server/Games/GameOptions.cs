namespace MexicanStandoff.Server.Games;

public sealed class GameOptions
{
    /// <summary>Seconds players get to pick an action; 0 or less disables the timer.</summary>
    public int SelectionTimerSeconds { get; set; } = 30;

    /// <summary>Idle sessions older than this are pruned.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// How long a screen waiting to become the board keeps its place in the queue.
    /// Both the waiting screen and the host's prompt run this clock down, so it also
    /// decides how long they stare at a request nobody answered.
    /// </summary>
    public TimeSpan MonitorRequestLifetime { get; set; } = TimeSpan.FromMinutes(2);

    public BotOptions Bots { get; set; } = new();
}

/// <summary>Dev-only lobby bots. Enabled in Development config; keep off in production.</summary>
public sealed class BotOptions
{
    /// <summary>Lets the host add bot seats in the lobby.</summary>
    public bool Enabled { get; set; }

    /// <summary>Fake think time before bots lock in, so rounds don't resolve jarringly fast.</summary>
    public int ThinkMilliseconds { get; set; } = 1200;
}
