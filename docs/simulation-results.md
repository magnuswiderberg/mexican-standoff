# Simulation Results — Parameter Tuning (2026-07-08)

Run: `dotnet run --project src/MexicanStandoff.Simulation -c Release -- --games 1000`
(7 configs × 6 player counts × 1000 games, seed 12345, five bot strategies —
adaptive, aggressive, chest-rusher, turtle, random — cycled through the seats.
Duration estimate assumes ~30 s per round.)

## Decision: keep the baseline defaults

**HP 2, max bullets 2, gold to win 3, 1 chest at 2–4 players / 2 chests at 5–8.**

Baseline game lengths land right in the target window (max ~10 min):

| Players | avg rounds | p90 | est. duration | timeouts |
|--------:|-----------:|----:|--------------:|---------:|
| 2 | 5.5 | 8 | ~2.7 min | 0% |
| 3 | 7.7 | 11 | ~3.8 min | 0% |
| 4 | 12.4 | 19 | ~6.2 min | 0% |
| 5 | 10.5 | 18 | ~5.2 min | 0% |
| 6 | 13.1 | 22 | ~6.5 min | 0% |
| 8 | 16.1 | 25 | ~8.1 min | 0.8% |

## What the alternatives showed

- **gold=2** — too swingy: pure chest-rushing becomes a top strategy at 8p
  (32% win rate) and the aggressive archetype can barely win. Rejected.
- **gold=4** — 8p games stretch to ~9 min average (p90 27 rounds). Rejected.
- **hp=3** — mainly makes small games longer (2p ~4.3 min) without improving
  balance. Rejected.
- **bullets=3** — nearly identical to baseline; no reason to complicate. Rejected.
- **2 chests already from 4 players** — marginal effect (4p: 6.2 → 6.0 min). Not worth it.
- **single chest always** — clearly bad at scale: 8p averages ~12.4 min with
  2.9% timeouts. Confirms the 2-chest threshold is needed.

## Balance observations (baseline)

- **The mind-game works**: pure chest-rushing is punished hard (0–12% win rate);
  gold has to be earned by reading the table, not by spamming Chest.
- **Adaptive play wins**: the heuristic all-rounder is the best or near-best
  strategy at almost every table size — skill expression exists.
- **Turtling is strong at big tables** (up to ~40% at 8p): hide while others
  shoot, profit late. Real players will punish an obvious turtle more than bots
  do; worth watching in playtests.
- **From 4 players up, ~98% of games end on gold**, not elimination — chests set
  the pace, guns set the threat. Games still feel violent (HP is lost constantly)
  but the clock is the gold race.
- **The Final Duel is a shootout**: at 2 players, aggression wins ~88% and only
  ~3% of duels end on gold; ~20% end in mutual destruction. For an *endgame*
  that's arguably the right drama, but if 2-player *starts* feel one-note in
  real play, revisit duel chest rules (v2 candidate).

## Variant run: everyone starts with a loaded gun (2026-07-08)

Run: `... --games 1000 --start-bullets 1` (new `StartingBullets` parameter).

Compared to the unloaded baseline:

- **Game length barely moves** (8p: 16.1 → 15.1 avg rounds; others ±0.5). The
  free bullet saves one Load action, but play patterns adapt around it.
- **The opening changes character**: with unloaded guns, round 1 is a "safe"
  round everyone can reason about (nobody can shoot yet); with loaded guns the
  game is lethal from the first reveal.
- **Aggression gets stronger at small tables**: at 3 players the aggressive bot
  jumps from 48% → 61% win rate and becomes clearly dominant. At 5+ players the
  effect washes out.
- **Duels get less suicidal**: 2p mutual destruction drops 20% → 12%, and the
  aggressive-vs-adaptive gap narrows (88/28 → 74/32).

**Decision: keep the unloaded start as the default.** It balances small tables
better and gives new players one safe round to find their feet. A loaded-gun
start is a good candidate for the v2 asymmetric/variant setups — mechanically it
already works via `StartingBullets`.

## Caveats

- Bots are simple heuristics; humans bluff, spite-shoot and meta-game. Treat
  these numbers as sanity bounds, not truth — re-run after real playtests.
- Duration model is flat 30 s/round; duel steps resolve 3 per selection, so
  duel-heavy games are slightly overestimated.
