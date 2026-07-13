using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace MexicanStandoff.Server.Games;

/// <summary>Session storage boundary — in-memory for MVP, Cosmos-backed later.</summary>
public interface IGameStore
{
    GameSession Create();
    GameSession? Get(string code);
}

public sealed class InMemoryGameStore(IOptions<GameOptions> options) : IGameStore
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();

    public GameSession Create()
    {
        Prune();
        while (true)
        {
            var session = new GameSession { Code = Codes.New(), MonitorToken = Tokens.New() };
            if (_sessions.TryAdd(session.Code, session))
                return session;
        }
    }

    public GameSession? Get(string code) => _sessions.GetValueOrDefault(code);

    private void Prune()
    {
        var cutoff = DateTimeOffset.UtcNow - options.Value.SessionLifetime;
        foreach (var (code, session) in _sessions)
        {
            if (session.LastActivity < cutoff)
                _sessions.TryRemove(code, out _);
        }
    }
}
