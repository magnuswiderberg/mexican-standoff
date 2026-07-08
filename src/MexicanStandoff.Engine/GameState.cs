namespace MexicanStandoff.Engine;

/// <summary>
/// Immutable snapshot of a game. Player order is seat order and is used wherever
/// deterministic iteration matters.
/// </summary>
public sealed record GameState
{
    public required GameParameters Parameters { get; init; }
    public required IReadOnlyList<PlayerState> Players { get; init; }
    public int RoundNumber { get; init; }

    /// <summary>Completed Final Duel sequences without an elimination or gold gained.</summary>
    public int DuelSequencesWithoutProgress { get; init; }

    /// <summary>Duel stalemate guard active: no chest, free bullet at each sequence start.</summary>
    public bool SuddenDeath { get; init; }

    public IEnumerable<PlayerState> AlivePlayers => Players.Where(p => p.IsAlive);
    public int AliveCount => Players.Count(p => p.IsAlive);
    public bool IsDuel => AliveCount == 2;
    public int ChestCount => SuddenDeath ? 0 : Parameters.ChestCountFor(AliveCount);

    public PlayerState Player(string id) =>
        Players.FirstOrDefault(p => p.Id == id)
        ?? throw new ArgumentException($"Unknown player '{id}'.", nameof(id));

    public static GameState New(GameParameters parameters, IReadOnlyList<(string Id, string Name)> players)
    {
        if (players.Count < parameters.MinPlayers || players.Count > parameters.MaxPlayers)
            throw new ArgumentException(
                $"Player count must be between {parameters.MinPlayers} and {parameters.MaxPlayers}, got {players.Count}.",
                nameof(players));
        if (players.Select(p => p.Id).Distinct().Count() != players.Count)
            throw new ArgumentException("Player ids must be unique.", nameof(players));

        return new GameState
        {
            Parameters = parameters,
            Players = players
                .Select(p => new PlayerState
                {
                    Id = p.Id,
                    Name = p.Name,
                    Hp = parameters.StartingHp,
                    Bullets = Math.Min(parameters.StartingBullets, parameters.MaxBullets),
                })
                .ToArray(),
        };
    }
}
