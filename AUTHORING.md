# Content Authoring

How to tune and extend the game **without touching code**. Everything a designer reaches for is a
ScriptableObject asset under `Assets/_Project/ScriptableObjects`, or a sprite under
`Assets/_Project/Art`.

> This is a **game-design / tooling** doc. Git workflow rules live in `GIT_GALATEO.md`.

## The rule

- **Definition assets are seeded once, then owned by you.** The scene builder creates them with
  sensible defaults the first time only. After that it never overwrites a value you set; it only
  backfills newly-required empty references and newly-required positive values.
- **Prefabs and the scene are generated.** They are rebuilt every time you run the builder, so put
  tuning in assets, not in prefab overrides or scene components.
- **Never delete an asset or a scene without explicit approval.** Generated artifacts can be
  rebuilt, but authored assets are yours and are only ever backfilled, never replaced.

Rebuild the scene with **Tools ▸ BattleForYourSoul ▸ Build Game Scene**. Runtime systems read the
assets on start, so editing an asset and entering Play mode (no rebuild) is enough to see most
changes.

## Mobile controls

The generated `HUD` canvas contains a `MobileControls` root with a left joystick and three buttons.
It is authored inactive and `MobileControlsVisibility` activates it only on `Application.isMobilePlatform`,
so desktop Play sessions and desktop builds do not create a virtual input device. The controls feed a
virtual **Gamepad** (`leftStick`, `buttonSouth`, `buttonWest`, `buttonNorth`), so they drive the
existing `Move`, `Dash`, `PrimaryAttack` and `SecondaryAttack` bindings without touch-specific code
in gameplay. Upgrade the bindings in `InputSystem_Actions.inputactions` if the button mapping should
change; no scene edit is needed.

## Agreed technical conventions

These were established with the first milestones and apply to every new system:

- **Unity 6 LTS, 2D URP, 2D Physics, Input System, 2D Tilemap, Pixel Perfect Camera and
  ScriptableObjects.** Built-in and official packages only.
- **Composition over inheritance.** Behaviour lives in small components that collaborate through
  wired references.
- **No global singletons or static state.** Each scene owns its own registries and controllers;
  the builder wires references directly.
- **Runtime state never lives in a definition asset.** Assets are immutable tuning data; current
  health, charges, cooldowns and upgrade totals stay on scene components.
- **No scene-wide searches in gameplay.** Cache references at build/start time; scene-scoped
  registries (such as `EnemyRegistry`) provide live lookups.
- **No per-frame allocations.** Reuse lists and filters, and pool transient objects (projectiles,
  enemies, hit VFX).
- **Input acquisition is separate from gameplay.** Only input reader components touch the Input
  System; gameplay consumes source-agnostic interfaces (`IPlayerInput`, `ISuperpowerActions`).
- **Isometric sorting is centralised.** `IsometricSpriteSorter` derives depth from world position;
  never assign arbitrary fixed per-sprite sort orders.

## Automated tests

Tests live in `Assets/_Project/Tests` and target mechanics, not placeholder art or content:

- **EditMode** — fast tests for pure maths and authoring-time data: isometric grid round-trips,
  monster scaling, the XP curve, default weapon modifiers and upgrade weapon filters.
- **PlayMode** — stateful components wired from minimal objects built in test code, never the
  generated scene: health damage/immunity/death, XP and level-ups, upgrade aggregation, dash
  charges/cooldown/grace/teleport, the HUD's delayed ghost bar, and combat mechanics (weapon
  cooldowns, melee arc and layer filtering, impact radius, projectile speed/spread and pool reuse,
  and VFX sweep/fade/lifetime).

Run them from Unity's Test Runner or with `unity test . --mode EditMode` /
`unity test . --mode PlayMode`. When using a connected Editor, stop Play mode first: an EditMode
run cannot start while the Editor is already playing.

## Where things live

