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

## Variant run: rescaled gold economy — chest pays more, target scales (2026-07-12)

Run: `... --games 1000` with new `GoldPerChest` parameter and a `lootLost`
column (share of games where loot-split rounding destroyed gold / average gold
destroyed as a share of the win target). Motivation: with chest=1/win=3, a
2-shooter kill of a 1-bar player destroys the whole bar.

Compared configs with identical grabs-to-win: baseline (1/3), **2/6**, **3/9**.

| Players | baseline lootLost | chest=2 win=6 | chest=3 win=9 |
|--------:|------------------:|--------------:|--------------:|
| 3 | 20% / 6.7% | **0% / 0.0%** | 20% / 2.2% |
| 4 | 20% / 6.6% | **0% / 0.0%** | 20% / 2.2% |
| 5 | 19% / 7.7% | **4% / 0.8%** | 19% / 2.3% |
| 6 | 55% / 34.6% | 38% / 8.9% | 35% / 4.8% |
| 8 | 92% / 96.0% | 54% / 18.4% | 80% / 20.0% |

- **chest=2/win=6 eliminates rounding loss at 3–5 players.** Kills are almost
  always 2-shooter (HP 2) and chest grabs keep gold totals even, so 2-way splits
  are exact. At 8p the baseline destroys ~a full win-target of gold per game;
  2/6 cuts that ~5×.
- **Pacing is unchanged at small tables and *better* at large ones**: 8p drops
  16 → 12 avg rounds (~7.8 → ~6.1 min) because looted gold recycles into the
  race instead of evaporating.
- **Balance improves at 8p**: turtle dominance falls 38% → 15% and win rates
  spread out (adaptive 19%, turtle 15%, aggressive 11%) — gold destruction was
  quietly subsidizing the turtle. Chest-rushing stays punished (≤10%).
- **3/9 is strictly worse than 2/6** below 6 players (odd totals reappear) and
  no better at 8; bigger numbers for nothing.
- 2p untouched (duel loot barely exists).

**Recommendation: adopt chest=2 / win=6** pending a real playtest — the one
cost is presentational ("first to 6 bars", two-bar grab animations).

**Adopted as the default (2026-07-12)**: `GoldPerChest = 2`, `GoldToWin = 6`.
The simulation baseline now uses these values; `chest=1 win=3` remains as a
legacy comparison config.

## Variant run: the Heal action — spend gold to restore HP (2026-07-16)

Run: `... --games 1000` with a new `Heal` action (5th card). Start at 2 HP,
spend `HealCost` gold to restore 1 HP up to a raised `MaxHp` ceiling. Heal is an
*investment action* like Load — a hit cancels it and refunds the gold — and it is
**disabled inside the Final Duel** (duel sequences carry no Heal). Only the two
defensive archetypes seek it out: adaptive patches back up when wounded and safe,
turtle banks to the ceiling whenever nobody is armed. The `heal` column is
`share of games with ≥1 heal / average heals per game`.

Configs (all vs the no-heal baseline): `maxHp2 cost1` (pure 1→2 patch),
`maxHp3 cost{1,2}`, `maxHp4 cost{1,2}`.

| Config | 4p rounds | 4p heal | 5p rounds | 5p heal | 8p rounds | 8p heal |
|--------|----------:|--------:|----------:|--------:|----------:|--------:|
| baseline no-heal | 12.1 | 0% | 9.9 | 0% | 12.2 | 0% |
| maxHp2 cost1 | 12.3 | 0% | 10.5 | 20% / 0.3 | 12.5 | 8% / 0.1 |
| **maxHp3 cost2** | 13.5 | 23% / 0.2 | 11.8 | 48% / 0.8 | 12.7 | 35% / 0.5 |
| maxHp3 cost1 | 13.2 | 24% / 0.2 | 11.7 | 49% / 0.8 | 12.4 | 34% / 0.5 |
| maxHp4 cost2 | 13.7 | 24% / 0.3 | 11.7 | 48% / 0.9 | 12.5 | 33% / 0.5 |
| maxHp4 cost1 | 13.7 | 22% / 0.3 | 11.7 | 50% / 1.1 | 12.6 | 38% / 0.6 |

