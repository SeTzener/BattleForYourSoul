# Progression Design

Living design doc for how a run progresses. Adjust it as the design changes; code should follow
this document, not the other way around.

> This is a **game-design** doc. Git workflow rules live in `GIT_GALATEO.md`. For how to edit the
> tuning assets, see `AUTHORING.md`.

## Direction

The game is a **Hades-style** action roguelite, not a Vampire-Survivors-style loot collector:

- The run starts in **Samsara**, a no-enemy superpower-selection room with three doors and four physical
  superpower pickups. The player can try and swap freely, but must pick a superpower before leaving; the exit
  locks that superpower for the run. Samsara is only the opening room: progression does not loop back to it,
  so superpowers cannot be selected again mid-run.
- After Samsara, the run is a sequence of discrete rooms. Enemies are already in the room when it
  starts.
- Clear every enemy to unlock the exits; then **walk into a door** to advance.
- Each door advertises the reward category that will be offered when the *next* room is cleared, so
  the exit is a choice about future build direction, not just a hallway. The three seeded door
  categories are **Upgrades**, Mobility and Vitality; only **Upgrades** currently has stage cards.
- **Stage upgrades and level-up upgrades are separate pools.** `UpgradeDefinition.source` decides
  where a card can appear, and the two sources never mix. The seven Superstrength uniques below are
  stage-only; everything else remains a level-up card.
- No XP pickups. Killing an enemy grants XP **directly**, scaled by the enemy's level.
- Level-ups still happen and offer a choice drawn only from the level-up source.

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
- A stage can also be marked `superpowerSelectionStage`, which suppresses enemy spawning, shows the
  superpower pickups, and gates the exits on a selection. `activeDoorIndices` limits which shared door
  fixtures are enabled; Samsara leaves it empty so all three are reachable.
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
  that is already open. A category with fewer than three cards falls back to the rest of the **stage
  source only**; it never pulls a level-up card into a stage reward.

### Samsara: superpower selection

Samsara is the first stage in `StageProgression_Default`. It is a staging room, not a combat room:

1. No enemies are placed, so the room starts cleared. Four physical pickups — sword, spear, bow and
   **Superstrength** (fists) — stand on pedestals authored in `GameSettings.superpowerPedestalPositions`.
2. Walking into a pickup equips it. The player may pick up another at any time; the previously
   selected prop reappears at its original pedestal, so every superpower stays available to try.
3. All three doors are active. They stay locked (no label, closed art) until a superpower has been picked.
4. Walking into an unlocked door calls `SuperpowerSelection.Lock()` and advances to the next stage. The
   choice is now final for the run: swapping is no longer possible and the pickups are hidden for all
   later stages.
5. **Samsara only happens once, at the start.** `StageProgression_Default` does not loop, so there is no
   mid-run return to the pickups. Opening Samsara still initialises base stats, health/XP and an empty
   `RunUpgradeState` so every run begins from the same state.

`SuperpowerSelection` owns the selection state, `SuperpowerController` receives the chosen definition, and
`LevelDirector` drives the door gate from the stage rule. None of this uses static state or scene-wide
searches; the builder wires the references directly.

### Superpowers and two actions

Each superpower has exactly two Input System actions — **primary** and **special** — mapped to
`PrimaryAttack` / `SecondaryAttack`. Gameplay code never reads a keyboard directly. Attacks are
manual: nothing auto-fires.

| Superpower | Primary | Special | Delivery |
|---|---|---|---|
| Sword | Slash | Cleave | close melee arc; wider special |
| Spear | Thrust | Sweep | long line reach; wider special |
| Bow | Shot | Spread | projectile; multi-shot special |
| Superstrength | Punch | Uppercut | fast short-range fists; rooted hits and a lunge/knockback special |

Superpower-specific behaviours such as bow multishot/ricochet and sword longer reach/area impact are
**upgrades**, not extra actions. The two-action input surface never changes.

> For now they're upgrades but don' forget these are just placeholders. The real superpowers are gonna be
> different. I just need the mechanics to be in place.

### Superstrength (first real superpower)

