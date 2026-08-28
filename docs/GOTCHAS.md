# Traps already paid for on this machine

Facts about **Unity, Xcode and this Mac** - not about any previous project, and not approaches
to copy. Each one cost real time to find and none of them announce themselves: every trap below
fails silently, succeeds misleadingly, or reports the wrong thing.

Add to this file whenever something costs more than about twenty minutes to diagnose.

## Headless Unity

- **`yield return new WaitForEndOfFrame()` never resumes under `-batchmode`.** It hangs until the
  timeout with no error message at all. Use `camera.Render()` + `yield return null`.
- **The screenshot tour must run WITHOUT `-nographics`**, or every capture comes out empty.
- **`camera.aspect` is the editor window's aspect until a target texture is assigned.** Compute
  framing maths from the capture's own dimensions instead. Portrait is where this bites: at 9:16
  the horizontal field is far narrower than the vertical FOV suggests.
- **A `ScreenSpaceOverlay` canvas does not render into a camera's target texture.** Any capture
  that needs to show UI has to switch the canvas to `ScreenSpaceCamera` first - and switch it
  *after* assigning the target texture, because the canvas sizes itself to the camera's viewport
  and the viewport is only the texture once the texture is assigned. `Screenshot.Capture` borrows
  and returns overlay canvases around each render, so Overlay stays the path the phone runs.
- **The editor renders at the "PC" quality level, iOS renders at "Mobile".** Captures force
  Mobile, or the gate images are judged on a render path the phone never uses.

## Determinism in captures

- **Headless play mode runs uncapped, so "wait 1.2 seconds" is a different number of frames every
  run.** Anything simulated over that span - NavMesh steps, spawn ticks, avoidance - lands somewhere
  slightly different each time, and the gate images stop being reproducible. Two Phase 3 captures
  drifted **8.2%** and **2.4%** of their pixels between consecutive runs of identical code. Set
  `Time.captureDeltaTime` (the tour uses 1/60) so every frame advances the clock by exactly the same
  amount, and `Random.InitState` so anything drawing a random number draws the same one.
  Found by the baseline check, which is the only reason anyone knew.
- **`Screenshots/` is gitignored because every image is reproducible - so reproducibility has to
  actually be true.** The approved baselines live in `Tests/GateBaselines/` and *are* committed.

## Disk and worktrees

- **Clone `Library/` into a new worktree with `cp -Rc`, not a fresh import.** On APFS that is a
  copy-on-write clone: 2.5 GB, instant, and **zero additional disk used**. A fresh Unity import of
  the same project takes minutes and really does cost the gigabytes. Matters here because the disk
  has been sitting above 90% since August.

## Cameras and framing

- **The phone is not 9:16.** An iPhone 14 Pro is 1179x2556 - aspect **0.461**, not 0.5625. A
  720x1280 capture is 22% wider in aspect than the device, so every framing judgement made from one
  is made about a screen that does not exist, and always errs toward "there is more room than there
  is". Captures are 720x1560, and `CameraFraming.DeviceAspect` is the single place that number
  lives.
- **Unity's field of view is vertical.** In portrait a wide one is enormously tall: at 45° and 26 m
  the camera bought only 12 m of screen *width* and leant everything near the top and bottom of
  frame outward. Reason about the short axis; the number Unity shows you is the long one.
- **Measure a reference screenshot, do not eyeball it.** Camera pitch falls straight out of any
  circle lying on the ground: on-screen `height / width = sin(pitch)`. Use **centre-to-centre**
  across a ring of objects, not the outer silhouette - a uniform height offset then cancels, so
  stones or props standing up do not bias it. XP Hero's fire pit gives 47.5°.
- **A top-down camera foreshortens the body but not the head.** A sphere projects as a circle at
  any pitch, so `apparent heads-tall = built heads-tall x cos(pitch)` - every character reads more
  chibi on screen than it was modelled. At 47.5 deg that is a x0.676 error, enough to pick the wrong
  silhouette entirely if you judge a model sheet instead of a render. Judge through the game camera,
  and convert before comparing against a reference screenshot.
- **Compare zoom in character-widths across the screen, not in metres or screen percentages.** It
  is the only scale-free measure, and it survives not knowing the reference's world units. It also
  separates two things that are easy to confuse: how far the camera is, and how the character is
  *proportioned*. A squat chibi and a 2:1 capsule at the identical camera do not read alike.
