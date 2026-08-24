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
  that needs to show UI has to switch the canvas to `ScreenSpaceCamera` first. Relevant from
  Phase 4, when the cargo bar has to appear in the gate images.
- **The editor renders at the "PC" quality level, iOS renders at "Mobile".** Captures force
  Mobile, or the gate images are judged on a render path the phone never uses.

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
- **Unity generates no `Unity-iPhone` scheme** - only `GameAssembly`. Build with
  `-target Unity-iPhone`, not `-scheme`.
- **`xcodebuild`'s exit status is easy to lose.** A wrapper whose last command is `tail` reports
  success over a failed build. Grep the log for `** BUILD FAILED **`.
- **A free Apple account stops the app launching after ~7 days** and allows 3 sideloaded apps.
- **First launch needs a human**: Settings → General → VPN & Device Management → trust the
  certificate. Until then `devicectl process launch` fails even though the install succeeded.