Superstrength swaps the placeholder prop for fists. Its inherent **Defense is 12**; every other
superpower has 0. Attacks are manual, with cooldowns matching the placeholder animation lengths, so a
new attack is possible by the time the last frame finishes.

- **Punch** — fast 0.28s primary, short 1.05-unit arc with a **0.5-unit forward step**. It hits in
  front once; every hit roots a plain `Foe` for 0.3s, knocks it back slightly, and disables its
  contact damage for 0.35s so a hit foe cannot punch back at close range.
- **Uppercut** — slower 1.6s special that **moves the player 1.5 world units** in the locked facing
  direction. It is a real body movement, not a physics jump: the upward "hop" is still a **sprite
  offset only**. During the movement the player is **immune to damage** and passes through enemies
  (walls/obstacles still block). The melee query sweeps the whole path and hits **each foe once**,
  with stronger knockback and contact-damage suppression.
- **Attack movement** — a dedicated `AttackMovement` component drives both steps with
  `Rigidbody2D.MovePosition` over the authored duration. Input is suspended for the step, the
  direction is captured once, and the body finishes exactly at the authored distance. It replaced
  the old velocity lunge, which blended with held input and let contacts shove the player off course.
  The attack routine was rewritten around this single step; it never writes the player's transform.
- **Visual/body split** — the player sprite lives on a `Visual` child; the root owns only physics.
  The pose player used to write its hop and captured base position to the root renderer, snapping the
  body back to the button-press position on every attack with **every** superpower. That was the
  source of the strange movement; visual code now refuses to run on the root.
- **Enemy ranks** — `Foe`, `SpecialFoe`, `Boss`. Root and knockback only reach `SpecialFoe` when the
  action opts in; bosses ignore root, stun and knockback entirely. Knockback always pushes away from
  the attacker along the hit direction. **Contact-damage suppression is rank-independent**: a landed
  melee hit briefly makes even a boss harmless, including while it is being knocked back.
- **Level-ups** — hits have a separate 20%-per-card chance to stun for 0.25s, capped at **50%** for
  this superpower. The cap is enforced in `PlayerStats`, not on the asset. A stun is not the same as the
  guaranteed 0.3s `Foe` root; they can overlap.
- **Stage upgrades** — Charge, Super Charge, Power-up, Hulk, Man of Steel, Northern Star Strike and
  Body Slam. They are **stage-clear only** and Superstrength-only. "Unique" means **once per run**;
  level-up cards remain repeatable.
  Hulk and Man of Steel share the `superstrength-form` exclusive group, so they can never both be taken.
  Super Charge requires Charge.
- **Charge** — dashing through a foe damages it once and knocks it back along the dash direction.
- **Super Charge** — **hold** Dash up to 2s for up to 2x Charge damage, then **release** to fire. A tap
  deals the same damage as plain Charge (1x). Charge progress is exposed for a future HUD meter.
- **Northern Star Strike** — replaces Punch with three full-damage hits. Its cooldown is authored at
  twice Punch's because the animation is correspondingly longer.
- **Body Slam** — replaces Uppercut with a heavier arc, the same 1.5-unit immune sweep and a larger
  sprite-only hop.

> **Tune later:** all percentages and damage values above are first-pass placeholders. Treat them as
> tuning knobs, not balancing decisions.

> **Observe:** Northern Star currently deals **full damage on every hit**. Track whether that is too
> strong once three damage frames land reliably; the simple fix is to author a per-hit damage ratio.

> **Observe:** the bonus stun is deliberately separate from the root. Watch whether a rooted-and-stunned
> `Foe` feels over-controlled before changing the chance or the root duration.

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

With Superstrength's **Charge** upgrade, the dash also damages each enemy it passes through once
(with knockback). **Super Charge** turns the dash button into a hold-to-charge input: holding for the
authored 2s doubles that damage, releasing fires the dash, and a quick tap stays at 1x. The charge
window is cancelled when the game is paused.

### XP flow

1. Enemy dies → `EnemySpawner.EnemyDied` and its live count drops.
2. `KillXpGranter` reads `Enemy.XpReward` and calls `Experience.AddXp`.
3. When the live count reaches zero → `EnemySpawner.Cleared` → director unlocks doors and queues any
   pending category reward.
