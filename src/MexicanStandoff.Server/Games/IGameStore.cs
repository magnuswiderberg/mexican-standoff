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
    // No lookalike characters (I/O/0/1) — codes are read aloud and typed on phones.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 4;

    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();

    public GameSession Create()
    {
        Prune();
        while (true)
        {
            var code = string.Concat(Enumerable.Range(0, CodeLength)
                .Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]));
            var session = new GameSession { Code = code };
            if (_sessions.TryAdd(code, session))
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