- **A two-tone tile scatter reads as a checkerboard, not as texture,** if the tile is anywhere
  near the size of the visible frame. At 4 m tiles under a camera that sees 9.5 m, even a 10%
  value difference reads as painted squares. The unit the world is *designed* in and the unit the
  ground is *drawn* in have to be different numbers - halving the draw size is enough to turn the
  grid into clusters.
- **A tall boundary wall must not cast shadows.** At 5.5 m under a 38-degree sun it throws seven
  metres of shadow back across the ground *inside* the world, so the entire rim of the map renders
  as a black trench and the biome it is supposed to edge cannot be seen at all.
- **Emissive materials tonemap to white long before you expect.** An emission colour at 2.2x came
  out as stage lighting; the hue is the signal, not the intensity, and about 1.25x keeps it.
- **Framing tuned against marker cubes answers the wrong question.** "How much warning do I get"
  and "is the character readable" are different questions with different answers, and a third -
  "is a *change* to the character readable" - decides more than either, because DESIGN.md's hook is
  a visual on the player's body. Put the real character, approach markers and stand-in props in
  every framing sweep image (`CameraFramingSweep` does).

## NavMesh

- **`NavMeshCollectGeometry` is in `UnityEngine.AI`, not `Unity.AI.Navigation`** - even though
  `NavMeshSurface` and `CollectObjects`, which you use in the same three lines, are in the package
  namespace. The error names the type and not the namespace, so it reads like a missing package
  reference.
- **`NavMeshSurface.BuildNavMesh()` from a script bakes into memory and nothing else.** The scene
  saves clean, the component looks configured, and the data is gone on the next load - and in a
  player build it never existed. Persist it explicitly:
  `AssetDatabase.CreateAsset(surface.navMeshData, path)` after the bake.
- **Bake from `PhysicsColliders`, not the default `RenderMeshes`,** if any decoration deliberately
  has no collider. Flat plates, pools and paving are things the player walks straight over; baked
  from render meshes each one punches a hole in the NavMesh that nothing reports.

## ScriptableObjects

- **Defaults in the class only ever apply once.** A `[CreateAssetMenu]` default field value is used
  the moment the `.asset` is created and never again - after that the serialised file wins, and
  editing the defaults in code changes nothing at all with no error. If an asset's defaults *are*
  the design (`WorldLayout` is), the asset has to be deleted to pick a change up. It looks exactly
  like an edit that did not compile.

## Editor asset creation

- **`AssetDatabase.CreateAsset` part-way through a scene build does not reach disk.** Creating
  materials between `EditorSceneManager.NewScene` and `SaveScene` produces no `.mat` file, and
  every reference to one serialises as `{fileID: 0}`. Nothing throws, the scene saves, and
  `ConfigureAll` exits zero - the first sign is a fight rendered entirely in default white.
  Create project assets **before** the scene build and `SaveAssets()` immediately.
  This hid for a whole phase because the world's materials were all already on disk from the run
  that first made them, so `GetOrCreate` never took the create path again.

## Primitives

- **A `Quad` is one-sided and it is culled from behind.** Whether a camera-facing quad is visible
  at all comes down to which way Unity's quad mesh points once it has been through the camera's
  rotation plus whatever correction was applied - and getting it backwards throws nothing, logs
  nothing, and renders nothing, which is indistinguishable from never having built the object.
  For anything small and always-facing (health bars), use a **flattened `Cube`**: it has no wrong
  side and at 2 cm thick it looks identical under a camera that cannot move.
