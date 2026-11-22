# Progression Design

Living design doc for how a run progresses. Adjust it as the design changes; code should follow
this document, not the other way around.

> This is a **game-design** doc. Git workflow rules live in `GIT_GALATEO.md`. For how to edit the
> tuning assets, see `AUTHORING.md`.

## Direction

The game is a **Hades-style** action roguelite, not a Vampire-Survivors-style loot collector:

- No XP pickups. Nothing is gathered off the floor.
- Killing an enemy grants XP **directly**, and the amount comes from **the enemy's level**.
- Level-ups still happen and still offer a choice of upgrades (the level-up mechanic is kept).
- The **player's** XP requirement grows with the player's level.
- The **monster's** level is set by the **stage**, never by the player's level.

Consequence: on an early stage the monsters are weak and always give the same (low) XP, so the
player levels up slowly. As stages progress the monsters get stronger **and** give more XP.

## Model

Two independent levels:

| Level          | Owner        | Grows with            | Drives                          |
|----------------|--------------|-----------------------|---------------------------------|
| Player level   | `Experience` | XP from kills         | XP-to-next curve, upgrade picks |
| Monster level  | `LevelDirector` | Stage progression  | Enemy stats, XP, spawn pacing   |

### Player level

- `Experience` banks XP and resolves level-ups.
- XP-to-next comes from a `ProgressionDefinition` asset: `baseXp * growth^(level - startingLevel)`.
- Each level-up raises `Experience.LeveledUp`; `LevelUpController` pauses and offers upgrades.

### Monster level (the stage)

- Stages are authored as `StageDefinition` assets; `StageProgression_Default` lists them in play
  order. Each stage sets its own **monster level**, **kill quota** and **spawn pacing**, and can list
  several `EnemyDefinition`s with weights (the enemy roster).
- `LevelDirector` walks the list, feeds the current stage to `EnemySpawner` via `Configure`, and
  raises `LevelChanged` on advance.
- **Advance rule: kill quota.** A stage is cleared after its configured number of kills; clearing it
  moves to the next stage. (Placeholder for real rooms/portals later — see "Open questions".)
- After the last stage the run **escalates**: the final stage's pacing repeats while the monster
  level keeps climbing. Setting `loopStages` on the progression restarts from stage one instead.
- `MonsterScaling` (asset) turns a monster level into stat and XP multipliers:
  - health / damage / speed grow linearly per level above 1.
  - XP grows per level above 1.
- An enemy's final XP = `EnemyDefinition.xpReward (level-1 base) * MonsterScaling.XpMultiplier(level)`.
- Enemy health / contact damage / move speed are the definition's base values scaled the same way.

### XP flow

1. Enemy dies → `EnemySpawner.EnemyDied`.
2. `KillXpGranter` reads `Enemy.XpReward` and calls `Experience.AddXp`.
3. `LevelDirector` counts the kill toward the current stage's quota and may raise the monster level.
4. When `Experience` crosses a threshold, `LevelUpController` pauses the run and offers upgrades.

No pickups, no pools for XP, no magnet radius.

## Upgrades

Level-up choices are `UpgradeDefinition` assets carrying `StatModifier`s, gathered into an
`UpgradePool` asset. `PlayerStats` aggregates the chosen ones and pushes the result into `Health`,
`WeaponController` and `PlayerMovement`.

Current upgrades: projectile damage, fire rate, move speed, max health, extra projectile,
projectile speed. (The old pickup-radius upgrade was removed with the pickup system.)

## Open questions / to tune

- **Stage advance**: currently a kill quota per stage. Real Hades uses discrete rooms/portals; the
  quota is a stand-in so the loop can be played and tuned now.
- **Scaling shape**: linear per level for now. May become a curve asset.
- **Enemy variety per stage**: stages already pick from a weighted roster; more enemy types still to
  author.
- **Run end / boss stages**: not modelled yet.
