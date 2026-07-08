namespace MexicanStandoff.Engine;

/// <summary>
/// Final Duel: with exactly two players alive, both program a sequence of
/// actions which resolve step by step as normal simultaneous-volley rounds.
/// An action that is illegal when its step comes up fizzles into a Dodge.
/// Sequences without progress build toward sudden death (no chest, free bullet
/// at each sequence start).
/// </summary>
public static class DuelResolver
{
    public static RoundResult Resolve(
        GameState state,
        IReadOnlyDictionary<string, IReadOnlyList<PlayerAction>> sequences)
    {
        if (!state.IsDuel)
            throw new InvalidOperationException(
                $"Final Duel requires exactly 2 alive players, got {state.AliveCount}.");

        var duelists = state.AlivePlayers.Select(p => p.Id).ToList();
        foreach (var id in duelists)
        {
            if (!sequences.TryGetValue(id, out var sequence))
                throw new InvalidActionException(id, "no action sequence submitted");
            if (sequence.Count != state.Parameters.DuelSequenceLength)
                throw new InvalidActionException(
                    id, $"sequence must contain {state.Parameters.DuelSequenceLength} actions, got {sequence.Count}");
        }

        if (sequences.Keys.Any(id => !duelists.Contains(id)))
            throw new InvalidActionException(
                sequences.Keys.First(id => !duelists.Contains(id)), "player is not part of the duel");

        var reveal = new List<RevealStep>();
        var current = state;

        // Sudden death: both duelists get a free bullet at sequence start.
        if (current.SuddenDeath)
        {
            foreach (var id in duelists)
            {
                var p = current.Player(id);
                if (p.Bullets >= current.Parameters.MaxBullets)
                    continue;
                var loaded = p with { Bullets = p.Bullets + 1 };
                current = current with
                {
                    Players = current.Players.Select(x => x.Id == id ? loaded : x).ToArray(),
                };
                reveal.Add(new RevealStep.SuddenDeathBullet(id, loaded.Bullets));
            }
        }

        var goldBefore = duelists.ToDictionary(id => id, id => current.Player(id).Gold);

        for (var step = 0; step < state.Parameters.DuelSequenceLength; step++)
        {
            var stepActions = new Dictionary<string, PlayerAction>();
            foreach (var id in duelists)
            {
                var planned = sequences[id][step];
                if (ActionValidator.Validate(current, id, planned) is not null)
                {
                    reveal.Add(new RevealStep.ActionFizzled(id, planned));
                    planned = PlayerAction.Dodge.Instance;
                }

                stepActions[id] = planned;
            }

            var result = RoundResolver.Resolve(current, stepActions);
            reveal.AddRange(result.Reveal);
            current = result.NewState;

            if (result.IsGameOver)
            {
                return new RoundResult
                {
                    NewState = current,
                    Reveal = reveal,
                    WinnerIds = result.WinnerIds,
                    WinReason = result.WinReason,
                };
            }
        }

        // Sequence finished without a winner: stalemate accounting. Progress is
        // gold gained (an elimination would have ended the duel above).
        var progress = duelists.Any(id => current.Player(id).Gold > goldBefore[id]);
        var stalemates = progress ? 0 : current.DuelSequencesWithoutProgress + 1;
        current = current with
        {
            DuelSequencesWithoutProgress = stalemates,
            SuddenDeath = current.SuddenDeath || stalemates >= current.Parameters.DuelStalemateSequences,
        };

        return new RoundResult { NewState = current, Reveal = reveal };
    }

    /// <summary>
    /// Submission-time validation of a programmed sequence against the projected
    /// state, assuming the player's own actions all succeed (Load then Attack on
    /// an empty gun is legal; Attack first is not). Returns null when valid.
    /// </summary>
    public static string? ValidateSequence(GameState state, string playerId, IReadOnlyList<PlayerAction> sequence)
    {
        if (!state.IsDuel)
            return "not in a duel";

        var player = state.Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
            return "unknown player";
        if (!player.IsAlive)
            return "player is eliminated";
        if (sequence.Count != state.Parameters.DuelSequenceLength)
            return $"sequence must contain {state.Parameters.DuelSequenceLength} actions";

        var opponentId = state.AlivePlayers.Single(p => p.Id != playerId).Id;
        var bullets = player.Bullets;
        if (state.SuddenDeath)
            bullets = Math.Min(bullets + 1, state.Parameters.MaxBullets);

        for (var i = 0; i < sequence.Count; i++)
        {
            switch (sequence[i])
            {
                case PlayerAction.Dodge:
                    break;

                case PlayerAction.Load when bullets >= state.Parameters.MaxBullets:
                    return $"step {i + 1}: gun would already be full";
                case PlayerAction.Load:
                    bullets++;
                    break;

                case PlayerAction.Attack a when a.TargetId != opponentId:
                    return $"step {i + 1}: attack must target the opponent";
                case PlayerAction.Attack when bullets < 1:
                    return $"step {i + 1}: gun would be empty";
                case PlayerAction.Attack:
                    bullets--;
                    break;

                case PlayerAction.OpenChest c when c.ChestIndex < 0 || c.ChestIndex >= state.ChestCount:
                    return $"step {i + 1}: no chest with index {c.ChestIndex}";
                case PlayerAction.OpenChest:
                    break;

                default:
                    return $"step {i + 1}: unknown action";
            }
        }

        return null;
    }
}
