# What the real XP Hero build tells us

Source: `~/Downloads/Payload 2/XPHero.app` - a shipped iOS build, `io.supercent.weaponrpg`
v25.2.0, 545 MB. Findings from its Unity data and IL2CPP metadata. **Reference only** - see the
copyright note in `ASSETS.md`.

## Engine and stack - verified, not guessed

| Fact | Value | Why it matters to us |
|---|---|---|
| Unity version | **2022.3.76f1 LTS** | A shipping mobile team chose LTS, not the newest. We are on Unity 6 because it is already installed; if we hit engine bugs, this is the fallback. |
| Navigation | `Unity.AI.Navigation` - `NavMeshSurface`, `NavMeshModifier`, `NavMeshModifierVolume`, baked NavMesh data | **Confirms 3D**, and confirms baked NavMesh is the right call for our enemies. |
| Camera | **Cinemachine** | Use it. Don't hand-roll a follow camera. |
| Shading | **ToonyColorsPro** (paid asset) | Their look is toon-shaded. A hand-written toon shader or URP toon gets us close for free. |
| VFX | AllIn1Vfx, KinoBloom | Bloom does a lot of the heavy lifting on their "juice". |
| UI | Coffee SoftMask / UIEffect / UIParticle, Febucci TextAnimator | Animated damage numbers are an asset, not custom code. |
| Haptics | Lofelt NiceVibrations | Cheap game-feel win on mobile. |
| Backend | Firebase Analytics / Auth / Crashlytics / Firestore / **RemoteConfig** | RemoteConfig = they tune balance live after launch without shipping a build. |
| Monetisation | AppLovin MAX, GoogleMobileAds, Tapjoy, Unity IAP, AppsFlyer | The whole mediation stack. Not our problem yet. |
| Debug | LunarConsole | In-game log console on device. Worth copying eventually. |

## Scene architecture - the most useful finding

The entire game ships **8 scenes** (`level0`–`level7`), and only one is large (`level3`, 2.5 MB).
There is an `AssetBundleManager`, and `resources.assets` is 143 MB.

**Conclusion: they do not use a scene per world.** One gameplay scene, content streamed in as
prefabs/bundles at runtime.

This directly contradicts the common advice to make a scene per biome. **Follow XP Hero here:**
one gameplay scene, biomes as content loaded into it. Cheaper transitions, and it is what makes
a genuinely continuous world possible.

## Cargo - they already built your extraction loop

Recovered class and field names:

```
CargoAmount      CargoFillRate    CargoMaxed       CargoIsEmpty
CargoBarUI       CargoController  CargoData        CargoStatCalculator
CargoItemList    CargoStack       CargoStackTime   CargoGold
CargoFullOffer   CargoFullEvent   CargoMultSubscription
```

Plus a log line: `"CargoFull detected (non-combat). Starting 2s delay before offer..."`

So: cargo fills → full is an **event** → the game waits until you are **out of combat** → then
makes an offer. Note the restraint - they deliberately do not interrupt a fight.

**Take from this:** carry-capacity-then-bank is proven in this genre. And the "wait for non-combat
before interrupting the player" rule is good design worth copying regardless of monetisation.

## Other systems worth knowing about

- **Plunder** - `PlunderOfflineMinutePerOnce`, `PlunderDailyMaxCount`, `PlunderedResources`,
  `PlunderedByPoint`. Asynchronous offline raiding of other players. Deep meta layer; ignore for now.
- **BossRaid** - an enormous subsystem (deck building, camps, ally spawners, per-boss cameras,
  participant stats). Much bigger than "a boss at the end of a run".
- **Enemy naming** - `AnubisExplosiveMummyController`, `CerberusWarningAreaDetector`. Mythology
  themes per world, and **"WarningAreaDetector" means telegraphed attacks** - the player is shown
  where damage will land before it lands. Copy this; it is why their bosses feel fair.
- **ABTestConfig**, `AdOptimizerConfig` - everything is A/B tested and remotely tuned.

## The honest scale check

XP Hero is a live-ops product with ~160 assemblies, a full ad mediation stack, offline PvP, boss
raids, remote config and A/B testing. It represents many developer-years.

`docs/BUILD_PLAN.md` targets the **combat and extraction core only** - correctly. The value of this
reference is architectural (one scene, NavMesh, Cinemachine, cargo, telegraphed attacks), not a
feature checklist to race through.
