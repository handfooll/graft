#!/usr/bin/env bash
# Build GRAFT for iOS, sign it, and install it on a connected device.
#
#   ./scripts/build_ios.sh              # full rebuild (regenerates the Xcode project)
#   ./scripts/build_ios.sh --no-unity   # skip Unity, just recompile + reinstall
#
# Why signing is done by hand instead of letting xcodebuild do it:
# Xcode stamps every bundle directory it creates (.app, .framework, .storyboardc) with
# com.apple.FinderInfo carrying the "has bundle" bit (0x20 at offset 8). codesign then
# refuses to sign it -- "resource fork, Finder information, or similar detritus not allowed".
# Because Xcode sets the attribute and signs in the same build, stripping beforehand cannot
# help. Building unsigned, stripping, then signing gives a point where nothing re-adds it.
set -euo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEAM="6WR7SYV5L7"
BUNDLE="com.handfooll.graft"
IDENTITY="${GRAFT_SIGN_IDENTITY:-EB5B899FC60A527AEBC96E2F75338F923063E917}"
PROFILE="${GRAFT_PROFILE:-$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles/e6d53a12-1073-4e5f-a0cd-5b4b5ecb4058.mobileprovision}"
DEVICE_UDID="${GRAFT_DEVICE_UDID:-00008120-001605DA0253C01E}"

# Build products MUST live outside the FileProvider-backed (cloud-synced) volume that
# ~/Desktop sits on. That filesystem synthesises an undeletable com.apple.FinderInfo on every
# bundle directory -- "xattr -d" answers "No such xattr" while "xattr -l" still lists it -- and
# codesign then refuses the bundle forever. On a normal volume xattr -cr genuinely clears it.
PRODUCTS="${GRAFT_BUILD_DIR:-$HOME/Library/Caches/GraftBuild/products}"
APP="$PRODUCTS/GRAFT.app"

cd "$ROOT"
mkdir -p "$ROOT/build" "$PRODUCTS"

if [[ "${1:-}" != "--no-unity" ]]; then
  echo "==> Unity: generating Xcode project"
  "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
    -executeMethod Graft.EditorTools.BuildIOS.Build -logFile "$ROOT/build/unity_ios.log"
fi

# Do NOT pass PRODUCT_BUNDLE_IDENTIFIER here: an xcodebuild command-line setting applies to
# every target, so UnityFramework.framework would get the app's identifier too and installd
# rejects the app ("parent bundle has the same identifier as sub-bundle"). Unity already
# writes the correct per-target identifiers into the generated project.
echo "==> xcodebuild: compiling (unsigned)"
xcodebuild -project "$ROOT/build/ios/Unity-iPhone.xcodeproj" \
  -target Unity-iPhone -configuration Debug \
  -sdk iphoneos -arch arm64 \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" \
  CONFIGURATION_BUILD_DIR="$PRODUCTS" \
  build

[[ -d "$APP" ]] || { echo "No app produced at $APP"; exit 1; }

echo "==> Stripping extended attributes"
xattr -cr "$APP"

echo "==> Embedding provisioning profile"
cp "$PROFILE" "$APP/embedded.mobileprovision"

echo "==> Extracting entitlements"
ENT="$ROOT/build/entitlements.plist"
security cms -D -i "$PROFILE" > "$ROOT/build/profile.plist"
/usr/libexec/PlistBuddy -x -c "Print :Entitlements" "$ROOT/build/profile.plist" > "$ENT"

echo "==> Signing nested code (innermost first)"
# Frameworks and dylibs must be signed before the app that contains them.
find "$APP" \( -name "*.framework" -o -name "*.dylib" -o -name "*.appex" \) -print0 |
  while IFS= read -r -d '' item; do
    xattr -cr "$item"
    codesign --force --sign "$IDENTITY" --timestamp=none "$item"
  done

echo "==> Signing the app"
xattr -cr "$APP"
codesign --force --sign "$IDENTITY" --timestamp=none \
  --entitlements "$ENT" --generate-entitlement-der "$APP"

echo "==> Verifying signature"
codesign --verify --deep --strict --verbose=2 "$APP"

echo "==> Installing on $DEVICE_UDID"
xcrun devicectl device install app --device "$DEVICE_UDID" "$APP"

echo "==> Launching"
xcrun devicectl device process launch --device "$DEVICE_UDID" "$BUNDLE" ||
  echo "Launch call failed -- tap the GRAFT icon on the device instead."

echo "==> Done"
