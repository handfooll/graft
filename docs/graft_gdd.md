# GRAFT - Concept Design Document
*(working title - same arena-survivor idle-RPG DNA as XP Hero, different signature hook)*

## 1. One-line pitch

You don't level up by filling a bar. You level up by grafting pieces of the monsters you kill onto your own body - and every graft changes both what you can do and what you look like.

## 2. Pillars

Three things this game has to deliver on every run, in order of priority: your build has to feel like it emerged from *this specific run's* enemies rather than from a menu; your hero has to visibly become a monster over the course of a run, so power and horror grow together; and none of that should get in the way of the arcade-y, one-thumb, thirty-second-to-understand feel that makes this genre work on mobile in the first place.

## 3. Session structure (the two loops)

Like XP Hero, this runs on two nested loops. The **run loop** is a single 3–6 minute combat session in an arena: you fight waves, absorb traits, face a boss, and end with loot. The **meta loop** sits above it: between runs you spend currencies to permanently strengthen your roster, unlock new Vessels (see §5) and Relic Weapons (see §7), and push into new worlds gated by boss difficulty - the same idle-RPG scaffolding XP Hero uses, which is why this still feels satisfying to open every day. What's different lives entirely inside the run loop.

## 4. The signature hook: Absorption & Mutation ("Grafting")

Enemies don't drop generic XP orbs. When you kill something, it drops **Essence** shaped like a piece of that enemy - a stinger, a claw, a shard of hide, an eyeball. Essence auto-flies to you like XP orbs do in the genre, but it doesn't fill an abstract bar; it fills a meter tied to *that enemy type*. When the meter for a given enemy type is full, you're offered a **Graft**: a permanent-for-this-run upgrade built from that enemy's signature trait, and you choose which body slot to put it in.

Your hero has five graft slots - Head, Core, Left Arm, Right Arm, Legs - visible on screen at all times. A graft in the Head slot from a venom-spitter might give you a ranged poison-cone attack; the same enemy's graft in an Arm slot instead gives you a melee poison-claw swipe. So the *type* of enemy determines the *ability*, and the *slot* you choose determines *how it's delivered* - which is what makes the level-up choice meaningful rather than "pick the bigger number." Slots can be overwritten by a stronger graft later in the run, so late-run choices are genuinely a build decision (keep the graft that's carried you this far, or trade it for something new), not just an accumulation.

Grafting isn't free of consequence, which is the other half of the hook. Every graft nudges a **Purity meter** toward Corrupted. Low corruption keeps your hero mostly human and grants a small "focus" bonus (slightly faster graft-choice reroll, slightly better precision on aimed attacks). High corruption makes your hero visibly monstrous - bigger, uglier, scarier on screen - and grants raw power (flat damage and HP multipliers) at the cost of losing that focus bonus and, past a threshold, taking a slow self-damage tick that pushes you to end runs before you over-extend. This is what turns "always take the upgrade" (XP Hero's default) into a real tension: stay clean and controllable, or go monstrous and powerful but harder to pilot. It's the same emotional beat as a soulslike's "bonfire risk" or a roguelike's greed mechanic, just expressed through your own character model.

## 5. Does a hero-card collection system contradict this? (Vessels)

Short answer: not if the cards represent *bodies*, not *builds*. XP Hero's hero cards work because collecting and leveling them is a strong meta-progression hook - you don't want to throw that away. The fix is to reframe what a "hero card" *is*.

In this design, collectible heroes are called **Vessels**. A Vessel is the body grafts get applied to - it has a starting silhouette, a small passive identity trait (say, a Vessel with reptilian ancestry accepts poison/venom grafts more readily and gets a bonus from them; a construct-type Vessel resists corruption longer before going monstrous), and a permanent stat baseline from meta-progression (§6). Crucially, a Vessel has **no built-in kit or "class"** - it never comes with fixed abilities the way an XP Hero character card would. All actual combat abilities still come from grafting, every run, regardless of which Vessel you're using.

That framing is what avoids the contradiction: collecting Vessels is still a satisfying gacha/unlock loop (rarity tiers, new silhouettes, new affinities to chase), but it's collecting *hosts for the absorption fantasy*, not collecting *pre-made power*. It also adds strategic depth the original doesn't have - a run with the venom-affine Vessel plays differently from the same run with the construct Vessel, because the same enemy drops interact differently with each one, which gives you a reason to own and level more than one Vessel instead of just chasing "the best" hero card.

## 6. Do stat upgrades (power, HP, regen, attack speed…) still exist?

Yes - kept deliberately, because "number go up" between runs is a big part of why this genre is replayable on mobile, and there's no reason grafting has to remove it. It just now comes from two distinct places instead of one:

**Vessel Mastery** is the permanent, between-runs layer - spend soft currency and Vessel Shards on a stat tree per Vessel: Power, Max HP, HP Regen, Attack Speed, Crit Chance, Move Speed, Pickup Radius, and Corruption Resistance (a new stat that raises how many grafts you can take before going fully monstrous). This is the direct analog of XP Hero's upgrade system and should feel just as satisfying to dump currency into.

**In-run graft stats** are temporary and tied to what you absorbed this run: many grafts carry a passive stat bump alongside their active ability (a hide-graft might be "+12% Max HP" with no active effect at all, a fast-enemy graft might be "+8% Attack Speed" alongside its dash). These reset every run, same as XP Hero's in-run power spikes, but here they're flavored by *which* monsters you killed rather than being a flat generic bonus - so two runs with identical Vessel Mastery can still feel different depending on what you fought early.

## 7. Weapons: obtaining, merging, and leveling

Also kept, and it slots in cleanly as the *chassis* grafts attach to rather than something that competes with grafting. **Relic Weapons** (melee and ranged, same split as XP Hero) are obtained from run rewards, the shop, and gacha pulls, each with a rarity tier. Duplicate weapons of the same tier merge into the next tier up, exactly as you asked - same recognizable "collect three, merge to one better one" pattern most players already know from merge games, so it needs no new tutorialization. Merged weapons can then be leveled with a dedicated currency (Weapon Cores) for flat stat growth, independent of tier.

The tie-in to grafting is what makes this feel native to the new hook rather than bolted on: your equipped weapon determines the *shape* of your graft abilities. A claw-graft on a bladed weapon becomes an extended melee slash; the same claw-graft on a firearm becomes a spread of thrown projectiles. So weapon choice and graft choice compound on each other - a build isn't just "which grafts did I get," it's "which grafts did I get, on which Vessel, wielding which weapon" - three collectible systems (Vessels, Relic Weapons, Grafts) that each reinforce the central fantasy instead of sitting next to it as an unrelated system, which is where XP Hero's weapon system currently feels a bit disconnected from its leveling.

## 8. Worlds, bosses, and currencies

World and boss-gate structure carries over close to as-is - multiple themed zones, each ending in a boss with a real attack pattern rather than a stat check, unlocking the next zone and a new tier of Essence/enemy types (which matters more here than in XP Hero, since new enemy types directly mean new graft options, not just new reskinned mobs).

| Currency | Earned from | Spent on |
|---|---|---|
| Soft Gold | every run | Vessel Mastery stat tree |
| Weapon Cores | runs, daily quests | leveling Relic Weapons |
| Vessel Shards | boss clears, gacha | unlocking/upgrading Vessels |
| Prime Essence (premium) | IAP, rare drops | gacha pulls, reroll graft choices |

## 9. Monetization sketch

Rewarded-ad graft rerolls (skip a bad graft choice once per level-up, or grants a bonus pull between waves) map well onto this system since the graft-choice moment is the natural place a player wants a do-over - arguably a better ad-placement fit than XP Hero's post-boss interstitial. Gacha for Vessels and Relic Weapons follows the same soft/premium currency shape XP Hero already uses successfully, so this doesn't require inventing a new monetization model, just re-pointing the existing one at three collectible tracks instead of one.

## 10. How this differs from XP Hero, explicitly

The arena-survivor combat, the boss-gated worlds, the collectible-hero meta layer, and the merge-to-upgrade weapon system are the same genre DNA XP Hero uses (and which XP Hero itself borrowed from earlier genres). What changes is the thing players will actually describe when they talk about the game: leveling up is not "fill a bar, get a random-ish upgrade," it's "choose what to graft from what you just killed, and watch your hero mutate." That single change cascades into new stakes (the Purity/Corruption trade-off), a new reason enemy variety matters (new enemies = new grafts, not just new reskins), and a new reason the three collectible systems reinforce each other instead of sitting side by side.

## 11. Open questions for you to decide next

A few decisions are genuinely yours to make before this goes further: whether Corruption should be run-only (resets each run, purely a risk/reward pacing tool) or should also leave a small permanent mark on a Vessel across many runs (a rogue-lite meta-corruption layer, more ambitious but riskier to balance); how many simultaneous graft "recipes" per enemy type you want at launch (a small curated set per enemy is easier to balance and market than fully generative combos); and whether the Head/Core/Arms/Legs slot visualization is worth the art cost of visibly mutating character sprites per graft, versus a cheaper palette/particle-effect version of the same idea for a first prototype.
