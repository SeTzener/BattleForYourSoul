# Deployment Guide — BattleForYourSoul

This is the repeatable deployment procedure for this project. Follow it from the current source branch; do not deploy from stale generated folders in `Builds/`.

## Golden rules

1. **Deploy from current Unity source**, not from a stale generated project. `Builds/` is regenerated, not preserved.
2. `Builds/` is git-ignored. Switching branches does **not** update it. Regenerate it on every deploy.
3. `Builds/` holds **one latest build per platform only** (see [Clean layout](#clean-layout)). Do not keep numbered/date-suffixed copies.
4. **Quit Unity before using `unity build` on the command line.** The CLI refuses to run while the Editor has the project open.
5. For iOS, use **Tools → BattleForYourSoul → Build iOS Xcode Project**. It deletes and recreates `Builds/iOS/BattleForYourSoul_Latest`, so Xcode can never reuse an old IL2CPP project.
6. Keep Xcode derived data **outside `Builds/`** (for example `/private/var/folders/.../opencode/BattleForYourSoul_iOS_DerivedData`). Never put a `DerivedData*` folder in `Builds/` — it is a multi-GB cache, not a build artifact.
7. **Verify device IDs every time.** Xcode and `devicectl` use different identifiers for the same iPhone.

## Clean layout

After a successful deploy, `Builds/` should look like this and nothing more:

```text
Builds/
├── Android/
│   └── BattleForYourSoul.apk
├── AndroidStudio_Gradle/
│   └── Gradle project source only (no build/, .gradle/, .idea/)
└── iOS/
    ├── BattleForYourSoul_Latest/     ← fresh Unity Xcode export
    │   └── Unity-iPhone.xcodeproj
    └── BattleForYourSoul.app         ← final signed app, copied here after xcodebuild
```

Anything else — old `AndroidStudio/`, `Il2CppBackup/`, stale `iOS/BattleForYourSoul/`, `DerivedData*`, `*BackUpThisFolder*` — is stale and should be deleted.

Current known values for this Mac (verify, do not assume):

| Item | Value |
|---|---|
| Unity | `6000.6.9f1` |
| Xcode | `26.2` |
| Branch used for current work | `feature/superstrength` |
| Android bundle ID | `com.setzener.battleforyoursoul` |
| iOS bundle ID | `com.DefaultCompany.BattleForYourSoul` (no `iPhone:` override in `ProjectSettings.asset`) |
| Apple Development team | `XRC9NUZN4G` |
| iPhone name | `iPhone di Tano` |
| Xcode destination ID (changes) | `00008140-001A299A2613001C` |
| `devicectl` CoreDevice ID (changes) | `B8D3F2E1-EF92-5D77-8E2F-529A1BC91040` |

---

## One-time device setup

### Android

1. On the phone: **Settings → About phone → tap Build number 7 times** to enable Developer options.
2. **Developer options → USB debugging** = on.
3. Connect by USB and accept the RSA prompt.
4. Verify:

   ```sh
   adb devices
   ```

   If `adb` is not on your PATH:

   ```sh
   ~/Library/Android/sdk/platform-tools/adb devices
   ```

### iOS

1. Join the Apple Development team `XRC9NUZN4G` in Xcode.
2. Have a valid **Apple Development** signing certificate:

   ```sh
   security find-identity -v -p codesigning
   ```

3. Connect the iPhone by USB and unlock it.
4. After the first developer-app install, trust the certificate on the phone:
   **Settings → General → VPN & Device Management → trust the developer**.

---

# Android deployment

## Option A — Android Studio Run hook (recommended for iteration)

This makes the Android Studio **Run ▶** button rebuild the Unity APK from the current branch and install it. It avoids the stale-APK problem entirely.

### One-time setup

1. In Unity, run **Tools → BattleForYourSoul → Export Android Gradle Project**.
   This creates a real Gradle project at:
   ```text
   Builds/AndroidStudio_Gradle
   ```
   Delete `build/`, `.gradle/` and `.idea/` from that folder if they appear — they are caches, not source.
2. Open that folder in Android Studio, not the Unity source root and not any old `Builds/AndroidStudio*` folder.
3. The root `build.gradle` contains a task named `installUnityApk`. Select that task in the Android Studio Run configuration:
   - **Run → Edit Configurations → `+` → Gradle**
   - Gradle project: `Builds/AndroidStudio_Gradle`
   - Task: `installUnityApk`
4. Ignore the Android Studio parallel-sync prompt, or choose **Enable parallel sync for this project**. It is unrelated to the game build.

### Every deploy

Press **Run ▶**.

The `installUnityApk` task runs:

```text
scripts/deploy-android.sh
```

which:

1. Calls the running Unity Editor menu **Tools → BattleForYourSoul → Build Android APK**.
   If no Editor is reachable, it falls back to `unity build` and Unity must be closed.
2. Writes a fresh APK to `Builds/Android/BattleForYourSoul.apk`.
3. Verifies the APK contains `SuperpowerController` and no `WeaponController`.
4. Uninstalls the old app, installs the fresh APK, and launches it.

You can test the build/verification part without a phone:

```sh
scripts/deploy-android.sh --build-only
```

> Unity can overwrite the generated Gradle files when you re-export the Gradle project. If `installUnityApk` disappears from `Builds/AndroidStudio_Gradle/build.gradle`, re-add the task snippet from the end of that file. The heavy logic lives in `scripts/deploy-android.sh`, which is tracked and survives re-export.

## Option B — Unity GUI, direct APK

Unity 6 uses **File → Build Profiles**; there is no `File → Build Settings` menu in this version.

1. Open the project in Unity on the branch you want to deploy.
2. **File → Build Profiles**.
3. Select the existing **Android** profile.
4. In the profile **Platform Settings**:
   - **Export Project** = **off**. This is the checkbox that sends the build to Android Studio.
   - **Build App Bundle (Google Play)** = **off**. It only appears once **Export Project** is off.

   With both of those off, Unity builds an `.apk`. There is no separate “Export Type: APK” field in Unity 6.
5. If needed, click **Switch Platform**.
6. Click **Build**.
7. Save as:

   ```text
   Builds/Android/BattleForYourSoul.apk
   ```

8. Install:

   ```sh
   adb devices
   adb install -r Builds/Android/BattleForYourSoul.apk
   ```

If the install fails with a signature mismatch:

```sh
adb uninstall com.setzener.battleforyoursoul
adb install -r Builds/Android/BattleForYourSoul.apk
```

> The checked-in Android profile may have **Export Project** enabled, which exports a Gradle project instead of an APK. Either disable it in Build Profiles or use the CLI override below.

## Option C — CLI, direct APK

Quit Unity first.

```sh
unity build --profile Android \
  --android-export-type apk \
  --output Builds/Android/BattleForYourSoul.apk \
  --allow-dirty-build
```

Then install:

```sh
adb install -r Builds/Android/BattleForYourSoul.apk
```

`--allow-dirty-build` is needed while local IDE folders or generated artifacts are present. If your working tree is clean, you can omit it.

## Optional — AAB for Play Store

```sh
unity build --profile Android \
  --android-export-type aab \
  --output Builds/Android/BattleForYourSoul.aab \
  --allow-dirty-build
```

An `.aab` cannot be installed directly with `adb install`; use `bundletool` or upload it to Play Console.

---

# iOS deployment

## Step 1 — Generate a fresh Xcode project from Unity

Use the fixed menu; it deletes `Builds/iOS/BattleForYourSoul_Latest` before building, so there is exactly one iOS export and it always matches current source.

### Unity GUI (recommended)

1. In Unity, run **Tools → BattleForYourSoul → Build iOS Xcode Project**.
2. It switches the active target to iOS if needed and rebuilds:
   ```text
   Builds/iOS/BattleForYourSoul_Latest
   ```
3. Unity logs `[IOSDeployment] Xcode project build succeeded: ...` on success.

### CLI (Unity must be closed)

Create the iOS profile once:

```sh
unity build --create-profile iOS --allow-dirty-build
```

Then build:

```sh
unity build --profile iOS \
  --output Builds/iOS/BattleForYourSoul_Latest \
  --allow-dirty-build
```

### Verify the export contains current code

```sh
grep -c 'SuperpowerController\|AttackMovement' \
  Builds/iOS/BattleForYourSoul_Latest/Il2CppOutputProject/Source/il2cppOutput/BattleForYourSoul.cpp

grep -c 'WeaponController\|WeaponDefinition' \
  Builds/iOS/BattleForYourSoul_Latest/Il2CppOutputProject/Source/il2cppOutput/BattleForYourSoul.cpp
```

The first number should be greater than `0`; the second should be `0`.

---

## Step 2 — Build and sign with xcodebuild

Use a derived-data path **outside `Builds/`**. This keeps `Builds/` clean and avoids reusing stale intermediate objects.

```sh
DERIVED="/private/var/folders/qr/srlwwl5j3yj4l4dg7l1ksggm0000gn/T/opencode/BattleForYourSoul_iOS_DerivedData"

xcodebuild \
  -project Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone \
  -configuration Release \
  -destination 'generic/platform=iOS' \
  -derivedDataPath "$DERIVED" \
  DEVELOPMENT_TEAM=XRC9NUZN4G \
  -allowProvisioningUpdates \
  build
```

Do **not** pass `CODE_SIGN_IDENTITY='Apple Development: ...'` on the command line. The project uses Automatic signing; overriding the identity makes UnityFramework report conflicting provisioning settings.

The signed app is produced at:

```text
$DERIVED/Build/Products/Release-iphoneos/BattleForYourSoul.app
```

Copy it to the canonical final path and verify the signature:

```sh
rm -rf Builds/iOS/BattleForYourSoul.app
cp -a "$DERIVED/Build/Products/Release-iphoneos/BattleForYourSoul.app" \
  Builds/iOS/BattleForYourSoul.app

codesign -dv --verbose=4 Builds/iOS/BattleForYourSoul.app 2>&1 | grep -E 'Authority=|TeamIdentifier='
```

### Xcode GUI alternative

1. Open `Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj`.
2. Select scheme/target **Unity-iPhone**.
3. **Signing & Capabilities**: Team `XRC9NUZN4G`, **Automatically manage signing** = on.
4. Destination dropdown: choose **iPhone di Tano** to build and install directly.
5. Press **Run**. Xcode writes derived data to its own global location, not `Builds/`.

---

## Step 3 — Install and launch with `devicectl` (optional)

`devicectl` uses a different device ID than `xcodebuild`. Get it with:

```sh
xcrun devicectl list devices
```

Install the canonical app:

```sh
xcrun devicectl device install app \
  --device <COREDEVICE_ID> \
  Builds/iOS/BattleForYourSoul.app
```

Launch:

```sh
xcrun devicectl device process launch \
  --device <COREDEVICE_ID> \
  com.DefaultCompany.BattleForYourSoul
```

If you changed the iOS bundle ID in `ProjectSettings/ProjectSettings.asset`, regenerate the Xcode project and use the new ID here.

---

# Common problems

| Problem | Fix |
|---|---|
| `unity build` says project is already open | Quit Unity, then rerun the CLI command. |
| Xcode says no device matches destination | You used the `devicectl` ID. Run `xcodebuild -showdestinations` and use that ID. |
| Old weapons/behavior on iPhone | The Xcode project or app was stale. Run **Tools → BattleForYourSoul → Build iOS Xcode Project**, rebuild, and copy the fresh `.app`. |
| `Builds/` grew to many GB | You built with `-derivedDataPath` inside `Builds/`. Delete the `DerivedData*` folder and always use a path outside `Builds/`. |
| Signing fails with "requires a provisioning profile" | Do not override `CODE_SIGN_IDENTITY`. Use `DEVELOPMENT_TEAM=XRC9NUZN4G`, `CODE_SIGN_STYLE=Automatic`, and `-allowProvisioningUpdates`. |
| "UnityFramework has conflicting provisioning settings" | You passed a full `CODE_SIGN_IDENTITY` override on the command line. Remove it. |
| Android Studio parallel-sync prompt | It is only an IDE/Gradle prompt. Choose **Enable parallel sync for this project** or ignore it and deploy the APK from Unity. |
| Android build opens Android Studio instead of making an APK | Turn off **Export Project** in Build Profiles, or use `--android-export-type apk`. |
| `installUnityApk` task disappeared | Unity re-exported the Gradle files. Re-add the task to `Builds/AndroidStudio_Gradle/build.gradle`; the deploy script itself is tracked and unchanged. |
| App will not open on iPhone | Unlock the phone and trust the developer in **Settings → General → VPN & Device Management**. |
| `adb install` signature mismatch | `adb uninstall com.setzener.battleforyoursoul`, then install again. |

---

# Quick checklist

### Android

```sh
# Unity closed for CLI, or use the running Editor via the Android Studio Run hook
unity build --profile Android --android-export-type apk \
  --output Builds/Android/BattleForYourSoul.apk --allow-dirty-build

adb install -r Builds/Android/BattleForYourSoul.apk
```

### iOS

```sh
# In Unity: Tools → BattleForYourSoul → Build iOS Xcode Project
# Then, outside Unity:

DERIVED="/private/var/folders/qr/srlwwl5j3yj4l4dg7l1ksggm0000gn/T/opencode/BattleForYourSoul_iOS_DerivedData"

xcodebuild \
  -project Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone \
  -configuration Release \
  -destination 'generic/platform=iOS' \
  -derivedDataPath "$DERIVED" \
  DEVELOPMENT_TEAM=XRC9NUZN4G \
  -allowProvisioningUpdates \
  build

rm -rf Builds/iOS/BattleForYourSoul.app
cp -a "$DERIVED/Build/Products/Release-iphoneos/BattleForYourSoul.app" \
  Builds/iOS/BattleForYourSoul.app

xcrun devicectl list devices

xcrun devicectl device install app \
  --device <COREDEVICE_ID> \
  Builds/iOS/BattleForYourSoul.app

xcrun devicectl device process launch \
  --device <COREDEVICE_ID> \
  com.DefaultCompany.BattleForYourSoul
```

### Final check

```text
Builds/ should contain only:
  Android/BattleForYourSoul.apk
  AndroidStudio_Gradle/ (source only, no caches)
  iOS/BattleForYourSoul_Latest/
  iOS/BattleForYourSoul.app
```
