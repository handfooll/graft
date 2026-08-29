# Phase 4 - Essence, cargo, and the walk home

*Agreed 2026-08-30. Status: **written, not built.** Build against this file.*

`DESIGN.md` already agrees this phase exists - §3 (the extraction loop), §4 (cargo), D4 (death
costs unbanked cargo only), D5 (corruption resets on banking). This spec is therefore about *how*,
not *whether*. It closes the four things the contract left open, and each of those four was decided
here rather than asked, at the author's instruction ("do what you recommend").

---

## 1. What it is

Enemies drop **essence motes** on death. Motes auto-attract to the player and consume **cargo**, a
capacity the player can see at all times. Cargo is worth nothing until it is **banked**, which
happens automatically the moment the player steps into the bank plaza at the centre of Base - and
banking is also what resets corruption and clears grafts. Dying anywhere else loses **all** unbanked
cargo and nothing else. A visible cue points home whenever the player is carrying something, so the
decision "one more fight, or leave now" is always in front of them. Nothing else in the game is
lost, ever.

---

## 2. The four decisions this spec closes

### D-P4.1 - Taking a graft does **not** spend essence. Corruption is the whole cost.

The family meter of §5 is a **gauge, not a wallet**: filling it offers a graft, accepting one costs
corruption and leaves cargo untouched.

Why. The alternative - grafts drain the essence you are carrying - makes a stronger-sounding trade
and is the wrong one. DESIGN.md names the mutation ladder as *the hook*: "if those four images do
not read as becoming a monster, the game does not have its selling point yet". Charging cargo for
grafts teaches the player to refuse them, which optimises them straight past the thing the game is
selling. The contract already prices grafting, and prices it better: corruption is non-linear,
visible on the character, and gets *more* dangerous exactly as the cargo gets more valuable - which
is the §3 tension stated in one variable. A second, linear price on the same action flattens it.
Practically it also keeps Phase 4 finishable: coupling cargo to per-family pools would mean Phase 4
could not be judged until Phase 5 existed, and one phase at a time is the discipline that survived.

**The consequence worth building for:** because motes fill cargo *and* family meters, a full cargo
bar stops both. Full means no more money **and** no more power. That is the non-nagging prompt §4
asks for, and it needs no UI nagging at all.

### D-P4.2 - Base is two rings. The safe disc lets you survive; the inner plaza banks.

`WorldLayout.baseRadius` is **20 m** today and `WorldRegions.IsInBase` covers all of it - enemies do
not spawn there and the player heals. Phase 4 adds an inner **bank plaza, radius 7 m**, at the
monument. Crossing *into the plaza* banks, unconditionally and with no button press.

Why. Straight "entering Base banks everything" has a trap the cluster world makes real: Base borders
several biomes, so travelling between two of them can route the player across Base, and an
accidental bank ends an expedition the player was in the middle of. But making banking a button
press is worse - it costs a thumb the player does not have spare (D8), and it turns the moment of
arriving home, which should be the payoff, into paperwork. Two rings gives both: flee into the 20 m
disc, be safe, keep your build and your cargo, catch your breath; walk the last 7 m to the monument
and the expedition is over. The retreat valve exists, the bank still costs no input, and DESIGN.md
§3's sentence stays true - the spec just says precisely which "it" is being entered.

### D-P4.3 - Death takes **all** unbanked cargo. No corpse run, not yet.

Respawn at the Base anchor, whole. Banked total untouched, grafts cleared, corruption zeroed.

Why. D4 is locked and says it: "Death = lose unbanked cargo **only**" - the "only" limits which
*categories* are at risk, not what fraction of them. Keeping some fraction blunts the exact thing §3
calls the whole game. The tempting middle - the cargo spills where you fell and can be recovered -
is not forbidden by D4 and is proven elsewhere, but it must not land in this phase: Phase 4's job is
to measure whether the walk home is a commitment, and a recovery mechanic dulls the signal being
measured while the measurement is being taken. It is additive later and invalidates nothing.

