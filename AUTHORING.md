# Content Authoring

How to tune and extend the game **without touching code**. Everything a designer reaches for is a
ScriptableObject asset under `Assets/_Project/ScriptableObjects`, or a sprite under
`Assets/_Project/Art`.

> This is a **game-design / tooling** doc. Git workflow rules live in `GIT_GALATEO.md`.

## The rule

- **Definition assets are seeded once, then owned by you.** The scene builder creates them with
  sensible defaults the first time only. After that it never overwrites a value you set.
- **Prefabs and the scene are generated.** They are rebuilt every time you run the builder, so put
  tuning in assets, not in prefab overrides or scene components.

Rebuild the scene with **Tools ▸ BattleForYourSoul ▸ Build Game Scene**. Runtime systems read the
assets on `Awake`, so editing an asset and entering Play mode (no rebuild) is enough to see most
changes.

## Where things live

| What you want to change | Asset | Notes |
|---|---|---|
| Player stats, sprite, body, starting weapon | `Player_Default.asset` | Max HP, move speed, accel/decel, collider size/offset |
| Enemy type | `Enemy_Grunt.asset` | Sprite, HP, speed, contact damage, XP, collider radius |
| Weapon | `Weapon_Default.asset` | Fire rate, spread, damage, projectile speed/lifetime, **projectile sprite** |
| Player level curve | `Progression_Default.asset` | Starting level, base XP, growth |
| Monster stat/XP scaling | `MonsterScaling_Default.asset` | Growth per monster level |
| A stage | `Stages/Stage_01.asset` … | Monster level, kill quota, spawn pacing, **enemy roster** |
| Stage order / end behaviour | `StageProgression_Default.asset` | Ordered stage list, loop-vs-escalate |
| Which upgrades can appear | `UpgradePool_Default.asset` | List of upgrade assets |
| One upgrade | `Upgrades/*.asset` | Name, icon, colour, stat modifiers |
| Rendering (PPU, resolution, iso, sorting) | `GameSettings.asset` | Read by the camera, grid and import pipeline |

## Sprites

1. Drop a PNG anywhere under `Assets/_Project/Art/`.
2. `PixelArtPostprocessor` imports it automatically: Sprite type, Point filter, Uncompressed, no
   mipmaps, Clamp wrap, FullRect mesh, and the PPU from `GameSettings` (32 by default). Alignment is
   left alone, so author your own pivot.
3. Assign the sprite to the matching definition (`Player_Default`, `Enemy_*`, `Weapon_Default`, an
   upgrade's icon, …).

Swapping art never requires a code change or a scene rebuild.

## Stages and the enemy roster

A run walks the ordered list in `StageProgression_Default`:

- Each `StageDefinition` sets the **monster level** enemies spawn at, the **kill quota** that clears
  the stage, the **spawn pacing** (interval, max alive, spawn radius, bounds), and an **enemy
  roster**.
- The roster is a weighted list of `EnemyDefinition`s. One generic enemy prefab backs every type, so
  adding variety is just adding entries with weights.
- After the last stage, the run **escalates**: the final stage's pacing repeats while the monster
  level keeps climbing. Tick `loopStages` on the progression to restart from stage one instead.

To add an enemy type: duplicate `Enemy_Grunt.asset`, give it a sprite and stats, then add it to a
stage's roster.

## Upgrades

`UpgradePool_Default` lists every upgrade a level-up may offer. To add a choice: duplicate an asset
under `Upgrades/`, edit its modifiers, and add it to the pool. Each upgrade carries one or more
`StatModifier`s (`UpgradeStat` + value); percent stats are whole numbers (12 = +12%).

## Adding a new definition type

Definition assets are plain `ScriptableObject`s with `[CreateAssetMenu]`. Runtime state stays on the
scene components, never in the asset. If the builder should create the asset for you, add an
`EnsureX()` in `GameSceneBuilder` that seeds defaults **only when the asset is created** (see
`EnsureAsset` / `SeedReferenceIfMissing`).