| What you want to change | Asset | Notes |
|---|---|---|
| Player stats, sprite, body, dash, starting superpower | `Player_Default.asset` | Max HP, move speed, accel/decel, dash speed/duration/cooldown/grace, collider size/offset |
| Enemy type | `Enemy_Grunt.asset` | Sprite, HP, speed, **aggro radius**, contact damage, XP, collider radius |
| Superpower | `Superpowers/Superpower_*.asset` | Display name, description, **pickup sprite**, primary and special action refs |
| One superpower action | `Superpowers/Actions/Action_*.asset` | Shape (melee arc / line / projectile), cooldown, damage, reach, arc/width, projectile settings |
| Player level curve | `Progression_Default.asset` | Starting level, base XP, growth |
| Monster stat/XP scaling | `MonsterScaling_Default.asset` | Growth per monster level |
| A stage | `Stages/Stage_*.asset` | Monster level, **fixed enemy placements**, room rules (see below) |
| Stage order / end behaviour | `StageProgression_Default.asset` | Ordered stage list, loop-vs-escalate |
| Doors, room entrance, superpower pedestals | `GameSettings.asset` | `roomEntrance`, `doorPositions`, `superpowerPedestalPositions` (shared by all stages) |
| Which upgrades can appear | `UpgradePool_Default.asset` | List of upgrade assets |
| One upgrade | `Upgrades/*.asset` | Name, icon, colour, **source** (`LevelUp` / `StageClear`), category, superpower filter, stat modifiers |
| Door/reward category | `UpgradeCategories/*.asset` | Display name, colour, description |
| Rendering (PPU, resolution, iso, sorting) | `GameSettings.asset` | Read by the camera, grid and import pipeline |

`Superpower_Default.asset` is the legacy fallback definition kept for older references; the four
selectable superpowers (Sword, Spear, Bow, Superstrength) live under `Superpowers/`.

## Sprites

1. Drop a PNG anywhere under `Assets/_Project/Art/`.
2. `PixelArtPostprocessor` imports it automatically: Sprite type, Point filter, Uncompressed, no
   mipmaps, Clamp wrap, FullRect mesh, and the PPU from `GameSettings` (32 by default). Alignment is
   left alone, so author your own pivot.
3. Assign the sprite to the matching definition (`Player_Default`, `Enemy_*`, a superpower's
   `pickupSprite`, an action's `projectileSprite`, an upgrade's icon, …).

Run **Tools ▸ BattleForYourSoul ▸ Generate Placeholder Art** to (re)create the temporary placeholders,
including the superpower pickup props (now with fists for Superstrength), the attack animation frames
in `Art/Animation/`, the arrow projectile, and the visible locked/open exit doors. Swapping art never
requires a code change or a scene rebuild. The scene builder also generates any missing placeholder
before it seeds assets, so a fresh checkout still wires up.

Animation frames are referenced by each action's `animation` block: `Frames`, `FrameDuration`,
`HopOffset`, `LungeDistance` and `LungeDuration`. Keep every frame on the same pivot as the resting
player sprite. `HopOffset` is a **sprite-only** offset — never use it to fake a physics jump.

## Room encounters

A run walks the ordered list in `StageProgression_Default`:

- Each `StageDefinition` sets the **monster level** and a list of `EnemyPlacement`s: an enemy type and
  an exact world position. Those enemies are already standing in the room when the stage starts.
- Enemies wait at their post until the player enters their authored **aggro radius**, then chase.
- The player starts each stage at `GameSettings.roomEntrance`.
- There is no timer and no replacement wave. Defeat **every** placed enemy to unlock the exits.
- Three doors are shared map fixtures positioned by `GameSettings.doorPositions`. They are always
  visible: locked doors show barred art; unlocked doors show their category colour and label. There is
  no Superpowers door.
- Walking into an unlocked door advances the stage. No key is required.

To add an enemy type: duplicate `Enemy_Grunt.asset`, give it a sprite and stats, then add placements
to stages referencing it.

After the last authored stage the run **escalates** (the final room repeats while monster level keeps
climbing). Tick `loopStages` on the progression to restart from stage one instead.

## Movement and dash

Normal walking is tuned on `Player_Default.asset` (`moveSpeed`, `acceleration`, `deceleration`).
Dash adds four fields:

| Field | Default | Meaning |
|---|---|---|
| `dashSpeed` | 14 | World units per second while dashing |
| `dashDuration` | 0.18 | Seconds the dash velocity is held |
| `dashCooldown` | 1.2 | Recharge time for the normal dash charge |
| `dashGrace` | 0.08 | Brief damage immunity after the dash ends |