**It is therefore the first lever if playtesting says death is too punishing** - before touching
cargo capacity or essence value, which are load-bearing for the depth gradient.

### D-P4.4 - A full expedition is ~2.7 min shallow, ~3.5 min deep. Depth changes payout, not fill rate.

Target: **~150 s of sustained fighting to fill an empty cargo bar, in any biome** - about 40 kills.
A mote costs the same cargo everywhere; what depth changes is what a banked mote is *worth*.

| Run | Walk out | Fill | Walk home | Total | Walk home as % |
|---|---|---|---|---|---|
| The Verge | ~7 s | ~150 s | ~7 s | ~2.7 min | 5% |
| Ember Scar | 29 s | ~150 s | 29 s | ~3.5 min | 14% |

Why this shape. Short loops are right here - the extraction decision *is* the product, and the
player should meet it many times a session, not twice. But Phase 5's ladder needs enough time inside
one expedition to climb visibly toward 95% corruption, which two flat minutes does not buy. Holding
fill time constant and letting the walk vary resolves both: the loop stays tight, and depth is felt
as the thing §7 says it is - the difficulty dial.

**This answers the question `STATUS.md` carried forward.** 29 s home from Ember Scar is 14% of the
run, walked while carrying the most valuable bar in the game with the heaviest swarm in the game
behind you. That is a commitment. It is also the number most likely to need a nudge after the first
real playtest, and depth is still one field in `WorldLayout_Cluster.asset`.

**Depth value ratio:** one full bar from Ember Scar banks **~4.1x** one full bar from the Verge -
the same ratio Phase 2 tuned distance to. Derive it from `WorldMap.DistanceFromBase`, never a copied
constant, so that re-tuning depth re-tunes payout and does not become a test rewrite. This mirrors
how the Phase 2 depth test was written and for the same reason.

---

## 3. Files and types

### `Graft.Core` - no UnityEngine, all of it unit-tested

| Type | Responsibility |
|---|---|
| `Cargo` | Capacity, units carried, `TryAdd`, `Clear`, `IsFull`, `Fraction`. Rejects overflow rather than clamping silently, so the caller can decide what a refused mote does. |
| `EssenceMote` | Readonly struct: `EnemyFamily` + cargo units + origin `Area`. |
| `EssenceValues` | Banked value per unit by area, **derived from `WorldMap.DistanceFromBase`**, normalised so the nearest biome is 1.0. |
| `ExtractionLedger` | The rules of the loop: `Carried`, `Banked`, `Absorb(mote)`, `Bank()`, `LoseCarried()`. Raises `Banked` and `Lost` events - this is the seam Phase 5 hangs corruption reset and graft clearing on. |

`ExtractionLedger` is where the phase actually lives. Everything below it is presentation.

### `Graft.Game`

| Type | Responsibility |
|---|---|
| `EssenceTuning` (ScriptableObject) | Capacity, motes per kill by family, attract radius, flight speed, mote lifetime. Data in an asset, never hardcoded - build rule 3. |
| `EssenceDrop` | The mote in the world. Spawned from `Damageable.Died`, which **already fires exactly once** and is already guarded against a second shot killing the same thing twice - hang the drop there, do not add a second death path. |
| `EssenceMagnet` | On the player. Attracts within **3.5 m** (about a third of the 9.5 m visible width, so absorption happens visibly on screen), flies motes in at ~9 m/s, hands them to the ledger. At full cargo it does not attract: motes stay on the ground, visibly unclaimed. |
| `BankZone` | The 7 m plaza trigger. Calls `ExtractionLedger.Bank()`. No button. |
| `HomeCue` | Ground-projected chevrons near the player pointing at the Base anchor, shown whenever `Carried > 0`, more urgent as the bar fills. World-space, not a screen-edge compass - D2 fixes the camera, so a metre is a fixed number of pixels and world-space costs no per-frame projection. Same reasoning that put the health bars in the world. |
| `PlayerVitals.Fall()` | **Replaced.** Today it is Phase 3's stand-in - back to Base, whole, nothing lost. It becomes: `ledger.LoseCarried()`, then the existing teleport and heal. The seam was left as one method; keep it that way. |
| `WorldLayout.bankRadius` | New field, default **7 m**. `WorldRegions` gains `IsInBankPlaza(position)` alongside the existing `IsInBase`. |

