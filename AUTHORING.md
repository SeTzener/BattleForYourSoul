# Content Authoring

How to tune and extend the game **without touching code**. Everything a designer reaches for is a
ScriptableObject asset under `Assets/_Project/ScriptableObjects`, or a sprite under
`Assets/_Project/Art`.

> This is a **game-design / tooling** doc. Git workflow rules live in `GIT_GALATEO.md`.

## The rule

- **Definition assets are seeded once, then owned by you.** The scene builder creates them with
  sensible defaults the first time only. After that it never overwrites a value you set; it only
  backfills newly-required empty references.
- **Prefabs and the scene are generated.** They are rebuilt every time you run the builder, so put
  tuning in assets, not in prefab overrides or scene components.

Rebuild the scene with **Tools ▸ BattleForYourSoul ▸ Build Game Scene**. Runtime systems read the
assets on start, so editing an asset and entering Play mode (no rebuild) is enough to see most
changes.

## Where things live

| What you want to change | Asset | Notes |
|---|---|---|
| Player stats, sprite, body, starting weapon | `Player_Default.asset` | Max HP, move speed, accel/decel, collider size/offset |
| Enemy type | `Enemy_Grunt.asset` | Sprite, HP, speed, **aggro radius**, contact damage, XP, collider radius |
| Weapon | `Weapons/Weapon_*.asset` | Display name, description, **pickup sprite**, primary and special action refs |
| One weapon action | `Weapons/Actions/Action_*.asset` | Shape (melee arc / line / projectile), cooldown, damage, reach, arc/width, projectile settings |
| Player level curve | `Progression_Default.asset` | Starting level, base XP, growth |
| Monster stat/XP scaling | `MonsterScaling_Default.asset` | Growth per monster level |
| A stage | `Stages/Stage_*.asset` | Monster level, **fixed enemy placements**, room rules (see below) |
| Stage order / end behaviour | `StageProgression_Default.asset` | Ordered stage list, loop-vs-escalate |
| Doors, room entrance, weapon pedestals | `GameSettings.asset` | `roomEntrance`, `doorPositions`, `weaponPedestalPositions` (shared by all stages) |
| Which upgrades can appear | `UpgradePool_Default.asset` | List of upgrade assets |
| One upgrade | `Upgrades/*.asset` | Name, icon, colour, category, **weapon filter**, stat modifiers |
| Door/reward category | `UpgradeCategories/*.asset` | Display name, colour, description |
| Rendering (PPU, resolution, iso, sorting) | `GameSettings.asset` | Read by the camera, grid and import pipeline |

`Weapon_Default.asset` is the legacy fallback definition kept for older references; the three
selectable weapons live under `Weapons/`.

## Sprites

1. Drop a PNG anywhere under `Assets/_Project/Art/`.
2. `PixelArtPostprocessor` imports it automatically: Sprite type, Point filter, Uncompressed, no
   mipmaps, Clamp wrap, FullRect mesh, and the PPU from `GameSettings` (32 by default). Alignment is
   left alone, so author your own pivot.
3. Assign the sprite to the matching definition (`Player_Default`, `Enemy_*`, a weapon's
   `pickupSprite`, an action's `projectileSprite`, an upgrade's icon, …).

Run **Tools ▸ BattleForYourSoul ▸ Generate Placeholder Art** to (re)create the temporary placeholders,
including the weapon pickup props, the arrow projectile, and the visible locked/open exit doors.
Swapping art never requires a code change or a scene rebuild. The scene builder also generates any
missing placeholder before it seeds assets, so a fresh checkout still wires up.

## Room encounters

A run walks the ordered list in `StageProgression_Default`:

- Each `StageDefinition` sets the **monster level** and a list of `EnemyPlacement`s: an enemy type and
  an exact world position. Those enemies are already standing in the room when the stage starts.
- Enemies wait at their post until the player enters their authored **aggro radius**, then chase.
- The player starts each stage at `GameSettings.roomEntrance`.
- There is no timer and no replacement wave. Defeat **every** placed enemy to unlock the exits.
- Doors are shared map fixtures positioned by `GameSettings.doorPositions`. They are always visible:
  locked doors show barred art; unlocked doors show their category colour and label.
