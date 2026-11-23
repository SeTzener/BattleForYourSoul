# Progression Design

Living design doc for how a run progresses. Adjust it as the design changes; code should follow
this document, not the other way around.

> This is a **game-design** doc. Git workflow rules live in `GIT_GALATEO.md`. For how to edit the
> tuning assets, see `AUTHORING.md`.

## Direction

The game is a **Hades-style** action roguelite, not a Vampire-Survivors-style loot collector:

- The run starts in **Samsara**, a no-enemy weapon-selection room with one door and three physical
  weapon pickups. The player can try and swap freely, but must pick a weapon before leaving; the exit
  locks that weapon for the run.
- After Samsara, the run is a sequence of discrete rooms. Enemies are already in the room when it
  starts.
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
- A stage can also be marked `weaponSelectionStage`, which suppresses enemy spawning, shows the
  weapon pickups, and gates the exit on a selection. `activeDoorIndices` limits which shared door
  fixtures are enabled (Samsara uses just one).
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

### Samsara: weapon selection

Samsara is the first stage in `StageProgression_Default`. It is a staging room, not a combat room:

1. No enemies are placed, so the room starts cleared. Three physical pickups — sword, spear and bow —
   stand on pedestals authored in `GameSettings.weaponPedestalPositions`.
2. Walking into a pickup equips it. The player may pick up another at any time; the previously
   selected prop reappears at its original pedestal, so every weapon stays available to try.
3. Only one door is active. It stays locked (no label, closed art) until a weapon has been picked.
4. Walking into the unlocked door calls `WeaponSelection.Lock()` and advances to the next stage. The
   choice is now final for the run: swapping is no longer possible and the pickups are hidden for all
   later stages.

`WeaponSelection` owns the selection state, `WeaponController` receives the chosen definition, and
`LevelDirector` drives the door gate from the stage rule. None of this uses static state or scene-wide
searches; the builder wires the references directly.

### Weapons and two actions

Each weapon has exactly two Input System actions — **primary** and **special** — mapped to
`PrimaryAttack` / `SecondaryAttack`. Gameplay code never reads a keyboard directly. Attacks are
manual: nothing auto-fires.

| Weapon | Primary | Special | Delivery |
|---|---|---|---|
| Sword | Slash | Cleave | close melee arc; wider special |
| Spear | Thrust | Sweep | long line reach; wider special |
| Bow | Shot | Spread | projectile; multi-shot special |

Weapon-specific behaviours such as bow multishot/ricochet and sword longer reach/area impact are
**upgrades**, not extra actions. The two-action input surface never changes.

> For now they're upgrades but don' forget these are just placeholders. The real weapons are gonna be
> different. I just need the mechanics to be in place.

### Dash

A dedicated `Dash` action (Space / gamepad A) dashes the player in their current facing direction.
`PlayerDash` owns the run state: one normal charge with a cooldown, plus optional always-ready
charges from upgrades. While dashing the player ignores all enemy colliders and takes no damage; a
short grace window after the dash protects against an enemy they land on. `PlayerMovement` holds the
dash velocity for the authored duration, then normal movement resumes. A stage teleport cancels an
active dash and clears its immunity and collision overrides.

The seeded **Phase Step** Mobility upgrade grants one instant dash charge. Instant charges are not
consumed permanently — they refill whenever a dash ends — so taking it always leaves one
cooldown-free dash available. Stacking the upgrade grants more consecutive free dashes.

### XP flow

1. Enemy dies → `EnemySpawner.EnemyDied` and its live count drops.
2. `KillXpGranter` reads `Enemy.XpReward` and calls `Experience.AddXp`.
3. When the live count reaches zero → `EnemySpawner.Cleared` → director unlocks doors and queues any
   pending category reward.
4. Player walks into an unlocked door → director picks that category and advances the stage.
5. When `Experience` crosses a threshold, `LevelUpController` pauses and offers an unfiltered upgrade
   choice, queued behind any category reward.

No pickups, no pools for XP, no magnet radius. Samsara grants no XP; the flow above starts on the
stage after the weapon is chosen.

## Upgrades

Level-up choices are `UpgradeDefinition` assets carrying `StatModifier`s, gathered into an
`UpgradePool` asset. Each upgrade may name a `category`; that is how a door-filtered clear choice is
built. `PlayerStats` aggregates the chosen ones and pushes the result into `Health`,
`WeaponController`, `PlayerMovement` and `PlayerDash`.

Seeded placeholder categories: **Shots**, **Mobility**, **Vitality**.

An upgrade can also carry a **weapon filter**. Global upgrades (mobility, vitality and the shared
stats) have an empty filter and roll for every weapon. Weapon-specific effects only appear while the
matching weapon is equipped, so a bow run never sees sword-only cards and vice versa. `PlayerStats`
keeps one set of accumulated totals across weapon swaps inside Samsara, so trying a different weapon
never loses an upgrade.

Current upgrades: projectile damage, fire rate, move speed, instant dash charge, max health, extra
projectile, projectile speed, plus weapon-filtered placeholders — sword reach/edge/impact, spear
reach/broadhead/quick thrust, bow split/ricochet/impact. Ricochet and impact burst are implemented
as inert-by-default effects: they do nothing until their upgrade is taken.

## Decisions log

Agreed choices that shape the design. Update this when a decision changes.

- **Samsara and weapons.** The run starts in a no-enemy weapon-selection room with three physical
  pickups; the chosen weapon is locked for the run on exit. Each weapon has exactly two manual
  actions (primary/special) and never auto-fires. Sword and spear attacks get pooled one-shot hit
  VFX (a slash and a thrust respectively); the bow stays projectile-only. Weapon-specific
  behaviours are upgrade effects, not extra actions. The three weapons, their six actions and the
  effects are placeholders until the real designs and art arrive.
- **Dash.** It uses its own button (Space / gamepad A) rather than reusing the existing Jump action.
  The upgrade effect is **+1 always-ready charge** that refills after each dash — not a global
  cooldown removal — granted by a **new Mobility upgrade** (Phase Step) instead of replacing one of
  the existing speed upgrades. Dash passes through enemies with damage suppression and a short
  grace window afterwards.

## Open questions / to tune

- **Room shape / door count**: three doors on the far edge is the placeholder. Positions live in
  `GameSettings`, so more or fewer can be authored without code.
- **Scaling shape**: linear per level for now. May become a curve asset.
- **Enemy variety per stage**: stages already list placements of any enemy type; more types still to
  author.
- **Category list**: placeholders only; final categories and their upgrades are still to be designed.
- **Weapons**: sword/spear/bow and their six actions are placeholders. The real weapons will be
  different; the milestone establishes the mechanics (pickup, swap, lock, two actions, filtered
  effects) rather than the final combat design.
- **Run end / boss stages**: not modelled yet.
