# GRAFT - Design Document v2
*Supersedes the v1 concept doc entirely. v1 is archived at the old project archive.*

> **Why v2 exists.** v1 never contained the words "3D" or "open world". It specified an
> arena-survivor with mutating *sprites*. A build was produced that matched it exactly and was
> useless, because the requirement lived in the author's head and not on the page.
> **Everything the game must be is written down here. If it is not in this file, it is not agreed.**

---

## 1. What this game is, in one paragraph

A **3D top-down** action game. The camera sits above the player and follows them; the player moves
freely with a virtual stick through a **continuous world** made of a safe Base plus **five distinct
biomes**. Enemies drop **Essence** shaped like themselves. Absorbing enough of one enemy family
offers a **Graft** - you choose which of five body slots it goes into, and your character
**visibly mutates**. Grafts push a **Corruption** meter: more power, but past a threshold you start
burning your own health. Loot you pick up is **unbanked** - you must physically **walk back to Base
to bank it**, or lose it when you go down. That walk back is the game.

## 2. Locked decisions

These were open in v1 and are now closed. Do not reopen without editing this file.

| # | Decision | Value |
|---|---|---|
| D1 | Dimensionality | **3D.** Real meshes, real 3D movement, NavMesh enemy navigation. Not sprites, not 2.5D. |
| D2 | Camera | **Top-down, follows the player, player stays centred.** Fixed pitch, no player-controlled rotation. |
| D3 | World shape | **Continuous.** Base + 5 biomes, walkable between. No scene-load gate between biomes. |
| D4 | Session shape | **Expedition / extraction.** Leave Base → gather → return to Base to bank. Death = lose unbanked cargo only. |
| D5 | Corruption reset | On **banking at Base**, not on a timer. Corruption is the cost of staying out longer. |
| D6 | Mutation visuals | **Sockets + shader.** Pre-made props snapped to rig anchors, plus material tint and bone scale. No runtime mesh surgery, no blendshapes in v1. |
| D7 | Codebase | **Clean slate.** Nothing inherited from the deleted 2D build. |
| D8 | Platform | **iOS first**, Android later. Touch controls from the start. |
| D9 | Engine | Unity **6000.5.8f1** (already installed). |

## 3. The extraction loop (the core change from v1)

```
        ┌──────────────────── BASE ────────────────────┐
        │  safe · bank cargo · spend · corruption = 0   │
        └───────────────┬──────────────────────────────┘
                        │ walk out
                        ▼
        ┌──────────────────────────────────────────────┐
        │  BIOMES - fight, absorb Essence, take Grafts  │
        │  cargo ▲   corruption ▲   danger ▲            │
        └───────────────┬──────────────────────────────┘
                        │
          ┌─────────────┴──────────────┐
          │                            │
     walk back                   go down out there
          │                            │
          ▼                            ▼
   bank everything            lose unbanked cargo,
   corruption resets          keep banked progress,
   grafts cleared             respawn at Base
```

**The tension:** every additional graft makes you stronger *and* raises corruption *and* the cargo
you are carrying is worth more, so the walk home gets more dangerous exactly as it gets more
valuable. This is the whole game. If a change would weaken this tension, it is the wrong change.

**Precedent:** the shipped XP Hero already has a `Cargo` system - `CargoAmount`, `CargoFillRate`,
`CargoMaxed`, `CargoBarUI`, `CargoFullOffer`. Carry-capacity-then-bank is proven in this genre, not
an experiment.

## 4. Cargo

- Cargo has a **capacity**. Essence and loot both consume it.
- Cargo capacity is a **Base upgrade** - a primary progression sink.
- At full cargo, gathering stops mattering → a natural, non-nagging prompt to head home.
- Cargo is **visible at all times** (a bar), because the player must be able to judge the risk.
- Banked currency is permanent. Unbanked cargo is at risk. Nothing else is ever lost on death.

## 5. Grafting

Unchanged in spirit from v1 §4, restated with 3D specifics.

Three independent axes - keeping them independent is the design:

| Axis | Decides | Example |
|---|---|---|
| **Enemy family** | *what* the ability does | venom / impact / hide / spark |
| **Body slot** | *how* it is delivered | Head = aimed · Core = aura · Arms = swipe · Legs = dash-burst |
| **Weapon** | *what shape* the delivery takes | blade = sweeping arc · gun = spread |

Five slots: **Head, Core, LeftArm, RightArm, Legs.** A slot can be overwritten later in an
expedition - that is a real build decision, not just accumulation.

**Grafts are cleared on banking.** They are expedition-scoped, like v1's run-scoped grafts.

## 6. Corruption

- Every graft raises corruption.
- **Low corruption:** small precision/focus bonus, character looks human.
- **High corruption:** flat damage and HP multipliers, character looks monstrous.
- **Past 85%:** slow self-damage tick.
- Resets to 0 **on banking**.
- `CorruptionResistance` is a Base upgrade - raises how many grafts you can carry before turning.

**Visual rule (D6):** corruption drives (a) material tint toward corrupted colours, (b) bone scale
swell, (c) how many socket props are visible. Each graft additionally snaps its own prop to its
slot. No mesh deformation, no blendshapes - those are v2 scope at the earliest.

## 7. The five biomes + Base

Six areas total. Each biome differs in **palette, ground material, enemy families, and Essence
types**. Biome identity must be readable in a single screenshot.

| Area | Role | Notes |
|---|---|---|
| Base | Safe hub | Bank, upgrades, no enemies |
| Biome 1 | Tutorial-tier | Nearest Base, weakest enemies, safest walk home |
| Biomes 2–5 | Escalating | Further from Base = better loot, longer and more dangerous return |

**Distance from Base is the difficulty dial.** This is what makes a continuous world *mean*
something rather than being five arenas in a trenchcoat.

## 8. Deferred - explicitly NOT in the first build

Named here so nobody assumes them: gacha, IAP, ads, Vessels/hero collection, weapon merging,
bosses, audio, multiplayer, offline plunder, remote config, backend of any kind.

v1 tried to specify all of these at once. That is what made it unbuildable.