- Walking into an unlocked door advances the stage. No key is required.

To add an enemy type: duplicate `Enemy_Grunt.asset`, give it a sprite and stats, then add placements
to stages referencing it.

After the last authored stage the run **escalates** (the final room repeats while monster level keeps
climbing). Tick `loopStages` on the progression to restart from stage one instead.

## Samsara and weapons

The first stage is **Samsara**, a weapon-selection room. It is authored like any other stage asset
(`Stages/Stage_Samsara.asset`) with two extra room rules:

- **Weapon selection stage** — no enemies are placed, and the physical weapons are active. The room is
  clear from the start, but its door stays locked until the player picks up a weapon.
- **Active door indices** — only the listed doors are enabled. Samsara uses one door so the exit is
  unambiguous; leave the list empty to enable every door.

Three pickups stand on the pedestals listed in `GameSettings.weaponPedestalPositions`, one per
weapon in the pool. Walking into a pickup equips it; picking another swaps freely, and the previously
chosen prop reappears at its original pedestal. Nothing is committed until the player leaves: the
door then locks the current weapon for the whole run, so swapping is no longer possible.

A weapon asset names two actions, both driven by Input System actions (`PrimaryAttack` /
`SecondaryAttack` — no keyboard reads in gameplay code):

| Weapon | Primary | Special | Shape |
|---|---|---|---|
| Sword | Slash | Cleave | close melee arc, wider special |
| Spear | Thrust | Sweep | long line reach, wider special |
| Bow | Shot | Spread | projectile, multi-shot special |

Actions are authored under `Weapons/Actions/`. `shape` selects the delivery: `MeleeArc`, `Line` or
`Projectile`. For melee/line actions, tune `range`, `arcDegrees` or `lineWidth`; for projectiles,
tune `projectilesPerShot`, `spreadDegrees`, `projectileSpeed`, `projectileLifetime` and
`projectileSprite`. Melee/line actions also show a pooled one-shot hit effect configured by
`hitVfxSprite`, `hitVfxColor`, `hitVfxSize`, `hitVfxDuration` and `hitVfxSweep` (the last sweeps the
sprite across its lifetime, giving a slash; leave it at 0 for a straight thrust). Projectile actions
ignore those fields. The builder seeds six actions and three weapons; edit those assets rather than
prefab or scene values.

> These weapons and effects are **placeholders**. The real weapons will be different; this
> milestone only puts the mechanics in place.

## Categories and upgrades

Doors advertise one `UpgradeCategoryDefinition` each. The category chosen at a door becomes the
**filtered clear reward** for the next stage: when that stage is cleared, `LevelUpController` shows a
three-card choice drawn only from upgrades in that category. Normal XP level-ups stay unfiltered and
queue alongside it.

- Each category should have **at least three upgrades** in `UpgradePool_Default` so its filter can
  fill a three-card choice.
- The seeded placeholders are **Shots**, **Mobility** and **Vitality**; edit their assets to rename,
  recolour or describe them. Samsara's single door carries one of these categories as usual.
- To add an upgrade: duplicate an asset under `Upgrades/`, set its modifiers and category, then add it
  to the pool. Percent stats are whole numbers (12 = +12%).
- An upgrade can also set a **weapon filter** (`weaponFilter`). Leave it empty for a global upgrade
  that every weapon can roll; fill it with one or more weapons for a weapon-specific effect. Effects
  from the filter are still upgrades, not extra actions — they are only offered while the matching
  weapon is equipped. The seeded sword/spear/bow effect upgrades are placeholders for the real
  weapon-specific upgrades to come.

## Adding a new definition type

Definition assets are plain `ScriptableObject`s with `[CreateAssetMenu]`. Runtime state stays on the
scene components, never in the asset. If the builder should create the asset for you, add an
`EnsureX()` in `GameSceneBuilder` that seeds defaults **only when the asset is created** (see
`EnsureAsset` / `SeedReferenceIfMissing`).