4. Player walks into an unlocked door → director picks that category and advances the stage.
5. When `Experience` crosses a threshold, `LevelUpController` pauses and offers a choice from the
   level-up source only, queued behind any category reward.

No pickups, no pools for XP, no magnet radius. Samsara grants no XP; the flow above starts on the
stage after the superpower is chosen.

## Upgrades

All upgrades are `UpgradeDefinition` assets in `UpgradePool`. `UpgradeDefinition.source` is the hard
split: `LevelUp` cards only appear on XP level-ups; `StageClear` cards only appear after clearing a
room with a pending door category. `category` only groups stage cards; level-ups ignore it.
`PlayerStats` aggregates chosen mods and pushes them into `Health`, `SuperpowerController`,
`PlayerMovement` and `PlayerDash`.

Seeded door categories: **Upgrades**, Mobility and Vitality. Only **Upgrades** currently has authored
stage cards; the other two remain placeholders and fall back to the same stage source rather than
soft-locking the run.

An upgrade can also carry a **superpower filter**. Global upgrades (mobility and vitality) have an
empty filter and roll for every superpower. Superpower-specific effects only appear while the matching
superpower is equipped, so a bow run never sees sword-only cards and vice versa. All projectile cards —
Sharpened Bolts, Rapid Fire, Split Shot and Velocity — are Bow-only, so Superstrength never gets a shot
upgrade. `PlayerStats` keeps one set of accumulated totals across superpower swaps inside Samsara, so
trying a different superpower never loses an upgrade — but entering Samsara again clears everything.

`RunUpgradeState` records picks for the current run. `maxStacks: 1` makes an upgrade unique,
`exclusiveGroup` makes a group mutually exclusive, and `requires` hides a card until its prerequisite
is taken. `LevelUpController` filters with `CanOffer` and marks a pick **before** `PlayerStats.Apply`,
so action overrides are visible on the same frame the card is chosen.

Current **level-up** upgrades:

- Global: Fleet Footed, Swift Stride, Sure Footing, Phase Step, Vitality, Iron Will, Tough Skin.
- Bow-only: Sharpened Bolts, Rapid Fire, Split Shot, Velocity, Split Arrow, Ricochet, Explosive Tip.
- Sword-only: Longer Slash, Impact Wave, Keen Edge.
- Spear-only: Long Reach, Broadhead, Quick Thrust.
- Superstrength-only: Strength, Heavy Fists, Swift Punch, Critical Chance, Critical Damage, Stun
  Chance, Toughness, Vigor, Firm Grip, plus the new tiered Strength-up Low/Mid/High, Critical Hit
  Low/Mid/High and Critical Damage Low/Mid/High.

Current **stage** upgrades: the seven Superstrength-only uniques listed above. Ricochet and impact
burst remain inert-by-default effects: they do nothing until their upgrade is taken.

### Formulas and caps

All caps live in `PlayerStats`; assets only contribute raw modifier values.

| Rule | Formula |
|---|---|
| Damage | `(base + Strength + DamageFlat) * DamagePercent` |
| Hulk low-health attack | `damage *= 1 + 0.5 * (1 - health.Normalized)` (up to +50% near 0 HP) |
| Critical | `chance` capped at 100%; on crit `damage *= 2 + CriticalDamage`, with `CriticalDamage` capped at +50% |
| Defense (Health) | `reduction = min(0.8, defense / (defense + knee))`, knee default 50; incoming damage is multiplied by `1 - reduction` |
| Man of Steel low-health defense | `defense += 40 * (1 - health.Normalized)` on top of its flat +30; refreshed whenever health changes |
| Bonus stun | `chance` capped at 50%; a successful roll adds a 0.25s stun on top of any authored root |

> **TODO — tune later:** every percentage and damage value above is provisional. In particular the
> Hulk/Man of Steel low-health curves, the 0.8 defense cap and the 2x critical baseline need playtest data.

