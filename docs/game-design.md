# Game Design — Mexican Standoff: A Quick Mind Game

A fast (max ~10 min) party game for 2–8 players. Everyone has an unloaded gun.
Win by being the first to collect **3 gold bars** or by being the **last player standing**.

This document is the authoritative rules spec for the game engine. Values marked
**(param)** are game parameters that the simulation harness will tune.

## Setup

- 2–8 players join a game via QR code / game code.
- Each player starts with:
  - **2 HP** (param)
  - An **unloaded gun** holding at most **2 bullets** (param)
  - **0 gold bars**
- Chests available, based on players *currently alive* (param):
  - 2–4 players: **1 chest**
  - 5–8 players: **2 chests**
- Gold bars needed to win: **3** (param)
- Chests hold an unlimited supply of bars (param — may become a finite pool later).

## Round flow

1. Every alive player secretly selects one action card (Attack and Chest also
   require a target: a player or a specific chest).
2. When all players have selected (or the selection timer expires), all cards
   are revealed simultaneously.
3. The engine resolves the round (see Resolution) and broadcasts a reveal
   script that all devices play back in sync.
4. Win conditions are checked; otherwise a new round starts.

Selection timer: **30 s** (param). A player who hasn't selected when it expires
plays **Dodge** automatically.

## Actions

| # | Action | Requirement | Effect |
|---|--------|-------------|--------|
| 1 | **Dodge** | — | Cannot be hit by shots this round. |
| 2 | **Attack** (target player) | ≥1 bullet | Fire one bullet at the target. Always consumes the bullet, even if the target dodges. |
| 3 | **Load** | <max bullets | Add one bullet to your gun. |
| 4 | **Chest** (target chest) | — | Attempt to take a gold bar from that chest. |

The UI disables cards whose requirements aren't met (Attack with empty gun,
Load with a full gun).

## Resolution: simultaneous volley

The round resolves in these phases, in order:

1. **Dodge** — dodging players are marked untouchable for the round.
2. **Attack** — *all* attacks fire simultaneously. Being hit never cancels
   another player's attack: if A shoots B while B shoots C, both shots land.
   Mutual shootouts (A↔B) hit both. Each shot:
   - consumes one bullet from the shooter,
   - deals 1 damage to the target unless the target dodged.
3. **Cancellation** — any player who was **hit** this round has their Load or
   Chest action cancelled. (Attacks were already resolved and are never
   cancelled; Dodge has no effect to cancel.)
4. **Load** — surviving, un-hit loaders gain one bullet (up to the gun's max).
5. **Chest** — for each chest: if **exactly one** un-cancelled player targeted
   it, that player gains one gold bar. If two or more players targeted the same
   chest, **nobody** gets a bar from it (a standoff at the chest).
6. **Eliminations** — players at 0 HP or less are *wounded* and eliminated.
7. **Loot** — an eliminated player's gold bars are split evenly among the
   players who shot them this round, rounded down; the remainder is lost.
   Looted gold counts fully — it can push a shooter to 3+ bars and win the
   game on the spot.
8. **Win check** — see Winning.

Note: a player who is hit while going for a chest loses HP *and* the bar; the
only defense against being shot is Dodge.

## Elimination

A player reduced to 0 HP is eliminated ("wounded") and becomes a spectator:
their device keeps showing the game (reveals, standings) and offers them a
one-tap **join next game** when a rematch starts.

## Winning

The game ends when, after a round resolves:

- A player has **≥3 gold bars**, or
- Only **one player** is left alive.

If several players cross 3 bars in the same round, ties break in order:

1. Most gold bars
2. Most HP remaining
3. Most bullets in the gun
4. Still tied → **shared victory**

## Two-player mode: the Final Duel

Used when a game starts with 2 players **or** when eliminations leave exactly
2 alive. Prevents the endless "both grab the chest" loop and gives the endgame
a showpiece finish.

- Both players secretly program a **sequence of 3 actions** (each with targets
  where applicable).
- The engine resolves the sequence step by step — each step is resolved as a
  normal simultaneous-volley round between the two players — with a dramatic
  step-by-step reveal.
- Requirements are validated against the *projected* state within the sequence
  (e.g. you may program Load then Attack with an empty gun; Attack-first is
  invalid). If a step becomes illegal at resolution time (e.g. your planned
  Attack's bullet was never loaded because the Load was cancelled), the action
  fizzles into a Dodge.
- If neither player has won after the 3 steps, both program a new sequence.
- Stalemate guard **(param, needs simulation)**: after 3 full sequences with no
  elimination and no gold gained, sudden death — the chest is removed and both
  players receive one free bullet before each new sequence.

## Game parameters to tune via simulation

The simulation harness (bots with different strategies playing thousands of
games) tunes these for a fun, ~5–10 minute game:

| Parameter | Initial value |
|-----------|---------------|
| Starting HP | 2 |
| Max bullets in gun | 2 |
| Gold bars to win | 3 |
| Chests per alive-player count | 1 (2–4), 2 (5–8); maybe 3 at some threshold |
| Selection timer | 30 s |
| Chest gold supply | unlimited |
| Final Duel stalemate guard | after 3 sequences |

## Open questions (not blocking MVP)

- Timeout action: is auto-Dodge right, or should repeat offenders be dropped
  from the game after N consecutive timeouts?
- Should the reveal call out *who shot whom* immediately, or build suspense
  (e.g. reveal dodges → shots → consequences)? To be prototyped in the UI.
- Player identity: pick a name + avatar/color at join (no accounts in MVP).