### `Graft.UI`

| Type | Responsibility |
|---|---|
| `CargoBar` | Always visible, hung off the existing `SafeArea` - which `STATUS.md` notes was built empty in Phase 1 for exactly this. Shows fill; distinct state at full. |
| `BankedReadout` | Banked total, plus what the carried bar is currently worth. The carried value is the number the extraction decision is actually made on, so it has to be on screen. |

HUD form is the implementer's call (standing instruction, released 2026-08-24). What is **not**
optional: cargo fill and carried value are readable at a glance, at all times, without the player
looking away from their character.

---

## 4. Deliberately out of scope

- **Per-family essence meters and graft offers** - Phase 5. Phase 4 ships the `Banked` event they
  will subscribe to, and nothing more.
- **Corruption itself** - Phase 5. Banking raises the event that will reset it; there is nothing to
  reset yet, and Phase 4 must not build a meter it cannot show.
- **Base upgrades** (cargo capacity, `CorruptionResistance`) - DESIGN.md §4 and §6 make these the
  progression sink, but a spending screen is a later phase. Capacity is a field in `EssenceTuning`.
- **Corpse recovery** - see D-P4.3. Named so nobody assumes it, and named as the first lever if
  death proves too harsh.
- **Loot that is not essence.** §4 says "essence and loot both consume cargo"; only essence exists.
- **Audio** - §8, deferred outright.

---

## 5. How it gets verified

### Assertions (EditMode, `Graft.Core`)

1. `Cargo` refuses a mote that would exceed capacity, and reports `IsFull` at exactly capacity.
2. `ExtractionLedger.Bank()` moves carried to banked, empties cargo, and raises `Banked` **once**.
3. `LoseCarried()` empties cargo and leaves `Banked` **bit-identical** - this is D4, and it is the
   one assertion that must never be softened.
4. Bank-then-die and die-then-bank both leave the banked total correct; neither double-counts.
5. **The depth ratio**: a full bar from Ember Scar banks ~4.1x a full bar from the Verge, asserted
   against `WorldMap.DistanceFromBase` and not against a literal, so re-tuning depth is not a test
   rewrite.
6. Kills to fill an empty bar lands in **35–45** for every biome.
7. `EssenceValues` is monotonic in distance from Base - no biome is ever worth less than a
   shallower one.

### PlayMode

8. A kill leaves a mote; the mote reaches the player; cargo rises by its units.
9. At full cargo, motes are **not** absorbed and are still lying there afterwards.
10. Crossing into the 7 m plaza banks. Standing anywhere else in the 20 m Base disc **does not** -
    this is D-P4.2 and it is the assertion that proves the retreat valve exists.
11. Player death out in the world zeroes carried and preserves banked.

### The gate - four PNGs, per `BUILD_PLAN.md`

| Image | What it has to show |
|---|---|
| `phase4_0_cargo_empty` | Leaving Base. Bar empty, home cue absent - nothing at risk yet. |
| `phase4_1_cargo_half` | Mid-expedition, motes flying in, bar half, home cue live. |
| `phase4_2_cargo_full_far` | **The picture the phase exists for.** Full bar, deep in Ember Scar, enemies present, cue pointing at a home that is 29 s away. |
| `phase4_3_banking_at_base` | The plaza, the moment of banking, the banked total jumping. |