> **TODO — Hulk/Man of Steel sprites:** the current growth/tint hooks are placeholders. Retune the
> scale and tint when the final character sprites arrive; the formulas above should not move with art.

### Experimental tracking (not final design)

- **Guard Break** — `HitEffects.GuardBreak` turns a boss's immunity to knockback into a 1.25x damage
  multiplier instead. It is implemented but no authored action enables it yet. Decide later whether
  bosses should have a breakable guard at all.
- **Hit feedback / crit flash** — melee/line VFX turn yellow and grow 20% on a critical hit. This is a
  cheap readability experiment, not a promised final feedback style.

## Decisions log

Agreed choices that shape the design. Update this when a decision changes.

- **Samsara and superpowers.** The run starts in a no-enemy superpower-selection room with three physical
  pickups; the chosen superpower is locked for the run on exit. Each superpower has exactly two manual
  actions (primary/special) and never auto-fires. Sword and spear attacks get pooled one-shot hit
  VFX (a slash and a thrust respectively); the bow stays projectile-only. Superpower-specific
  behaviours are upgrade effects, not extra actions. The three superpowers, their six actions and the
  effects are placeholders until the real designs and art arrive.
- **Dash.** It uses its own button (Space / gamepad A) rather than reusing the existing Jump action.
  The upgrade effect is **+1 always-ready charge** that refills after each dash — not a global
  cooldown removal — granted by a **new Mobility upgrade** (Phase Step) instead of replacing one of
  the existing speed upgrades. Dash passes through enemies with damage suppression and a short
  grace window afterwards.
- **Superstrength.** First real superpower: fists, fast short-range Punch, Uppercut with a forward
  lunge and knockback, and higher inherent defense. Root/stun/knockback are separated by enemy rank;
  bosses ignore all control. Cooldowns are authored to match the placeholder animations, including
  Northern Star's doubled 3-hit animation. Super Charge **fires on release**, not on press.
- **Samsara reset.** Nothing survives a loop back to the selection room: upgrades, stats and the
  superpower choice are all initialised. Superstrength has no door; its seven unique upgrades are once
  per run, while the other level-up cards repeat.
- **No real jump.** Uppercut and Body Slam offset the sprite upward and move the body 1.5 units
  forward; never add a Rigidbody hop to sell the animation. Damage immunity lasts only for the
  movement window, and dash/attack immunity leases are independent so one ending cannot cancel the
  other.

## Open questions / to tune

- **Room shape / door count**: three doors on the far edge is the current placeholder. Positions live in
  `GameSettings`, so more or fewer can be authored without code. There is deliberately no Superpowers
  door.
- **Scaling shape**: linear per level for now. May become a curve asset.
- **Enemy variety per stage**: stages already list placements of any enemy type and an authored
  `EnemyRank`; more ranks/types still to author.
- **Category list**: Upgrades, Mobility and Vitality are the door categories; Superpowers is an
  authoring-only category. Only Upgrades has stage cards today; final categories remain to be designed.
- **Superpowers**: sword/spear/bow and their six actions remain placeholders. Superstrength is the
  first real design but all its numbers are provisional. Its animation frames are crude stand-ins.
- **Tune percentages/damage later**: every Superstrength value, the defense knee/cap, crit baseline,
  stun chance and Charge/Super Charge multiplier needs a balancing pass. Do not treat the seeded
  values as final. The contact-suppression windows (0.35s Punch / 0.45s Uppercut / 0.5s Body Slam)
  and the step distances (0.5 Punch / 1.5 Uppercut and Body Slam) are provisional too.
- **Observe — stun:** the 20%/50% bonus stun sits on top of a guaranteed 0.3s `Foe` root. Watch for
  perma-control before raising either number.
- **Observe — Northern Star:** each of the three hits currently deals full damage. Track whether that
  out-damages Uppercut by too much; a per-hit ratio can be authored later.
- **Experiment — Guard Break / hit feedback:** boss damage conversion and the crit flash are tracked
  above; keep or cut after playtests.
- **Run end / boss stages**: not modelled yet. `Boss` rank exists and is immune to control effects, but
  no boss stage is authored.