Dash has its own Input System action (`Dash`, bound to Space and gamepad A) alongside `Move`, so
gameplay never reads a device directly. It always travels in the player's last facing direction and
passes through enemies: while dashing, the player's collider ignores every live enemy collider and
incoming damage is suppressed, then both are restored at the end. The short grace window keeps an
enemy the player lands on from hitting on the very next frame.

`PlayerDash` owns charges, cooldown and pass-through; `PlayerMovement` owns the body velocity for the
dash's duration. Both are scene components — no dash state lives in the definition asset. A stage
teleport cancels an active dash and clears its immunity and collision overrides.

## Samsara and superpowers

The first stage is **Samsara**, a superpower-selection room. It is authored like any other stage asset
(`Stages/Stage_Samsara.asset`) with two extra room rules:

- **Superpower selection stage** — no enemies are placed, and the physical superpowers are active. The room is
  clear from the start, but its door stays locked until the player picks up a superpower.
- **Active door indices** — only the listed doors are enabled. Samsara leaves the list empty so all
  three category doors are reachable; fill it to restrict a room.

Four pickups stand on the pedestals listed in `GameSettings.superpowerPedestalPositions`, one per
superpower in the pool (Sword, Spear, Bow and Superstrength). Walking into a pickup equips it; picking
another swaps freely, and the previously chosen prop reappears at its original pedestal. Nothing is
committed until the player leaves through a door: that locks the current superpower for the whole run.
Samsara is only the opening stage — progression does not loop back to it, so selection cannot recur.

A superpower asset names two actions, both driven by Input System actions (`PrimaryAttack` /
`SecondaryAttack` — no keyboard reads in gameplay code):

| Superpower | Primary | Special | Shape |
|---|---|---|---|
| Sword | Slash | Cleave | close melee arc, wider special |
| Spear | Thrust | Sweep | long line reach, wider special |
| Bow | Shot | Spread | projectile, multi-shot special |
| Superstrength | Punch | Uppercut | fast short melee arc; rooted hits, lunge and knockback special |

Actions are authored under `Superpowers/Actions/`. `shape` selects the delivery: `MeleeArc`, `Line` or
`Projectile`. For melee/line actions, tune `range`, `arcDegrees` or `lineWidth`; for projectiles,
tune `projectilesPerShot`, `spreadDegrees`, `projectileSpeed`, `projectileLifetime` and
`projectileSprite`. Melee/line actions also show a pooled one-shot hit effect configured by
`hitVfxSprite`, `hitVfxColor`, `hitVfxSize`, `hitVfxDuration` and `hitVfxSweep` (the last sweeps the
sprite across its lifetime, giving a slash; leave it at 0 for a straight thrust). Projectile actions
ignore those fields.

Three more fields matter for Superstrength-style actions:

- **Timeline** — `hitCount`, `hitDelay` and `hitInterval` define separate damage frames. Each frame
  rolls its own damage, crit and on-hit effects. Northern Star uses three hits; keep `cooldown` about
  equal to the full animation length.
- **On hit** — `onHit` carries `RootDuration`, `RootAffectsSpecialFoes`, `StunDuration`,
  `KnockbackForce`, `KnockbackDuration`, `KnockbackAffectsSpecialFoes` and the experimental
  `GuardBreak`. Root applies to `Foe` by default; bosses ignore all control effects. Knockback
  always travels away from the attacker.
- **Placeholder animation** — `animation` frames, duration, sprite-only hop and forward lunge. Keep
  `LungeDistance` small; Uppercut/Body Slam are the only larger lunges.

The builder seeds ten actions and four superpowers; edit those assets rather than prefab or scene values.

> These superpowers and effects are **placeholders**. The real superpowers will be different; this
> milestone only puts the mechanics in place.

## Categories and upgrades

Doors advertise one `UpgradeCategoryDefinition` each. The category chosen at a door becomes the
**filtered clear reward** for the next stage. `UpgradeDefinition.source` keeps the two reward pools
separate: `StageClear` cards never appear on XP level-ups, and `LevelUp` cards never appear as door
rewards. A category with too few cards falls back to the rest of the **stage source only**.

- Each category should have **at least three upgrades** in `UpgradePool_Default` so its filter can
  fill a three-card choice.
- The three door categories are **Upgrades**, Mobility and Vitality; edit their assets to rename,
  recolour or describe them. Only **Upgrades** has authored stage cards today. The `Shots` and
  `Superpowers` category assets are retained as placeholders and are not offered at a door.
