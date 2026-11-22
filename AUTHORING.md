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
| Weapon | `Weapon_Default.asset` | Fire rate, spread, damage, projectile speed/lifetime, **projectile sprite** |
| Player level curve | `Progression_Default.asset` | Starting level, base XP, growth |
| Monster stat/XP scaling | `MonsterScaling_Default.asset` | Growth per monster level |
| A stage | `Stages/Stage_01.asset` … | Monster level, **fixed enemy placements** |
| Stage order / end behaviour | `StageProgression_Default.asset` | Ordered stage list, loop-vs-escalate |
| Doors and room entrance | `GameSettings.asset` | `roomEntrance`, `doorPositions` (shared by all stages) |
| Which upgrades can appear | `UpgradePool_Default.asset` | List of upgrade assets |
| One upgrade | `Upgrades/*.asset` | Name, icon, colour, **category**, stat modifiers |
| Door/reward category | `UpgradeCategories/*.asset` | Display name, colour, description |
| Rendering (PPU, resolution, iso, sorting) | `GameSettings.asset` | Read by the camera, grid and import pipeline |

## Sprites

1. Drop a PNG anywhere under `Assets/_Project/Art/`.
2. `PixelArtPostprocessor` imports it automatically: Sprite type, Point filter, Uncompressed, no
   mipmaps, Clamp wrap, FullRect mesh, and the PPU from `GameSettings` (32 by default). Alignment is
   left alone, so author your own pivot.
3. Assign the sprite to the matching definition (`Player_Default`, `Enemy_*`, `Weapon_Default`, an
   upgrade's icon, …).

Run **Tools ▸ BattleForYourSoul ▸ Generate Placeholder Art** to (re)create the temporary placeholders,
including the visible locked/open exit doors. Swapping art never requires a code change or a scene
rebuild.

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

## Categories and upgrades

Doors advertise one `UpgradeCategoryDefinition` each. The category chosen at a door becomes the
**filtered clear reward** for the next stage: when that stage is cleared, `LevelUpController` shows a
three-card choice drawn only from upgrades in that category. Normal XP level-ups stay unfiltered and
queue alongside it.

- Each category should have **at least three upgrades** in `UpgradePool_Default` so its filter can
  fill a three-card choice.
- The seeded placeholders are **Shots**, **Mobility** and **Vitality**; edit their assets to rename,
  recolour or describe them.
- To add an upgrade: duplicate an asset under `Upgrades/`, set its modifiers and category, then add it
  to the pool. Percent stats are whole numbers (12 = +12%).

## Adding a new definition type

Definition assets are plain `ScriptableObject`s with `[CreateAssetMenu]`. Runtime state stays on the
scene components, never in the asset. If the builder should create the asset for you, add an
`EnsureX()` in `GameSceneBuilder` that seeds defaults **only when the asset is created** (see
`EnsureAsset` / `SeedReferenceIfMissing`).