- **Emission is added on top of the lit result, not blended with it.** With a directional light at
  1.9, an emissive material at strength 1.25 is being multiplied by roughly 3, so any channel above
  ~0.3 clips and the object tonemaps toward white. A saturated red bar needs its *green and blue*
  kept low - near-zero - and lets red clip on its own. Raising the red does nothing; lowering the
  other two is the whole fix. (Same trap as Ember Scar's vents, from the other direction.)

## iOS runtime

- **Unity defaults iOS to 30 fps**, silently. Any judgement about how movement feels made against a
  default build is being made through 33 ms of latency the shipped game would not have. Set
  `Application.targetFrameRate` (see `Graft.App.FrameRate`).
- **ProMotion is opt-in.** Without `CADisableMinimumFrameDuration` in Info.plist, iOS caps the app
  at 60 Hz no matter what `targetFrameRate` says, and nothing reports it. Set from a
  `[PostProcessBuild]` callback.

## Unity 6 specifics

- **`NamedBuildTarget` lives in `UnityEditor.Build`**, not `UnityEditor`. Easy to miss because
  most of `PlayerSettings` needs no extra `using`.
- **The `Arial.ttf` builtin font is gone.** It is `LegacyRuntime.ttf` now.
- **TextMeshPro needs its Essential Resources imported through the editor GUI** before it renders
  anything, which means a fresh clone shows no text. `Resources.GetBuiltinResource<Font>` has no
  import step and cannot go missing.
- **`FindAnyObjectByType<T>()` skips inactive objects.** Anything that starts disabled needs
  `FindObjectsInactive.Include`.
- **Package versions from the bundled project templates can be too old to compile.** The 3D
  template pins packages whose APIs this editor marks obsolete-*as-error*. The editor's own
  recommended set is in
  `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Resources/PackageManager/Editor/manifest.json`.

## uGUI

- **An `Image` with `type = Filled` and no `sprite` silently ignores `fillAmount`** and draws at
  full extent. No error, no warning - every bar in the game reads as permanently full. Assign a
  sprite to any filled or radial image.
- **A fresh `RectTransform` carries `sizeDelta = (100,100)`.** Give it stretch anchors without
  zeroing that and it ends up 100 units *wider* than its parent, clipping both edges.
- **A sprite generated at runtime cannot be saved into a scene.** It needs `HideAndDontSave` to
  avoid leaking, and that flag is exactly what stops serialisation - so the editor builds the
  `Image` components and the component assigns their sprites in `Awake`. An `Image` with no sprite
  is invisible, which reads as "the code is broken" rather than "the asset is missing".

## URP

- **Shadow distance is measured from the camera and must be sized to the camera.** Pull back past
  it and every shadow in the frame disappears, with no error. Derive it; never hardcode it.
- **Shadow cascades leave a visible diagonal seam on flat ground.** A fixed-height top-down camera
  has no long view to spread cascades over, so one cascade is both cheaper and cleaner.
- **URP's specular lobe is wide at low smoothness**, and across a floor the size of a biome that
  reads as a soft diagonal wash easily mistaken for a terrain seam. Flat-colour surfaces want
  `_SPECULARHIGHLIGHTS_OFF`.

## iOS device builds

- **Never build bundles onto a cloud-synced (FileProvider) volume.** `~/Desktop` here is
  FileProvider-backed and *synthesises* a `com.apple.FinderInfo` attribute on every bundle
  directory carrying the "has bundle" bit. It cannot be removed - `xattr -d` answers
  "No such xattr" while `xattr -l` still lists it - and codesign then fails forever with
  *"resource fork, Finder information, or similar detritus not allowed"*. A plain `mkdir` does
  not reproduce it, only real bundles do, which makes it easy to misdiagnose. Build to
  `~/Library/Caches/`, where `xattr -cr` genuinely works.
- **Xcode sets that bit itself while creating each bundle**, then signs in the same build, so
  stripping beforehand cannot help.
- **Never pass `PRODUCT_BUNDLE_IDENTIFIER` on the `xcodebuild` command line.** A command-line
  build setting applies to *every* target, so `UnityFramework.framework` inherits the app's id
  and installd rejects the install: *"parent bundle has the same identifier as sub-bundle"*.
- **`-derivedDataPath` is rejected unless `-scheme` is given.** With `-target` it fails outright:
  *"The flag -scheme, -testProductsPath, or -xctestrun is required when specifying
  -derivedDataPath."*
- **Unity 6 DOES generate a `Unity-iPhone` scheme.** Older Unity generated only `GameAssembly`,
  which is where the advice to use `-target Unity-iPhone` comes from - it is wrong here, and
  combined with the line above it is a dead end. Verified on 6000.5.8f1 with
  `xcodebuild -project ... -list`: schemes are `GameAssembly`, `Unity-iPhone`, `UnityFramework`.
- **`xcodebuild`'s exit status is easy to lose.** A wrapper whose last command is `tail` reports
  success over a failed build. Grep the log for `** BUILD FAILED **`.
- **A free Apple account stops the app launching after ~7 days** and allows 3 sideloaded apps.
- **First launch needs a human**: Settings → General → VPN & Device Management → trust the
  certificate. Until then `devicectl process launch` fails even though the install succeeded.