- **Raising `MaxHp` is what makes heal live; the price barely matters.** At
  `maxHp2` (heal can only patch 1→2) the card is nearly dead — 0% at 4p, single
  digits at 8p — because when you fear a shot you Dodge, and when you don't you
  don't need the HP. Lift the ceiling to 3 and heal is chosen in a quarter to
  half of 4–8p games: banking a buffer *above* your start is a reason to spend a
  calm round. `cost 1` vs `cost 2` is within noise (safety, not gold, is the
  binding constraint), so **keep cost 2** as a meaningful gold sink. `maxHp4`
  adds a second buffer point for negligible extra usage or pacing — not worth the
  extra HP-bar rendering.
- **Pacing stays inside the target.** Heal lengthens 4–8p games by ~0.5–1.8
  rounds (4p worst: 12.1 → 13.5, ~6.8 min); p90 creeps 18 → 20–21; timeouts stay
  ~0%. 2p/3p are untouched (heal is off in duels, and 3p resolves too fast/lethal
  to bother).
- **Balance improves rather than degrades.** The worry was that healing would
  inflate the already-strong turtle. It does the opposite: at 4p turtle falls
  50% → 42% while the skilled all-rounder (adaptive) climbs 34% → 36–39%, because
  adaptive now competes on durability too. Heal rewards *reading the table* (heal
  only when safe), not pure hiding. Chest-rushing stays punished (0% at 4p).

**Recommendation: if healing ships, use `MaxHp = 3`, `HealCost = 2`, `HealAmount = 1`**
(with `StartingHp = 2`). It is a real, frequently-taken decision at 4+ players,
keeps games inside the clock, and slightly widens skill expression. Not yet
adopted as a default — it needs a real playtest and the web reveal wiring
(a `playerHealed` step + heal card) before going live. Engine, bots, DTOs and
tests are in; the Final Duel deliberately excludes it.

### Refund policy: does a cancelled heal give the gold back? (2026-07-16)

Follow-up on the heal-hit interaction. A heal is cancelled by a hit; the
`HealCostRefundedOnCancel` parameter decides whether the gold is refunded (the
Load-like default) or spent anyway (healing under fire becomes a gamble). Sweeping
`maxHp3/maxHp4 cost2` with refund vs no-refund:

| Config (maxHp3 cost2) | 4p rounds/heal | 5p | 6p | 8p |
|-----------------------|:--------------:|:--:|:--:|:--:|
| refund | 13.3 / 20% | 11.9 / 49% | 12.2 / 41% | 12.8 / 33% |
| no-refund | 13.4 / 21% | 12.2 / 46% | 13.0 / 43% | 12.4 / 35% |

- **The two policies are statistically indistinguishable** (differences are
  within single-seed noise, and the win-rate spreads track). Reason: the bots that
  heal (adaptive, turtle) only do so when *no opponent is armed*, so their heals
  can never be cancelled — the refund branch is almost never exercised. Only
  RandomBot heals under threat, and rarely.
- **The refund rule is a human-facing lever, not a bot-facing one.** It changes
  the risk of *speculatively* healing when you're unsure whether you'll be shot —
  a decision perfect-information bots don't face. Pick it on feel, not on these
  numbers: refund (safe, forgiving, consistent with Load) vs no-refund (a real
  gamble, more dramatic).

**Adopted default: no-refund** (`HealCostRefundedOnCancel = false`, 2026-07-16).
Chosen for the tension — committing gold to a heal while unsure of incoming fire
is the psychology the mechanic is there for; a hit both denies the HP and burns
the bars. `HealCostRefundedOnCancel = true` remains available for a gentler,
Load-like variant.

## Caveats

- Bots are simple heuristics; humans bluff, spite-shoot and meta-game. Treat
  these numbers as sanity bounds, not truth — re-run after real playtests.
- Heal usage above is driven entirely by the adaptive + turtle heuristics
  (aggressive/chest-rusher never heal); real players will find sharper spots.
- Duration model is flat 30 s/round; duel steps resolve 3 per selection, so
  duel-heavy games are slightly overestimated.
