# Progression Design

Living design doc for how a run progresses. Adjust it as the design changes; code should follow
this document, not the other way around.

> This is a **game-design** doc. Git workflow rules live in `GIT_GALATEO.md`. For how to edit the
> tuning assets, see `AUTHORING.md`.

## Direction

The game is a **Hades-style** action roguelite, not a Vampire-Survivors-style loot collector:

- The run is a sequence of discrete rooms. Enemies are already in the room when it starts.
- Clear every enemy to unlock the exits; then **walk into a door** to advance.
- Each door advertises the reward category that will be offered when the *next* room is cleared, so
  the exit is a choice about future build direction, not just a hallway.
- No XP pickups. Killing an enemy grants XP **directly**, scaled by the enemy's level.
- Level-ups still happen and still offer an unfiltered choice of upgrades.

Consequence: early rooms are weak and give low XP, so the player levels up slowly. Later rooms hit
harder and pay more XP, while the door choice nudges which upgrades can appear at clear time.

## Model

| Level          | Owner        | Grows with            | Drives                          |
|----------------|--------------|-----------------------|---------------------------------|
| Player level   | `Experience` | XP from kills         | XP-to-next curve, upgrade picks |
| Monster level  | `LevelDirector` | Stage progression  | Enemy stats and XP              |

### Player level

- `Experience` banks XP and resolves level-ups.
- XP-to-next comes from a `ProgressionDefinition` asset: `baseXp * growth^(level - startingLevel)`.
- Each level-up raises `Experience.LeveledUp`; `LevelUpController` pauses and offers upgrades.

### Monster level and the room

- Stages are authored as `StageDefinition` assets; `StageProgression_Default` lists them in play
  order. Each stage sets its **monster level** and a fixed `EnemyPlacement[]` (type + position).
- `EnemySpawner` places those enemies once when the stage starts. They are pooled, but there is no
  timer, kill quota or replacement wave.
- Enemies are stationed: they wait until the player enters their `EnemyDefinition.aggroRadius`, then
  chase permanently.
- `LevelDirector` starts the stage, resets the player to `GameSettings.roomEntrance`, and watches
  the spawner's remaining count.
- **Advance rule: clear then walk.** When the last enemy dies, the doors unlock and show their
  category labels. A trigger on an unlocked door calls `LevelDirector.SelectExit`, which commits the
  category and starts the next stage. Locked doors do nothing on contact.
- After the last authored stage the run **escalates**: the final room's placements repeat while
  monster level keeps climbing. Setting `loopStages` on the progression restarts from stage one.
- `MonsterScaling` turns a monster level into stat and XP multipliers (health / damage / speed / XP
  grow linearly above level 1). An enemy's final XP is
  `EnemyDefinition.xpReward * MonsterScaling.XpMultiplier(level)`.

### Door-picked reward categories

- Doors are shared map fixtures in `GameSettings.doorPositions`; each is wired to one
  `UpgradeCategoryDefinition` in the generated scene.
- Picking a door stores a **pending category** on the director. It does not grant anything yet.
- When the following stage is cleared, the director calls
  `LevelUpController.QueueCategoryReward(pendingCategory)`. The controller then offers a **three-card
  choice filtered to that category**, with a title naming the category.
- Category rewards and XP level-ups share one queue, so a category choice never interrupts a panel
  that is already open. A category with fewer than three upgrades falls back to the full pool rather
  than soft-locking the run.

### XP flow

1. Enemy dies → `EnemySpawner.EnemyDied` and its live count drops.
2. `KillXpGranter` reads `Enemy.XpReward` and calls `Experience.AddXp`.
3. When the live count reaches zero → `EnemySpawner.Cleared` → director unlocks doors and queues any
   pending category reward.
4. Player walks into an unlocked door → director picks that category and advances the stage.
5. When `Experience` crosses a threshold, `LevelUpController` pauses and offers an unfiltered upgrade
   choice, queued behind any category reward.

No pickups, no pools for XP, no magnet radius.

## Upgrades

Level-up choices are `UpgradeDefinition` assets carrying `StatModifier`s, gathered into an
`UpgradePool` asset. Each upgrade may name a `category`; that is how a door-filtered clear choice is
built. `PlayerStats` aggregates the chosen ones and pushes the result into `Health`,
`WeaponController` and `PlayerMovement`.

Seeded placeholder categories: **Shots**, **Mobility**, **Vitality**.

Current upgrades: projectile damage, fire rate, move speed, max health, extra projectile, projectile
speed. Ricochet and other new projectile behaviours are out of scope until their own milestone.

## Open questions / to tune

- **Room shape / door count**: three doors on the far edge is the placeholder. Positions live in
  `GameSettings`, so more or fewer can be authored without code.
- **Scaling shape**: linear per level for now. May become a curve asset.
- **Enemy variety per stage**: stages already list placements of any enemy type; more types still to
  author.
- **Category list**: placeholders only; final categories and their upgrades are still to be designed.
- **Run end / boss stages**: not modelled yet.
