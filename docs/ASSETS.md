# Where the 3D models and animations come from

## The honest answer about what I can and cannot make

**I cannot author good 3D character models.** Not a modesty thing - sculpting, retopologising,
UV-unwrapping, rigging and hand-animating a creature is not something I can do well, and pretending
otherwise would cost you a week before you found out.

What I *can* do:

| Task | Me? |
|---|---|
| All the C# - movement, combat, grafting, sockets, UI, save | ✅ yes |
| Wire imported models into prefabs, sockets, animator controllers | ✅ yes |
| Generate **simple geometric props** procedurally (horns, spikes, shards, crystals) via a Blender Python script | ✅ yes, genuinely |
| Shaders - toon ramp, corruption tint, dissolve | ✅ yes |
| Rigged, animated humanoid or creature meshes | ❌ **no - use the packs below** |
| Texture painting, sculpting | ❌ no |

The plan is built around that split. Phases 0–5 use **primitives**, so none of it is blocked on art.
Art lands in Phase 6 as a swap.

## Characters and monsters - free, CC0, no attribution required

**1. KayKit - kaylousberg.itch.io** ← start here
Low-poly, rigged, animated, consistent art style, explicitly built for top-down games.
Free packs: Adventurers, Skeletons, Dungeon, Restaurant, City Builder.
Character packs come with a shared skeleton, so animations retarget between them cleanly.

**2. Quaternius - quaternius.com** ← best monster variety
Huge CC0 library. Ultimate Monsters, Ultimate Animated Animals, RPG characters, nature, dungeons.
Many are pre-animated. This is where your five biomes' enemy families come from.

**3. Kenney - kenney.nl**
CC0, enormous, but mostly **props and environment** rather than rigged characters.
Use for: terrain tiles, rocks, trees, crates, the Base buildings.

## Animations

**Mixamo - mixamo.com.** Free with an Adobe account. Two things it does:
1. **Auto-rigger** - upload an un-rigged humanoid mesh, get back a rigged one.
2. **Animation library** - thousands of mocap clips (walk, run, attack, hit, die), retargetable.

For a top-down game you need maybe six clips: idle, walk, run, attack, hit-react, die.
Download as FBX **without skin** once you have the mesh, and share one Animator Controller.

## Graft props - the part I can actually build for you

The horns, claws, spikes and crystals that snap onto sockets are **small, non-rigged, hard-surface
meshes**. That is exactly what a script can generate well. If you install Blender, I can write a
Python script that generates a parameterised family of them - a horn that varies in curl and
length, spikes that vary in count - and exports them as FBX ready for Unity.

That gives visual variety across dozens of grafts without you modelling anything, and it means
graft props can be **generated to match** rather than scavenged from mismatched packs.

Blender is a free ~1GB install (`brew install --cask blender`). Not needed until Phase 6.

## What NOT to do

The extracted `XPHero.app` in Downloads is **reference only** - for studying how they structured a
shipped game, which is legitimate. Its art assets are Supercent's copyrighted property. Do not
extract them into this project; that turns a portfolio piece into something you can never publish.
Everything in this file is CC0 or free-licensed and carries no such problem.

## Practical order

1. Phases 0–5: **primitives only.** Do not download anything. Do not get distracted by asset packs.
2. Phase 6: grab KayKit Adventurers (player) + Quaternius Ultimate Monsters (enemies).
3. Mixamo for the six animation clips.
4. Blender script for graft props, if the pack props are not enough.
