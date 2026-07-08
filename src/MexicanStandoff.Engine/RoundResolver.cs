namespace MexicanStandoff.Engine;

/// <summary>
/// Resolves one round using the simultaneous-volley model (docs/game-design.md):
/// dodges → all attacks fire together → being hit cancels Load/Chest → loads →
/// chests (strictly alone) → eliminations and loot → win check.
/// </summary>
public static class RoundResolver
{
    public static RoundResult Resolve(GameState state, IReadOnlyDictionary<string, PlayerAction> actions)
    {
        ValidateSubmission(state, actions);

        // Seat-ordered view of (alive player, action) for deterministic iteration.
        var seated = state.Players
            .Where(p => p.IsAlive)
            .Select(p => (Player: p, Action: actions[p.Id]))
            .ToList();

        var reveal = new List<RevealStep>
        {
            new RevealStep.ActionsRevealed(seated.Select(s => (s.Player.Id, s.Action)).ToList()),
        };

        var players = state.Players.ToDictionary(p => p.Id);

        // Phase 1: dodges.
        var dodgers = seated.Where(s => s.Action is PlayerAction.Dodge).Select(s => s.Player.Id).ToHashSet();

        // Phase 2: all attacks fire simultaneously. Being hit never cancels an attack.
        var hittersOf = new Dictionary<string, List<string>>();
        foreach (var (shooter, action) in seated)
        {
            if (action is not PlayerAction.Attack attack)
                continue;

            var me = players[shooter.Id];
            players[shooter.Id] = me with { Bullets = me.Bullets - 1 };

            var hit = !dodgers.Contains(attack.TargetId);
            reveal.Add(new RevealStep.ShotFired(shooter.Id, attack.TargetId, hit));
            if (hit)
                (hittersOf.TryGetValue(attack.TargetId, out var list)
                    ? list
                    : hittersOf[attack.TargetId] = []).Add(shooter.Id);
        }

        foreach (var (targetId, hitters) in hittersOf)
        {
            var target = players[targetId];
            players[targetId] = target with { Hp = target.Hp - hitters.Count };
        }

        // Phase 3: a hit player's Load/Chest is cancelled.
        var cancelled = new HashSet<string>();
        foreach (var (player, action) in seated)
        {
            if (action is (PlayerAction.Load or PlayerAction.OpenChest) && hittersOf.ContainsKey(player.Id))
            {
                cancelled.Add(player.Id);
                reveal.Add(new RevealStep.ActionCancelled(player.Id, action));
            }
        }

        // Phase 4: loads.
        foreach (var (player, action) in seated)
        {
            if (action is not PlayerAction.Load || cancelled.Contains(player.Id))
                continue;

            var me = players[player.Id];
            players[player.Id] = me with { Bullets = Math.Min(me.Bullets + 1, state.Parameters.MaxBullets) };
            reveal.Add(new RevealStep.GunLoaded(player.Id, players[player.Id].Bullets));
        }

        // Phase 5: chests — a bar only when exactly one un-cancelled player targeted it.
        for (var chest = 0; chest < state.ChestCount; chest++)
        {
            var contenders = seated
                .Where(s => s.Action is PlayerAction.OpenChest c && c.ChestIndex == chest)
                .Select(s => s.Player.Id)
                .ToList();
            if (contenders.Count == 0)
                continue;

            var eligible = contenders.Where(id => !cancelled.Contains(id)).ToList();
            string? winner = eligible.Count == 1 ? eligible[0] : null;
            if (winner is not null)
            {
                var w = players[winner];
                players[winner] = w with { Gold = w.Gold + 1 };
            }

            reveal.Add(new RevealStep.ChestResolved(chest, contenders, winner));
        }

        // Phase 6+7: eliminations and loot. Gold snapshots are taken before any
        // transfer so simultaneous deaths loot each other's pre-death gold.
        var victims = seated.Where(s => players[s.Player.Id].Hp <= 0).Select(s => s.Player.Id).ToList();
        var goldAtDeath = victims.ToDictionary(id => id, id => players[id].Gold);
        foreach (var victimId in victims)
            players[victimId] = players[victimId] with { Gold = 0 };

        foreach (var victimId in victims)
        {
            var looters = hittersOf[victimId];
            var share = goldAtDeath[victimId] / looters.Count;
            foreach (var looterId in looters)
            {
                var looter = players[looterId];
                players[looterId] = looter with { Gold = looter.Gold + share };
            }

            reveal.Add(new RevealStep.PlayerEliminated(
                victimId, looters, share, goldAtDeath[victimId] - share * looters.Count));
        }

        var newState = state with
        {
            Players = state.Players.Select(p => players[p.Id]).ToArray(),
            RoundNumber = state.RoundNumber + 1,
        };

        // Phase 8: win check.
        var (winners, reason) = WinEvaluator.Evaluate(newState, victims);
        if (winners is not null)
            reveal.Add(new RevealStep.GameEnded(winners, reason!.Value));

        return new RoundResult
        {
            NewState = newState,
            Reveal = reveal,
            WinnerIds = winners,
            WinReason = reason,
        };
    }

    private static void ValidateSubmission(GameState state, IReadOnlyDictionary<string, PlayerAction> actions)
    {
        foreach (var player in state.AlivePlayers)
        {
            if (!actions.ContainsKey(player.Id))
                throw new InvalidActionException(player.Id, "no action submitted");
        }

        foreach (var (playerId, action) in actions)
        {
            var reason = ActionValidator.Validate(state, playerId, action);
            if (reason is not null)
                throw new InvalidActionException(playerId, reason);
        }
    }
}