- To add an upgrade: duplicate an asset under `Upgrades/`, set its **source**, modifiers and category,
  then add it to the pool. Percent stats are whole numbers (12 = +12%).
- Projectile upgrades are Bow-only. Do not leave a shot card unfiltered or Superstrength will be
  offered upgrades for attacks it does not have.
- `InstantDashCharges` grants dash charges that are **always ready**, independent of the normal
  cooldown. A granted charge is not consumed permanently: it refills whenever a dash ends, so one
  charge means one free dash is always available. The seeded **Phase Step** upgrade in the Mobility
  category grants one; stacking the upgrade grants more consecutive free dashes.
- An upgrade can also set a **superpower filter** (`superpowerFilter`). Leave it empty for a global upgrade
  that every superpower can roll; fill it with one or more superpowers for a superpower-specific effect. Effects
  from the filter are still upgrades, not extra actions — they are only offered while the matching
  superpower is equipped. The seeded sword/spear/bow effect upgrades are placeholders for the real
  superpower-specific upgrades to come.
- Unique upgrades use `maxStacks: 1`, and `exclusiveGroup` makes a set mutually exclusive — the
  seeded Hulk and Man of Steel both use `superstrength-form`, so only one can ever be taken.
  `requires` hides a card until its prerequisite is picked (Super Charge requires Charge). These
  rules are enforced by `RunUpgradeState` and reset at the start of a new run.
- `primaryOverride` and `specialOverride` replace the equipped superpower's action for the rest of
  the run. Northern Star Strike and Body Slam use these; point them at an action asset under
  `Superpowers/Actions/`.

## Superstrength tuning

`Superpower_Superstrength.asset` authors inherent `defense: 12` and `defenseKnee: 50`; every other
superpower has zero defense. Superstrength upgrades are filtered to that definition. The numbers below
are **first-pass placeholders — tune percentages and damage later**.

| Field / formula | Effect |
|---|---|
| `Punch` | 0.28s primary; 0.5-unit forward step, one hit in front, roots a `Foe` for 0.3s, knocks it back and suppresses its contact damage for 0.35s |
| `Uppercut` | 1.6s special; 1.5-unit immune sweep through enemies, longer recharge, stronger knockback |
| Damage | `(base + Strength + DamageFlat) * DamagePercent` |
| Hulk low-health attack | `damage *= 1 + 0.5 * (1 - health.Normalized)` |
| Critical | 100% chance cap; crit multiplies by `2 + CriticalDamage`, capped at +50% |
| Defense | `reduction = min(0.8, defense / (defense + knee))`; damage is multiplied by `1 - reduction` |
| Man of Steel low-health defense | `defense += 40 * (1 - health.Normalized)` on top of its +30 flat |
| Bonus stun | 20% per card, capped at 50%; adds a 0.25s stun on top of any authored root |
| `ContactDamageDisabledDuration` | a hit enemy cannot deal contact damage for this long, including during knockback and regardless of rank |
| `sweepHitsWhileLunging` | moving attack: hit each foe once along the lunge path, with damage immunity and enemy pass-through for the movement window |

`PlayerStats` owns every cap. `RollBonusStun()` is separate from the guaranteed root: bosses ignore
both stun and root, and `SpecialFoe` ignores root unless the action opts in. Contact-damage suppression
is deliberately different — it is authored per action and reaches all ranks, so a boss that ignores
every control effect still cannot punish a melee hit. `Enemy.FixedUpdate` now decides contact damage in
one place, so knockback can no longer re-enable it early.

Immunity and enemy pass-through are **owner-keyed leases**: `Health.AddDamageImmunity(owner)` /
`RemoveDamageImmunity(owner)` and `EnemyRegistry.SuppressEnemyCollisions(owner, collider)` /
`RestoreEnemyCollisions(owner, collider)`. Every attack step takes these leases, not just sweeping
ones. Dash and an attack can overlap; each cleans up only its own lease, and a stage teleport or
attack cancel releases them both. Walls and obstacles are never ignored.