> `BUILD_PLAN.md`: *"If those four images do not communicate the risk, the loop is not readable and
> needs redesign before Phase 5."* Judge them against that sentence, not against whether the code is
> right. The tests above cannot see a loop that does not read.

Baselines land in `Tests/GateBaselines/`; regenerate them with the screenshot tour, and remember the tour
runs **without** `-nographics`.

---

## 6. What to do first

Build `ExtractionLedger` and its tests before anything touches a MonoBehaviour. Every decision above
is expressible as arithmetic on that one type, and if the arithmetic is wrong no amount of HUD makes
the loop read.

---

## 7. Addendum - written during the build, 2026-08-30

Everything above was agreed before any code existed. Two things in it turned out not to survive
contact with the numbers, and both are recorded here rather than quietly edited away, because a spec
that has been silently rewritten to match what got built is not a spec.

### §5 assertion 6 was wrong, and could not be satisfied

> *"Kills to fill an empty bar lands in **35–45** for every biome."*

This cannot be true at the same time as D-P4.4's ~150 s, at any tuning. Measured from the shipped
assets, the sustained kill rate per biome is Verge 0.53/s, Rust Flats 0.45, Glass Mire 0.52, Bone
Reach 0.17, Ember Scar 0.23 - a factor of three across the world, because a Bone Reach Shellback has
nine times a Verge Chitter's effective health. A flat 40 kills therefore means fill times of 76–239 s;
a flat 150 s means 25–79 kills. The two numbers in D-P4.4 were only ever consistent with each other
at depth, where the spec's arithmetic was evidently done.

**The fill time was kept and the kill count dropped**, because D-P4.4's stated reasoning is that fill
time is the invariant and Phase 5's corruption ladder depends on it. What makes it hold is
`EssenceRules`: a mote's cargo cost is proportional to the **effective health of the enemy that
dropped it**, so effective health cancels out of (kills per second × cargo per kill) and the bar
fills at a rate set by the player's damage output and nothing else. That is *depth changes payout,
not fill rate* as an identity rather than as a tuning aspiration.

Kills-to-fill is now derived rather than designed, and falls with depth because deep enemies pay
more each: **Verge 125, Rust Flats 68, Glass Mire 69, Bone Reach 24, Ember Scar 33.** The spec's
"about 40 kills" is right in the two deepest biomes and wrong everywhere else.

Measured fill times, printed by `ConfigureAll` and asserted in `ExtractionAssetTests`:

| | Verge | Rust Flats | Glass Mire | Bone Reach | Ember Scar |
|---|---|---|---|---|---|
| Seconds to fill | 150 | 149 | 134 | 145 | 144 |

### One Phase 3 tuning value had to move

The Verge was the only biome in the game that could not send enemies as fast as the player killed
them - 0.53 kills/s against a capability of 0.84 - so the player stood waiting for a third of every
expedition and the tutorial biome took **238 s** to fill a bar against everyone else's ~145. Invisible
before cargo existed to measure it against.

`SpawnTables` Verge `secondsBetweenSpawns` is now **1.15 s**, down from 1.9. Danger barely moves: four
Chitters at ×1.00 still need twelve seconds to empty a 225-health player, and the Verge remains the
gentlest biome by a factor of 2.5 on incoming damage. It is one field to revert; the cost of
reverting is that the Verge's expedition goes back to being 60% longer than everywhere else.

A consequence worth knowing: this makes the Verge's *pacing* faster than Rust Flats', which broke
`CombatAssetTests.DistanceFromBaseIsTheDifficultyDial`. That test asserted each of the three spawn
dials was independently monotonic with depth, which is a proxy rather than the thing meant - a
spawn interval is not a difficulty measure on its own. It now asserts population cap and stat
multiplier as before, and the composite (`AreaExpedition.IncomingDamagePerSecond`: 23 / 59 / 59 /
112 / 151 dps) in `TheDeeperBiomesPressWithMoreForce`.
