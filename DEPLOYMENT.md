# Deployment Guide — BattleForYourSoul

This is the repeatable deployment procedure for this project. Follow it from the current source branch; do not deploy from old folders in `Builds/`.

## Golden rules

1. **Deploy from current Unity source**, not from a stale generated project.
2. `Builds/` is git-ignored. Switching branches does **not** update it. Regenerate it.
3. **Quit Unity before using `unity build` on the command line.** The CLI refuses to run while the Editor has the project open.
4. Use a **fresh output folder** for iOS exports so Xcode cannot keep using an old IL2CPP project.
5. **Verify device IDs every time.** Xcode and `devicectl` use different identifiers for the same iPhone.

Current known values for this Mac (verify, do not assume):

| Item | Value |
|---|---|
| Unity | `6000.6.9f1` |
| Xcode | `26.2` |
| Branch used for current work | `feature/superstrength` |
| iOS/Android bundle ID | `com.setzener.battleforyoursoul` |
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
2. Open that folder in Android Studio, not the Unity source root and not the old `Builds/AndroidStudio` folder.
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

> The checked-in Android profile currently has **Export Project** enabled, so it exports an Android Studio/Gradle project by default. If you leave it enabled, you get a folder, not an APK. Either disable **Export Project** in Build Profiles or use the CLI override below.

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

`--allow-dirty-build` is needed while the local `.idea/` folder is present. If your working tree is clean, you can omit it.

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

Do **not** open:

```text
Builds/iOS/BattleForYourSoul/Unity-iPhone.xcodeproj
```

That is an old export with stale `Weapon*` IL2CPP code.

### Unity GUI

1. **File → Build Profiles**.
2. If there is no iOS profile, create one: **Create new profile → iOS**.
3. Select the iOS profile and click **Switch Platform**.
4. Click **Build**.
5. Use a fresh folder, for example:

   ```text
   Builds/iOS/BattleForYourSoul_Latest
   ```

### CLI

Quit Unity first. Create the iOS profile once:

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

## Step 2 — Build/install from Xcode GUI

1. Open:

   ```text
   Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj
   ```

2. Select scheme/target **Unity-iPhone**.
3. **Signing & Capabilities**:
   - Team: `XRC9NUZN4G`
   - Bundle Identifier: `com.setzener.battleforyoursoul`
   - **Automatically manage signing** = on
4. Destination dropdown: choose **iPhone di Tano**.  
   Do not choose **Any iOS Device** if you want Xcode to install automatically.
5. Press **Run**.
6. If prompted on the phone, trust the developer certificate.

If you still see old weapons or old behavior, you opened the wrong `.xcodeproj`.

---

## Step 3 — CLI build/install

### Get the current Xcode destination ID

```sh
xcodebuild -showdestinations \
  -project Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone
```

Look for the line containing your iPhone name and copy the `id=...` value.

### Build and sign

```sh
xcodebuild \
  -project Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone \
  -configuration Release \
  -destination 'id=<XCODE_DEVICE_ID>' \
  -derivedDataPath Builds/iOS/DerivedDataLatest \
  CODE_SIGN_STYLE=Automatic \
  -allowProvisioningUpdates \
  build
```

Installed app path after a successful build:

```text
Builds/iOS/DerivedDataLatest/Build/Products/Release-iphoneos/BattleForYourSoul.app
```

### Install and launch with `devicectl`

`devicectl` uses a different device ID than `xcodebuild`. Get it with:

```sh
xcrun devicectl list devices
```

Install:

```sh
xcrun devicectl device install app \
  --device <COREDEVICE_ID> \
  Builds/iOS/DerivedDataLatest/Build/Products/Release-iphoneos/BattleForYourSoul.app
```

Launch:

```sh
xcrun devicectl device process launch \
  --device <COREDEVICE_ID> \
  com.setzener.battleforyoursoul
```

---

# Common problems

| Problem | Fix |
|---|---|
| `unity build` says project is already open | Quit Unity, then rerun the CLI command. |
| Xcode says no device matches destination | You used the `devicectl` ID. Run `xcodebuild -showdestinations` and use that ID. |
| Old weapons/behavior on iPhone | You opened the stale `Builds/iOS/BattleForYourSoul/Unity-iPhone.xcodeproj`. Regenerate into a fresh folder. |
| Android Studio parallel-sync prompt | It is only an IDE/Gradle prompt. Choose **Enable parallel sync for this project** or ignore it and deploy the APK from Unity. |
| Android build opens Android Studio instead of making an APK | Turn off **Export Project** in Build Profiles, or use `--android-export-type apk`. |
| `installUnityApk` task disappeared | Unity re-exported the Gradle files. Re-add the task to `Builds/AndroidStudio_Gradle/build.gradle`; the deploy script itself is tracked and unchanged. |
| Signing fails | Confirm team `XRC9NUZN4G`, bundle ID `com.setzener.battleforyoursoul`, automatic signing, and pass `-allowProvisioningUpdates`. |
| App will not open on iPhone | Unlock the phone and trust the developer in **Settings → General → VPN & Device Management**. |
| `adb install` signature mismatch | `adb uninstall com.setzener.battleforyoursoul`, then install again. |

---

# Quick checklist

### Android

```sh
# Unity closed
unity build --profile Android --android-export-type apk \
  --output Builds/Android/BattleForYourSoul.apk --allow-dirty-build

adb install -r Builds/Android/BattleForYourSoul.apk
```

### iOS

```sh
# Unity closed, first time only
unity build --create-profile iOS --allow-dirty-build

# Every deploy
unity build --profile iOS --output Builds/iOS/BattleForYourSoul_Latest --allow-dirty-build

xcodebuild -showdestinations \
  -project Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone

xcodebuild \
  -project Builds/iOS/BattleForYourSoul_Latest/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone \
  -configuration Release \
  -destination 'id=<XCODE_DEVICE_ID>' \
  -derivedDataPath Builds/iOS/DerivedDataLatest \
  CODE_SIGN_STYLE=Automatic \
  -allowProvisioningUpdates \
  build

xcrun devicectl list devices

xcrun devicectl device install app \
  --device <COREDEVICE_ID> \
  Builds/iOS/DerivedDataLatest/Build/Products/Release-iphoneos/BattleForYourSoul.app

xcrun devicectl device process launch \
  --device <COREDEVICE_ID> \
  com.setzener.battleforyoursoul
```