`AttackMovement.Begin(direction, distance, duration)` is the single home for attack movement. It
captures the direction once, moves with `Rigidbody2D.MovePosition` in `FixedUpdate`, and suspends
`PlayerMovement` input for the duration. Do not reintroduce velocity-driven lunges: held input and
enemy contacts were what made the old attacks feel erratic. Never set `linearVelocity` after
`MovePosition` either — that cancels the move the engine just computed. `AttackAnimation.LungeDistance`
/ `LungeDuration` are the authored values for this step; the name is kept for asset compatibility.

**Attack lifecycle (rewritten).** `SuperpowerController.AttackRoutine` is the only attack entry point:
resolve direction once → optional deterministic `AttackMovement` step → visual pose → damage frames →
release leases in `finally`. `CancelAttack` is the only teardown. The routine never writes the player's
transform.

**Visual/body split (the actual jitter bug).** The player's `SpriteRenderer` lives on a `Visual` **child**;
the root owns only the Rigidbody, collider and gameplay components. `SuperpowerPosePlayer` and
`PlayerVisuals` are wired to that child and refuse to run if the renderer is on the root. An earlier
version wrote the pose hop and its captured base position to the root transform, so every attack
snapped the physics body back to where the button was pressed and fought the Rigidbody — with *every*
superpower, not just Superstrength. Do not move that renderer back to the root.

> **Observe:** Northern Star deals full damage on each of its three hits — track this before raising
> its damage. Bonus stun can stack with the guaranteed root on a `Foe`; watch for over-control.

> **TODO:** Hulk/Man of Steel growth and tint are placeholder hooks in `PlayerVisuals`. Retune the
> scale/tint when final sprites arrive; do not retune the combat formulas to chase art.

> **EXPERIMENT:** `GuardBreak` on `HitEffects` converts boss knockback immunity into a 1.25x damage
> multiplier. It is implemented but unused by seeded actions. The crit flash (yellow, 20% larger VFX)
> is also experimental feedback. Track both and cut them if they do not read well in playtests.

## Android build

The project is configured for Android with application id `com.setzener.battleforyoursoul`, landscape
orientation (portrait autorotation disabled), min SDK 26 and target SDK 36. Android Build Support,
the JDK and the SDK/NDK tools are installed with the Unity version used by this project.

A local development build is produced in batch mode with the checked-in Android build profile:

```sh
unity build --profile Android --output Builds/Android/BattleForYourSoul.apk
```

The profile is at `Assets/Settings/Build Profiles/Android.asset` and uses the project's global scene
list. This is an unsigned/debug-style build for local install; a Play Store release still needs a
keystore and signing configuration, which are deliberately not committed.

Graphics APIs are pinned to OpenGL ES 3 only for Android. Unity 6 otherwise prefers Vulkan, whose
software implementation in the Android emulator cannot render URP geometry (the screen stays a solid
camera-clear colour even though the player runs). Do not re-enable Vulkan in Player Settings unless
the target device has been verified.

## iOS build

The project is configured for iOS with application id `com.setzener.battleforyoursoul`, landscape
orientation and a minimum OS version of 15.0. iOS Build Support for the project's Unity version and
Xcode are required. A build with the checked-in iOS profile produces an Xcode project:

```sh
unity build --profile iOS --output Builds/iOS/BattleForYourSoul
```

The profile is at `Assets/Settings/Build Profiles/iOS.asset` and uses the project's global scene
list. The generated `Unity-iPhone.xcodeproj` has no `DEVELOPMENT_TEAM`, so a device build fails until
a team is selected in Xcode under **Unity-iPhone ▸ Signing & Capabilities** (Automatic signing then
creates the provisioning profile). Because each Unity build regenerates the Xcode project, the team
must be selected again after every rebuild. Release signing and App Store credentials are deliberately
not committed.

For a Simulator run, set `PlayerSettings.iOS.sdkVersion` to `iOSSdkVersion.SimulatorSDK` before the
Unity build, compile the generated project with `-sdk iphonesimulator CODE_SIGNING_ALLOWED=NO`, then
install the resulting `.app` with `xcrun simctl install`. Restore `iOSSdkVersion.DeviceSDK` afterwards
so device builds keep using the device SDK.

## Adding a new definition type

Definition assets are plain `ScriptableObject`s with `[CreateAssetMenu]`. Runtime state stays on the
scene components, never in the asset. If the builder should create the asset for you, add an
`EnsureX()` in `GameSceneBuilder` that seeds defaults **only when the asset is created** (see
`EnsureAsset` / `SeedReferenceIfMissing`).
