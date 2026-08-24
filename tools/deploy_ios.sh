#!/usr/bin/env bash
#
# Export, build, install and launch GRAFT on a connected iPhone.
#
#   tools/deploy_ios.sh              # full run
#   tools/deploy_ios.sh --skip-unity # reuse the last Xcode export
#   tools/deploy_ios.sh --device <udid>
#   tools/deploy_ios.sh --no-launch
#
# Written from scratch for this project (decision D7). Every awkward line below is one of the
# traps in docs/GOTCHAS.md; the comments say which.

set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUNDLE_ID="com.handfooll.graft"

# ~/Library/Caches, never ~/Desktop. Desktop here is FileProvider-backed and synthesises a
# com.apple.FinderInfo attribute on every bundle directory that cannot be removed, after which
# codesign fails forever with "resource fork, Finder information, or similar detritus not allowed".
BUILD_ROOT="$HOME/Library/Caches/graft-ios"
XCODE_DIR="$BUILD_ROOT/Xcode"
DERIVED="$BUILD_ROOT/DerivedData"
LOGS="$BUILD_ROOT/logs"
APP_NAME="GRAFT.app"

SKIP_UNITY=0
LAUNCH=1
DEVICE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-unity) SKIP_UNITY=1; shift ;;
    --no-launch)  LAUNCH=0; shift ;;
    --device)     DEVICE="$2"; shift 2 ;;
    -h|--help)    sed -n '2,12p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done

mkdir -p "$LOGS"
step() { printf '\n\033[1m==> %s\033[0m\n' "$1"; }
die()  { printf '\033[31merror: %s\033[0m\n' "$1" >&2; exit 1; }

# ---------------------------------------------------------------- signing team

# Read the Team ID out of the installed signing certificate's OU rather than hardcoding it, so this
# script still works from a fresh checkout on another machine.
TEAM="$(security find-certificate -a -c "Apple Development" -p 2>/dev/null \
        | openssl x509 -noout -subject 2>/dev/null \
        | sed -n 's/.*OU=\([A-Z0-9]\{10\}\).*/\1/p' | head -1 || true)"
[[ -n "$TEAM" ]] || die "No 'Apple Development' signing certificate found. Open Xcode → Settings → Accounts and add your Apple ID."
echo "signing team: $TEAM"

# ---------------------------------------------------------------- device

pick_device() {
  xcrun devicectl list devices --json-output /dev/stdout --quiet 2>/dev/null \
    | python3 -c '
import json, sys
try:
    devices = json.load(sys.stdin)["result"]["devices"]
except Exception:
    sys.exit(1)
for d in devices:
    state = d.get("connectionProperties", {}).get("tunnelState")
    paired = d.get("connectionProperties", {}).get("pairingState")
    if paired == "paired" and state != "unavailable":
        print(d["identifier"], d.get("deviceProperties", {}).get("name", "?"))
        break
'
}

if [[ -z "$DEVICE" ]]; then
  read -r DEVICE DEVICE_NAME < <(pick_device) || true
  [[ -n "${DEVICE:-}" ]] || die "No paired iPhone available. Plug it in, unlock it, and trust this Mac."
  echo "device: ${DEVICE_NAME:-?} ($DEVICE)"
fi

# ---------------------------------------------------------------- 1. Unity export

if [[ "$SKIP_UNITY" -eq 0 ]]; then
  step "Exporting Xcode project from Unity"
  UNITY_LOG="$LOGS/unity.log"
  rm -rf "$XCODE_DIR"
  # Unity's exit status is trustworthy here; the log is kept for the error message.
  if ! "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
        -executeMethod Graft.Editor.IOSBuild.Export \
        -graftOutput "$XCODE_DIR" -graftTeam "$TEAM" \
        -logFile "$UNITY_LOG"; then
    grep -E "error CS|BuildFailedException|Error building" "$UNITY_LOG" | sort -u | head -20 || true
    die "Unity export failed. Full log: $UNITY_LOG"
  fi
  grep -o "\[GRAFT\].*" "$UNITY_LOG" || true
else
  step "Reusing existing Xcode project at $XCODE_DIR"
  [[ -d "$XCODE_DIR" ]] || die "Nothing to reuse - run without --skip-unity first."
fi

# ---------------------------------------------------------------- 2. xcodebuild

step "Building with Xcode"
BUILD_LOG="$LOGS/xcodebuild.log"

# -scheme, not -target. Unity 6 does generate a 'Unity-iPhone' scheme (older Unity did not, which
# is why docs/GOTCHAS.md says otherwise about earlier versions), and -derivedDataPath is only
# accepted alongside -scheme - with -target it fails outright.
#
# Nothing is passed as a command-line build setting. A setting given on the command line applies to
# EVERY target, so PRODUCT_BUNDLE_IDENTIFIER would be inherited by UnityFramework.framework and
# installd would reject the install with "parent bundle has the same identifier as sub-bundle".
# The team and bundle id are baked into the project by IOSBuild.Export instead.
set +e
xcodebuild \
  -project "$XCODE_DIR/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone \
  -configuration Release \
  -destination 'generic/platform=iOS' \
  -derivedDataPath "$DERIVED" \
  -allowProvisioningUpdates \
  build \
  > "$BUILD_LOG" 2>&1
XCODE_STATUS=$?
set -e

# xcodebuild's exit status is easy to lose, and a build can also fail while returning 0 through a
# pipe. Grep the log for the failure banner as well as checking the status.
if [[ $XCODE_STATUS -ne 0 ]] || grep -q "^\*\* BUILD FAILED \*\*" "$BUILD_LOG"; then
  grep -E "error:|BUILD FAILED" "$BUILD_LOG" | head -30 || true
  die "Xcode build failed. Full log: $BUILD_LOG"
fi

APP="$DERIVED/Build/Products/Release-iphoneos/$APP_NAME"
[[ -d "$APP" ]] || die "Build reported success but $APP does not exist."
echo "built: $APP"

# ---------------------------------------------------------------- 3. install

step "Installing on device"
xcrun devicectl device install app --device "$DEVICE" "$APP"

# ---------------------------------------------------------------- 4. launch

if [[ "$LAUNCH" -eq 1 ]]; then
  step "Launching"
  if ! xcrun devicectl device process launch --device "$DEVICE" "$BUNDLE_ID"; then
    cat <<'MSG'

Install succeeded but launch did not. On a first install this is expected and needs a human:

  Settings → General → VPN & Device Management → trust the developer certificate

Then run again, or just tap the app.
MSG
    exit 1
  fi
fi

cat <<MSG

Done. Two things about a free Apple account:
  - the app stops launching after about 7 days and must be reinstalled
  - only 3 sideloaded apps at a time
MSG
