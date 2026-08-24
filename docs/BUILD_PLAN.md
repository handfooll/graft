# GRAFT - Build Plan

Read `docs/DESIGN.md` first. That file is the contract; this file is the order of work.

## The rule that exists because of what went wrong

> **Every phase ends with a screenshot that a human looks at before the next phase starts.**

The previous build compiled clean and passed 50/50 of its own tests while being the wrong game.
Tests prove the rules are right. **Only a picture proves the game is right.** A phase is not done
when it compiles, and not done when tests pass - it is done when you have looked at the PNG and
said yes.

Each phase below names its **Screenshot gate**: the specific thing you must be able to *see*.

---

## Phase 0 - Project skeleton (no gameplay)

Create the Unity 6 3D project properly, once.

- 3D URP template, colour space Linear, target iOS.
- Package deps: **Cinemachine** (camera), **Unity.AI.Navigation** (NavMesh), **Input System**.
  *(All three are what the shipped XP Hero uses - verified from its build.)*
- Assembly definitions so dependencies point one way only:
  `Graft.Core` (pure C#, no UnityEngine) → `Graft.Game` (MonoBehaviours) → `Graft.UI` → `Graft.App`.
- `.gitignore` for Unity (Library/, Temp/, Build/ - these are huge; disk is tight).
- Headless test + screenshot harness wired up **in Phase 0**, not later.

**Screenshot gate:** a grey box on a ground plane, lit, from the top-down camera. Nothing else.
If this does not look like a 3D scene viewed from above, stop - everything after inherits it.

## Phase 1 - Movement and camera feel

The single most important phase. If moving around is not pleasant, nothing else matters.

- `PlayerController` - virtual stick → 3D movement on the ground plane, with acceleration.
- `CameraRig` - Cinemachine, top-down, follows player, player stays centred, fixed pitch.
- Character is a **capsule primitive**. No art yet, deliberately.
- Tune: move speed, camera height, camera damping, stick deadzone.

**Screenshot gate:** three PNGs - player at rest, mid-move, and near a wall. You should be able to
tell which direction they are facing. **Play it on the phone before signing off.** Feel does not
survive a screenshot; this gate needs the device.

## Phase 2 - The world takes shape

- Ground built from modular tiles; Base + 5 biome regions laid out with real distances.
- Each biome gets a distinct ground material and palette (flat colours are fine).
- NavMesh baked across the whole walkable world (`NavMeshSurface`).
- Boundaries so you cannot walk into the void.

**Screenshot gate:** one PNG per area, six total. Each must be **identifiable as a different place**
from the picture alone. If two biomes are indistinguishable, the biome design has failed, and no
amount of later code fixes it.

## Phase 3 - Enemies and combat

- `EnemyAgent` on NavMeshAgent, chases player. Capsules, colour-coded per family.
- Four enemy families with distinct stats and colours.
- Player auto-attacks nearest target in range.
- Damage, death, hit feedback.
- Spawning driven by which biome the player is in (ScriptableObject spawn tables).

**Screenshot gate:** a fight in progress - several enemies converging, health bars visible,
a hit landing. Plus: enemies must be seen to *path around* obstacles, not walk through them.

## Phase 4 - Essence, cargo, and the walk home

**This is the phase that makes it your game rather than a generic survivor.**

- Essence drops on kill, auto-attracts to player, consumes cargo space.
- Cargo bar in the HUD, always visible.
- Base zone: entering it banks everything, resets corruption, clears grafts.
- Death out in the world: lose unbanked cargo, respawn at Base, keep banked.
- A visible directional cue pointing home.

**Screenshot gate:** four PNGs telling the story - cargo empty, cargo half, cargo full far from
Base, and the bank moment at Base. If those four images do not communicate the risk, the loop is
not readable and needs redesign before Phase 5.

## Phase 5 - Grafting and mutation

- `GraftResolver`: (enemy family × slot × weapon) → ability. Pure C#, unit-tested, in `Graft.Core`.
- Essence meter per enemy family; filling one offers a choice of grafts.
- Choice UI: pause, present 3 options, player picks slot.
- `SocketManager`: five rig anchors; chosen graft instantiates a prop onto its socket.
- Corruption: tint material, swell bone scale, self-damage tick past 85%.

**Screenshot gate:** the mutation ladder - the same character at 0%, 40%, 70%, 95% corruption,
side by side. **This is the hook. If those four images do not read as "becoming a monster",
the game does not have its selling point yet**, regardless of what the code does.

## Phase 6 - Art pass

Only now do the primitives get replaced. Because everything was built on sockets and prefab
references, this is a swap, not a rewrite.

- Swap capsules for KayKit / Quaternius characters (see `docs/ASSETS.md`).
- Retarget animations from Mixamo.
- Replace prop cubes with real horn / claw / spike meshes.
- Toon-shaded look (XP Hero ships **ToonyColorsPro**; URP's built-in toon or a hand-written
  toon shader gets close for free).

**Screenshot gate:** side-by-side, primitive build vs art build, same camera position.

---

## Sequencing rules

1. **One phase at a time.** No starting Phase N+1 before the Phase N screenshot is approved.
2. **Small files.** Single responsibility; if a file passes ~200 lines it probably wants splitting.
3. **Data in ScriptableObjects**, never hardcoded - enemy stats, biome configs, graft tables.
4. **`Graft.Core` never references UnityEngine.** It stays unit-testable headlessly, and it stays
   portable if the engine ever changes again.
5. **Commit at every phase boundary**, so any phase can be rolled back independently.

## Verification commands

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"

# Rules tests - fast, run constantly
"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testResults results.xml -logFile test.log

# Screenshot tour - NOTE: no -nographics, or you capture nothing
"$UNITY" -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testFilter ScreenshotTour -testResults tour.xml -logFile tour.log
```

Two traps already paid for on this machine, carried over from the deleted build's notes
(full list in the old project archive):

- `yield return new WaitForEndOfFrame()` **never resumes under `-batchmode`** and hangs until
  timeout with no error. Use `camera.Render()` + `yield return null`.
- Never build iOS bundles onto a cloud-synced folder (`~/Desktop` is FileProvider-backed here).
  codesign fails permanently with "resource fork / Finder information not allowed" and the
  attribute cannot be removed. Build to `~/Library/Caches/`.
