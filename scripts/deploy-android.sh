#!/bin/zsh
set -euo pipefail

# Rebuilds the Unity APK from the current source, verifies that it contains the
# Superpowers code, then uninstalls/installs/launches it on the connected Android device.
#
# Usage:
#   scripts/deploy-android.sh              # build + install + launch
#   scripts/deploy-android.sh --build-only # build + verify only

REPO_ROOT="${0:A:h:h}"
APK="$REPO_ROOT/Builds/Android/BattleForYourSoul.apk"
BUNDLE_ID="com.setzener.battleforyoursoul"
MENU_PATH="Tools/BattleForYourSoul/Build Android APK"
BUILD_ONLY=0

if [[ "${1:-}" == "--build-only" ]]; then
    BUILD_ONLY=1
fi

cd "$REPO_ROOT"

echo "[deploy-android] Rebuilding APK from current Unity source..."

BEFORE="$(stat -f %m "$APK" 2>/dev/null || echo 0)"

if unity command editor.invoke_menu --path "$MENU_PATH" --timeout 1800; then
    echo "[deploy-android] APK rebuilt by the running Unity Editor."
else
    echo "[deploy-android] Unity Editor not reachable; falling back to CLI build."
    unity build --profile Android \
        --android-export-type apk \
        --output "$APK" \
        --allow-dirty-build
fi

AFTER="$(stat -f %m "$APK" 2>/dev/null || echo 0)"
if [[ "$AFTER" -le "$BEFORE" ]]; then
    echo "[deploy-android] ERROR: APK was not rebuilt (timestamp did not change): $APK" >&2
    exit 1
fi

echo "[deploy-android] Verifying APK metadata..."

NEW_REFS="$(unzip -p "$APK" assets/bin/Data/Managed/Metadata/global-metadata.dat 2>/dev/null | strings | grep -c 'SuperpowerController' || true)"
OLD_REFS="$(unzip -p "$APK" assets/bin/Data/Managed/Metadata/global-metadata.dat 2>/dev/null | strings | grep -c 'WeaponController' || true)"

if [[ "$NEW_REFS" -eq 0 ]]; then
    echo "[deploy-android] ERROR: APK has no SuperpowerController references; it is stale." >&2
    exit 1
fi

if [[ "$OLD_REFS" -ne 0 ]]; then
    echo "[deploy-android] ERROR: APK still contains WeaponController references; it is stale." >&2
    exit 1
fi

echo "[deploy-android] APK verified: SuperpowerController=$NEW_REFS, WeaponController=$OLD_REFS"

if [[ "$BUILD_ONLY" -eq 1 ]]; then
    echo "[deploy-android] --build-only: skipping device install."
    exit 0
fi

ADB="$(command -v adb || true)"
if [[ -z "$ADB" ]]; then
    ADB="$HOME/Library/Android/sdk/platform-tools/adb"
fi

if [[ ! -x "$ADB" ]]; then
    echo "[deploy-android] ERROR: adb not found at $ADB" >&2
    exit 1
fi

DEVICE="$("$ADB" devices | awk 'NR > 1 && $2 == "device" { print $1 }')"
DEVICE_COUNT="$(printf '%s\n' "$DEVICE" | grep -c . || true)"

if [[ "$DEVICE_COUNT" -ne 1 ]]; then
    echo "[deploy-android] ERROR: expected exactly one connected Android device, found $DEVICE_COUNT." >&2
    echo "[deploy-android] Run '$ADB devices' and disconnect extras." >&2
    exit 1
fi

echo "[deploy-android] Installing on device $DEVICE..."
"$ADB" -s "$DEVICE" uninstall "$BUNDLE_ID" >/dev/null 2>&1 || true
"$ADB" -s "$DEVICE" install -r "$APK"
"$ADB" -s "$DEVICE" shell monkey -p "$BUNDLE_ID" -c android.intent.category.LAUNCHER 1 >/dev/null

echo "[deploy-android] Done. Launched $BUNDLE_ID on $DEVICE."
