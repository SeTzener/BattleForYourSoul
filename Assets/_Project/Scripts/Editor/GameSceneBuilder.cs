using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using BattleForYourSoul.Combat;
using BattleForYourSoul.Core;
using BattleForYourSoul.Enemies;
using BattleForYourSoul.Player;
using BattleForYourSoul.Progression;
using BattleForYourSoul.UI;
using BattleForYourSoul.VFX;
using BattleForYourSoul.Weapons;
using BattleForYourSoul.World;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Builds the isometric test scene from scratch: settings asset, prefabs, definitions, isometric
    /// grid, ground tilemap, obstacles, collision walls, visible exit doors, pixel-perfect camera,
    /// 2D light, the combat loop (enemy registry, fixed-placement spawner, player weapon), the
    /// stage/XP progression, category rewards and the HUD.
    ///
    /// This is authoring tooling, not gameplay. Re-running it regenerates the scene deterministically.
    /// Definition/settings assets are seeded only when they are first created, so hand-edited values
    /// and sprites survive a rebuild. Prefabs and the scene are generated artifacts and are rebuilt.
    /// Runtime wiring that depends on C# events is NOT done here (event subscriptions do not
    /// serialize); instead the builder assigns serialized references and the components subscribe in
    /// Awake/OnEnable.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string GameScenePath = ScenesFolder + "/Game.unity";

        private const string SettingsAssetPath = "Assets/_Project/ScriptableObjects/GameSettings.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string EnemyPrefabPath = "Assets/_Project/Prefabs/Enemy.prefab";
        private const string ProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile.prefab";
        private const string AttackVfxPrefabPath = "Assets/_Project/Prefabs/AttackVfx.prefab";
        private const string WeaponDefinitionPath = "Assets/_Project/ScriptableObjects/Weapon_Default.asset";
        private const string WeaponsFolder = "Assets/_Project/ScriptableObjects/Weapons";
        private const string WeaponActionsFolder = "Assets/_Project/ScriptableObjects/Weapons/Actions";
        private const string EnemyDefinitionPath = "Assets/_Project/ScriptableObjects/Enemy_Grunt.asset";
        private const string PlayerDefinitionPath = "Assets/_Project/ScriptableObjects/Player_Default.asset";
        private const string ProgressionDefinitionPath = "Assets/_Project/ScriptableObjects/Progression_Default.asset";
        private const string MonsterScalingPath = "Assets/_Project/ScriptableObjects/MonsterScaling_Default.asset";
        private const string StageProgressionPath = "Assets/_Project/ScriptableObjects/StageProgression_Default.asset";
        private const string UpgradePoolPath = "Assets/_Project/ScriptableObjects/UpgradePool_Default.asset";
        private const string UpgradesFolder = "Assets/_Project/ScriptableObjects/Upgrades";
        private const string UpgradeCategoriesFolder = "Assets/_Project/ScriptableObjects/UpgradeCategories";
        private const string StagesFolder = "Assets/_Project/ScriptableObjects/Stages";

        private const string FloorTileAPath = "Assets/_Project/Art/Environment/Tile_Floor_A.png";
        private const string FloorTileBPath = "Assets/_Project/Art/Environment/Tile_Floor_B.png";
        private const string RockPath = "Assets/_Project/Art/Environment/Prop_Rock.png";
        private const string PlayerSpritePath = "Assets/_Project/Art/Characters/Player.png";
        private const string EnemySpritePath = "Assets/_Project/Art/Characters/Enemy_Grunt.png";
        private const string ProjectileSpritePath = "Assets/_Project/Art/VFX/Projectile_Bolt.png";
        private const string ArrowSpritePath = "Assets/_Project/Art/VFX/Projectile_Arrow.png";
        private const string SlashVfxSpritePath = "Assets/_Project/Art/VFX/VFX_Slash.png";
        private const string ThrustVfxSpritePath = "Assets/_Project/Art/VFX/VFX_Thrust.png";
        private const string SwordSpritePath = "Assets/_Project/Art/Items/Weapon_Sword.png";
        private const string SpearSpritePath = "Assets/_Project/Art/Items/Weapon_Spear.png";
        private const string BowSpritePath = "Assets/_Project/Art/Items/Weapon_Bow.png";
        private const string UpgradeIconSpritePath = "Assets/_Project/Art/UI/Upgrade_Icon.png";
        private const string DoorClosedSpritePath = "Assets/_Project/Art/Environment/Door_Closed.png";
        private const string DoorOpenSpritePath = "Assets/_Project/Art/Environment/Door_Open.png";

        private const int GroundRadiusInCells = 8;
        private const int UpgradeCardCount = 3;
        private const float PlayerMaxHealth = 100f;

        [MenuItem("Tools/BattleForYourSoul/Build Game Scene")]
        public static void BuildGameScene()
        {
            // Create the scene first: EditorSceneManager.NewScene unloads unreferenced asset objects
            // (notably Tile assets), so anything loaded from the AssetDatabase before this call can
            // come back as a "fake null". Loading assets after the scene switch keeps them valid.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Placeholder art must exist before any asset seeding: newly added sprites are loaded by
            // the weapon/action seeders below, and LoadSprite would otherwise cache a null reference.
            EnsurePlaceholderSprites();

            GameSettings settings = EnsureSettings();
            WeaponActions actions = EnsureWeaponActions();
            WeaponDefinition[] weapons = EnsureWeapons(actions);
            EnsureLegacyWeaponDefinition(actions);
            EnemyDefinition enemyDefinition = EnsureEnemyDefinition();
            PlayerDefinition playerDefinition = EnsurePlayerDefinition(weapons.Length > 0 ? weapons[0] : null);
            ProgressionDefinition progression = EnsureProgressionDefinition();
            MonsterScaling scaling = EnsureMonsterScaling();

            var categories = EnsureUpgradeCategories();
            UpgradeDefinition[] upgrades = EnsureUpgrades(categories.shots, categories.mobility, categories.vitality, weapons);
            UpgradePool upgradePool = EnsureUpgradePool(upgrades);
            StageDefinition[] stages = EnsureStages(enemyDefinition);
            StageProgression stageProgression = EnsureStageProgression(stages);

            GameObject playerPrefab = EnsurePlayerPrefab(settings, playerDefinition);
            GameObject enemyPrefab = EnsureEnemyPrefab(settings, enemyDefinition);
            Sprite projectileSprite = actions.BowShot != null ? actions.BowShot.ProjectileSprite : LoadSprite(ProjectileSpritePath);
            GameObject projectilePrefab = EnsureProjectilePrefab(settings, projectileSprite);
            GameObject attackVfxPrefab = EnsureAttackVfxPrefab(settings);

            TileBase floorA = EnsureTile("Tile_Floor_A", FloorTileAPath);
            TileBase floorB = EnsureTile("Tile_Floor_B", FloorTileBPath);

            var environment = new GameObject("Environment");

            BuildGrid(environment.transform, settings, floorA, floorB);
            BuildObstacles(environment.transform, settings);
            BuildWalls(environment.transform);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = new Vector3(settings.RoomEntrance.x, settings.RoomEntrance.y, 0f);

            (EnemySpawner spawner, WeaponSelection selection) = BuildCombat(player, enemyDefinition, enemyPrefab, projectilePrefab, attackVfxPrefab, weapons, settings);
            (Experience experience, LevelDirector levelDirector) = BuildProgression(player, spawner, scaling, progression, stageProgression, playerDefinition);

            StageDoor[] doors = BuildDoors(environment.transform, settings, categories, levelDirector);
            CameraFollow cameraFollow = BuildCamera(player.transform, settings);
            BuildLight();
            LevelUpController levelUp = BuildHud(player, spawner, experience, levelDirector, upgradePool, selection);

            WireDirector(levelDirector, settings, player, cameraFollow, levelUp, doors, selection);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log($"[GameSceneBuilder] Built {GameScenePath}");
        }

        /// <summary>
        /// Loads an asset or creates it if missing. The returned <c>created</c> flag tells the caller
        /// whether to seed defaults: existing assets are never touched, so authored values survive.
        /// </summary>
        private static (T asset, bool created) EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return (existing, false);
            }

            EnsureFolder(System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/'));
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return (created, true);
        }

        private static GameSettings EnsureSettings()
        {
            var (settings, _) = EnsureAsset<GameSettings>(SettingsAssetPath);
            if (settings == null)
            {
                return settings;
            }

            // Existing assets predate the room fixtures; seed only when the list is empty.
            var serialized = new SerializedObject(settings);
            SerializedProperty doors = serialized.FindProperty("doorPositions");
            if (doors != null && doors.arraySize == 0)
            {
                var defaults = new[]
                {
                    new Vector2(-3.2f, 3.55f),
                    new Vector2(0f, 3.85f),
                    new Vector2(3.2f, 3.55f),
                };

                doors.arraySize = defaults.Length;
                for (int i = 0; i < defaults.Length; i++)
                {
                    doors.GetArrayElementAtIndex(i).vector2Value = defaults[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            // Samsara's props are a new required fixture; backfill them without touching authored doors.
            var pedestals = serialized.FindProperty("weaponPedestalPositions");
            if (pedestals != null && pedestals.arraySize == 0)
            {
                var defaults = new[]
                {
                    new Vector2(-4f, -1.2f),
                    new Vector2(0f, -1.8f),
                    new Vector2(4f, -1.2f),
                };

                pedestals.arraySize = defaults.Length;
                for (int i = 0; i < defaults.Length; i++)
                {
                    pedestals.GetArrayElementAtIndex(i).vector2Value = defaults[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        private static (UpgradeCategoryDefinition shots, UpgradeCategoryDefinition mobility, UpgradeCategoryDefinition vitality) EnsureUpgradeCategories()
        {
            EnsureFolder(UpgradeCategoriesFolder);

            return (
                EnsureCategory("UpgradeCategory_Shots", "Shots", "Sharpen bolts and bend their flight.", new Color32(244, 152, 88, 255)),
                EnsureCategory("UpgradeCategory_Mobility", "Mobility", "Move faster and outmaneuver the room.", new Color32(112, 220, 156, 255)),
                EnsureCategory("UpgradeCategory_Vitality", "Vitality", "Survive longer under pressure.", new Color32(236, 120, 160, 255)));
        }

        private static UpgradeCategoryDefinition EnsureCategory(string fileName, string displayName, string description, Color32 color)
        {
            string path = $"{UpgradeCategoriesFolder}/{fileName}.asset";
            var (category, created) = EnsureAsset<UpgradeCategoryDefinition>(path);
            if (!created)
            {
                return category;
            }

            var serialized = new SerializedObject(category);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("color").colorValue = color;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(category);
            AssetDatabase.SaveAssets();
            return category;
        }

        private static GameObject EnsurePlayerPrefab(GameSettings settings, PlayerDefinition definition)
        {
            // Rebuilt from scratch so component changes here are picked up deterministically.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(PlayerPrefabPath);
            }

            var player = new GameObject("Player");
            player.layer = LayerMask.NameToLayer("Player");
            player.tag = "Player";

            var spriteRenderer = player.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = definition != null ? definition.Sprite : LoadSprite(PlayerSpritePath);
            spriteRenderer.sortingLayerName = settings.EntitySortingLayer;
            player.AddComponent<IsometricSpriteSorter>();

            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = definition != null ? definition.ColliderSize : new Vector2(0.45f, 0.5f);
            collider.offset = definition != null ? definition.ColliderOffset : new Vector2(0f, 0.25f);

            player.AddComponent<PlayerInputReader>();

            var movement = player.AddComponent<PlayerMovement>();
            SetSerializedReference(movement, "definition", definition);

            var visuals = player.AddComponent<PlayerVisuals>();
            SetSerializedReference(visuals, "definition", definition);

            var health = player.AddComponent<Health>();
            SetSerializedFloat(health, "maxHealth", definition != null ? definition.MaxHealth : PlayerMaxHealth);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            return prefab;
        }

        private static GameObject EnsureEnemyPrefab(GameSettings settings, EnemyDefinition definition)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(EnemyPrefabPath);
            }

            var enemy = new GameObject("Enemy");
            enemy.layer = LayerMask.NameToLayer("Enemy");

            // The sprite lives on a child so presentation never affects the physics collider.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(enemy.transform, false);
            var spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = definition != null ? definition.Sprite : LoadSprite(EnemySpritePath);
            spriteRenderer.sortingLayerName = settings.EntitySortingLayer;
            visual.AddComponent<IsometricSpriteSorter>();

            var body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = enemy.AddComponent<CircleCollider2D>();
            collider.radius = definition != null ? definition.ColliderRadius : 0.28f;

            var health = enemy.AddComponent<Health>();
            var contactDamage = enemy.AddComponent<ContactDamage>();

            var enemyComponent = enemy.AddComponent<Enemy>();
            SetSerializedReference(enemyComponent, "health", health);
            SetSerializedReference(enemyComponent, "contactDamage", contactDamage);
            SetSerializedReference(enemyComponent, "spriteRenderer", spriteRenderer);
            SetSerializedReference(enemyComponent, "bodyCollider", collider);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPrefabPath);
            Object.DestroyImmediate(enemy);
            return prefab;
        }

        private static GameObject EnsureProjectilePrefab(GameSettings settings, Sprite projectileSprite)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(ProjectilePrefabPath);
            }

            var projectile = new GameObject("Projectile");
            projectile.layer = LayerMask.NameToLayer("Projectile");

            var spriteRenderer = projectile.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = projectileSprite != null ? projectileSprite : LoadSprite(ProjectileSpritePath);
            spriteRenderer.sortingLayerName = settings.VfxSortingLayer;
            spriteRenderer.sortingOrder = 0;

            var body = projectile.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = projectile.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.12f;

            var projectileComponent = projectile.AddComponent<Projectile>();
            SetSerializedReference(projectileComponent, "spriteRenderer", spriteRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(projectile, ProjectilePrefabPath);
            Object.DestroyImmediate(projectile);
            return prefab;
        }

        /// <summary>
        /// Builds the shared one-shot effect prefab. It sits on the VFX layer and uses the same
        /// world-Y sorting rule as every other sprite, with a small offset so it draws in front.
        /// </summary>
        private static GameObject EnsureAttackVfxPrefab(GameSettings settings)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AttackVfxPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(AttackVfxPrefabPath);
            }

            var effect = new GameObject("AttackVfx");

            var spriteRenderer = effect.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = LoadSprite(SlashVfxSpritePath);
            spriteRenderer.sortingLayerName = settings.VfxSortingLayer;
            spriteRenderer.sortingOrder = 0;

            var sorter = effect.AddComponent<IsometricSpriteSorter>();
            SetSerializedReference(sorter, "targetRenderer", spriteRenderer);
            SetSerializedInt(sorter, "orderOffset", 10);

            var attackVfx = effect.AddComponent<AttackVfx>();
            SetSerializedReference(attackVfx, "spriteRenderer", spriteRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(effect, AttackVfxPrefabPath);
            Object.DestroyImmediate(effect);
            return prefab;
        }

        private struct WeaponActions
        {
            public WeaponActionDefinition SwordSlash;
            public WeaponActionDefinition SwordCleave;
            public WeaponActionDefinition SpearThrust;
            public WeaponActionDefinition SpearSweep;
            public WeaponActionDefinition BowShot;
            public WeaponActionDefinition BowSpread;
        }

        private static WeaponActions EnsureWeaponActions()
        {
            EnsureFolder(WeaponActionsFolder);

            return new WeaponActions
            {
                SwordSlash = EnsureWeaponAction("Action_Sword_Slash", "Slash", WeaponAttackShape.MeleeArc, 0.42f, 2f, 1.2f, 110f, 0.4f, 1, 0f, 14f, 1f, null, LoadSprite(SlashVfxSpritePath), new Color32(250, 250, 255, 255), 1.6f, 0.18f, 90f),
                SwordCleave = EnsureWeaponAction("Action_Sword_Cleave", "Cleave", WeaponAttackShape.MeleeArc, 1.4f, 3f, 1.5f, 200f, 0.4f, 1, 0f, 14f, 1f, null, LoadSprite(SlashVfxSpritePath), new Color32(255, 238, 210, 255), 2.2f, 0.24f, 160f),
                SpearThrust = EnsureWeaponAction("Action_Spear_Thrust", "Thrust", WeaponAttackShape.Line, 0.5f, 2f, 2f, 360f, 0.35f, 1, 0f, 14f, 1f, null, LoadSprite(ThrustVfxSpritePath), new Color32(238, 248, 255, 255), 2.2f, 0.16f, 0f),
                SpearSweep = EnsureWeaponAction("Action_Spear_Sweep", "Sweep", WeaponAttackShape.Line, 1.5f, 3f, 2.4f, 360f, 0.9f, 1, 0f, 14f, 1f, null, LoadSprite(ThrustVfxSpritePath), new Color32(255, 240, 210, 255), 2.8f, 0.22f, 0f),
                BowShot = EnsureWeaponAction("Action_Bow_Shot", "Shot", WeaponAttackShape.Projectile, 0.34f, 1f, 8f, 360f, 0.4f, 1, 0f, 15f, 1.1f, LoadSprite(ArrowSpritePath), null, Color.white, 1.4f, 0.22f, 0f),
                BowSpread = EnsureWeaponAction("Action_Bow_Spread", "Spread", WeaponAttackShape.Projectile, 1.5f, 1f, 8f, 360f, 0.4f, 2, 22f, 12f, 1f, LoadSprite(ArrowSpritePath), null, Color.white, 1.4f, 0.22f, 0f),
            };
        }

        private static WeaponActionDefinition EnsureWeaponAction(
            string fileName,
            string displayName,
            WeaponAttackShape shape,
            float cooldown,
            float damage,
            float range,
            float arcDegrees,
            float lineWidth,
            int projectilesPerShot,
            float spreadDegrees,
            float projectileSpeed,
            float projectileLifetime,
            Sprite projectileSprite,
            Sprite hitVfxSprite,
            Color hitVfxColor,
            float hitVfxSize,
            float hitVfxDuration,
            float hitVfxSweep)
        {
            string path = $"{WeaponActionsFolder}/{fileName}.asset";
            var (action, created) = EnsureAsset<WeaponActionDefinition>(path);

            // Placeholder art may have been generated after a previous build seeded this action, so
            // backfill missing references without overwriting anything a designer has authored.
            if (projectileSprite != null)
            {
                SeedReferenceIfMissing(action, "projectileSprite", projectileSprite);
            }

            SeedHitVfxFields(action, hitVfxSprite, hitVfxColor, hitVfxSize, hitVfxDuration, hitVfxSweep);

            if (!created)
            {
                return action;
            }

            var serialized = new SerializedObject(action);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("shape").enumValueIndex = (int)shape;
            serialized.FindProperty("cooldown").floatValue = cooldown;
            serialized.FindProperty("damage").floatValue = damage;
            serialized.FindProperty("range").floatValue = range;
            serialized.FindProperty("arcDegrees").floatValue = arcDegrees;
            serialized.FindProperty("lineWidth").floatValue = lineWidth;
            serialized.FindProperty("projectilesPerShot").intValue = projectilesPerShot;
            serialized.FindProperty("spreadDegrees").floatValue = spreadDegrees;
            serialized.FindProperty("projectileSpeed").floatValue = projectileSpeed;
            serialized.FindProperty("projectileLifetime").floatValue = projectileLifetime;
            serialized.FindProperty("projectileSprite").objectReferenceValue = projectileSprite;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssets();
            return action;
        }

        private static WeaponDefinition[] EnsureWeapons(WeaponActions actions)
        {
            EnsureFolder(WeaponsFolder);

            return new[]
            {
                EnsureWeapon("Weapon_Sword", "sword", "Sword", "Close-range slash and a wide cleave.",
                    SwordSpritePath, actions.SwordSlash, actions.SwordCleave),
                EnsureWeapon("Weapon_Spear", "spear", "Spear", "A two-block thrust and a broad sweep.",
                    SpearSpritePath, actions.SpearThrust, actions.SpearSweep),
                EnsureWeapon("Weapon_Bow", "bow", "Bow", "Long-range shot and a spread volley.",
                    BowSpritePath, actions.BowShot, actions.BowSpread),
            };
        }

        private static WeaponDefinition EnsureWeapon(
            string fileName,
            string id,
            string displayName,
            string description,
            string spritePath,
            WeaponActionDefinition primary,
            WeaponActionDefinition special)
        {
            string path = $"{WeaponsFolder}/{fileName}.asset";
            var (weapon, created) = EnsureAsset<WeaponDefinition>(path);
            if (created)
            {
                var serialized = new SerializedObject(weapon);
                serialized.FindProperty("id").stringValue = id;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("description").stringValue = description;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
                AssetDatabase.SaveAssets();
            }

            // Newly required references are backfilled so older assets keep their authored values.
            SeedReferenceIfMissing(weapon, "pickupSprite", LoadSprite(spritePath));
            SeedReferenceIfMissing(weapon, "primaryAction", primary);
            SeedReferenceIfMissing(weapon, "specialAction", special);
            return weapon;
        }

        private static void EnsureLegacyWeaponDefinition(WeaponActions actions)
        {
            var (definition, _) = EnsureAsset<WeaponDefinition>(WeaponDefinitionPath);
            SeedReferenceIfMissing(definition, "primaryAction", actions.BowShot);
            SeedReferenceIfMissing(definition, "specialAction", actions.BowSpread);
            SeedReferenceIfMissing(definition, "pickupSprite", LoadSprite(BowSpritePath));
        }

        private static EnemyDefinition EnsureEnemyDefinition()
        {
            var (definition, created) = EnsureAsset<EnemyDefinition>(EnemyDefinitionPath);
            if (created)
            {
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            SeedReferenceIfMissing(definition, "sprite", LoadSprite(EnemySpritePath));
            return definition;
        }

        private static PlayerDefinition EnsurePlayerDefinition(WeaponDefinition defaultWeapon)
        {
            var (definition, created) = EnsureAsset<PlayerDefinition>(PlayerDefinitionPath);
            if (created)
            {
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            SeedReferenceIfMissing(definition, "sprite", LoadSprite(PlayerSpritePath));
            SeedReferenceIfMissing(definition, "startingWeapon", defaultWeapon);
            return definition;
        }

        private static ProgressionDefinition EnsureProgressionDefinition()
        {
            var (progression, _) = EnsureAsset<ProgressionDefinition>(ProgressionDefinitionPath);
            return progression;
        }

        private static MonsterScaling EnsureMonsterScaling()
        {
            var (scaling, _) = EnsureAsset<MonsterScaling>(MonsterScalingPath);
            return scaling;
        }

        private static UpgradeDefinition[] EnsureUpgrades(
            UpgradeCategoryDefinition shots,
            UpgradeCategoryDefinition mobility,
            UpgradeCategoryDefinition vitality,
            WeaponDefinition[] weapons)
        {
            EnsureFolder(UpgradesFolder);
            Sprite icon = LoadSprite(UpgradeIconSpritePath);

            var upgrades = new[]
            {
                EnsureUpgrade("Upgrade_SharpenedBolts", "sharpened-bolts", "Sharpened Bolts", "Increase projectile damage by 1.", new Color32(232, 96, 96, 255), icon, shots, UpgradeStat.DamageFlat, 1f),
                EnsureUpgrade("Upgrade_RapidFire", "rapid-fire", "Rapid Fire", "Fire 12% faster.", new Color32(240, 168, 72, 255), icon, shots, UpgradeStat.FireRatePercent, 12f),
                EnsureUpgrade("Upgrade_SplitShot", "split-shot", "Split Shot", "Fire one extra projectile.", new Color32(178, 130, 240, 255), icon, shots, UpgradeStat.ExtraProjectiles, 1f),
                EnsureUpgrade("Upgrade_Velocity", "velocity", "Velocity", "Projectiles travel 15% faster.", new Color32(120, 210, 240, 255), icon, shots, UpgradeStat.ProjectileSpeedPercent, 15f),

                EnsureUpgrade("Upgrade_FleetFooted", "fleet-footed", "Fleet Footed", "Move 8% faster.", new Color32(120, 214, 120, 255), icon, mobility, UpgradeStat.MoveSpeedPercent, 8f),
                EnsureUpgrade("Upgrade_SwiftStride", "swift-stride", "Swift Stride", "Move 10% faster.", new Color32(128, 232, 180, 255), icon, mobility, UpgradeStat.MoveSpeedPercent, 10f),
                EnsureUpgrade("Upgrade_SureFooting", "sure-footing", "Sure Footing", "Move 6% faster.", new Color32(156, 226, 142, 255), icon, mobility, UpgradeStat.MoveSpeedPercent, 6f),

                EnsureUpgrade("Upgrade_Vitality", "vitality", "Vitality", "Gain 20 max health.", new Color32(240, 120, 168, 255), icon, vitality, UpgradeStat.MaxHealthFlat, 20f),
                EnsureUpgrade("Upgrade_IronWill", "iron-will", "Iron Will", "Gain 14 max health.", new Color32(250, 150, 180, 255), icon, vitality, UpgradeStat.MaxHealthFlat, 14f),
                EnsureUpgrade("Upgrade_ToughSkin", "tough-skin", "Tough Skin", "Gain 12 max health.", new Color32(232, 160, 190, 255), icon, vitality, UpgradeStat.MaxHealthFlat, 12f),

                // Weapon-specific placeholders: each weapon has three so a filtered choice stays full.
                EnsureUpgrade("Upgrade_Bow_Split", "bow-split", "Split Arrow", "Fire one additional arrow.", new Color32(244, 176, 96, 255), icon, shots, UpgradeStat.ExtraProjectiles, 1f, weaponFilter(weapons,2)),
                EnsureUpgrade("Upgrade_Bow_Ricochet", "bow-ricochet", "Ricochet", "Arrows bounce to one more target.", new Color32(252, 200, 120, 255), icon, shots, UpgradeStat.RicochetBounces, 1f, weaponFilter(weapons,2)),
                EnsureUpgrade("Upgrade_Bow_Impact", "bow-impact", "Explosive Tip", "Hits burst in a small area.", new Color32(248, 148, 104, 255), icon, shots, UpgradeStat.ImpactRadius, 0.35f, weaponFilter(weapons,2)),

                EnsureUpgrade("Upgrade_Sword_Reach", "sword-reach", "Longer Slash", "Sword attacks reach half a block further.", new Color32(212, 172, 236, 255), icon, vitality, UpgradeStat.RangeFlat, 0.5f, weaponFilter(weapons,0)),
                EnsureUpgrade("Upgrade_Sword_Impact", "sword-impact", "Impact Wave", "Sword hits leave a small area of impact.", new Color32(228, 144, 220, 255), icon, vitality, UpgradeStat.ImpactRadius, 0.3f, weaponFilter(weapons,0)),
                EnsureUpgrade("Upgrade_Sword_Edge", "sword-edge", "Keen Edge", "Sword attacks deal one more damage.", new Color32(238, 196, 248, 255), icon, vitality, UpgradeStat.DamageFlat, 1f, weaponFilter(weapons,0)),

                EnsureUpgrade("Upgrade_Spear_Reach", "spear-reach", "Long Reach", "Spear attacks reach half a block further.", new Color32(132, 236, 176, 255), icon, mobility, UpgradeStat.RangeFlat, 0.5f, weaponFilter(weapons,1)),
                EnsureUpgrade("Upgrade_Spear_Broadhead", "spear-broadhead", "Broadhead", "Spear attacks deal 20% more damage.", new Color32(160, 244, 196, 255), icon, mobility, UpgradeStat.DamagePercent, 20f, weaponFilter(weapons,1)),
                EnsureUpgrade("Upgrade_Spear_Quick", "spear-quick", "Quick Thrust", "Spear attacks recover 15% faster.", new Color32(184, 248, 212, 255), icon, mobility, UpgradeStat.FireRatePercent, 15f, weaponFilter(weapons,1)),
            };

            return upgrades;
        }

        private static WeaponDefinition[] weaponFilter(WeaponDefinition[] weapons, int index)
        {
            return weapons != null && index < weapons.Length ? new[] { weapons[index] } : new WeaponDefinition[0];
        }

        private static UpgradeDefinition EnsureUpgrade(
            string fileName,
            string id,
            string displayName,
            string description,
            Color32 color,
            Sprite icon,
            UpgradeCategoryDefinition category,
            UpgradeStat stat,
            float value)
        {
            return EnsureUpgrade(fileName, id, displayName, description, color, icon, category, stat, value, null);
        }

        private static UpgradeDefinition EnsureUpgrade(
            string fileName,
            string id,
            string displayName,
            string description,
            Color32 color,
            Sprite icon,
            UpgradeCategoryDefinition category,
            UpgradeStat stat,
            float value,
            WeaponDefinition[] weaponFilter)
        {
            string path = $"{UpgradesFolder}/{fileName}.asset";
            var (upgrade, created) = EnsureAsset<UpgradeDefinition>(path);

            // Category is required for filtered rewards, so backfill it on older assets too.
            SeedReferenceIfMissing(upgrade, "category", category);

            if (!created)
            {
                if (weaponFilter != null && weaponFilter.Length > 0)
                {
                    SeedArrayIfEmpty(upgrade, "weaponFilter", weaponFilter);
                }

                return upgrade;
            }

            var serialized = new SerializedObject(upgrade);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("color").colorValue = color;
            serialized.FindProperty("icon").objectReferenceValue = icon;

            SerializedProperty modifiers = serialized.FindProperty("modifiers");
            modifiers.arraySize = 1;
            SerializedProperty element = modifiers.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("stat").enumValueIndex = (int)stat;
            element.FindPropertyRelative("value").floatValue = value;

            SetSerializedArray(upgrade, "weaponFilter", weaponFilter ?? new WeaponDefinition[0]);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(upgrade);
            AssetDatabase.SaveAssets();
            return upgrade;
        }

        private static UpgradePool EnsureUpgradePool(UpgradeDefinition[] upgrades)
        {
            var (pool, created) = EnsureAsset<UpgradePool>(UpgradePoolPath);
            if (created)
            {
                SetSerializedArray(pool, "upgrades", upgrades);
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssets();
            }
            else
            {
                AppendMissingReferences(pool, "upgrades", upgrades);
            }

            return pool;
        }

        private static StageDefinition[] EnsureStages(EnemyDefinition enemy)
        {
            EnsureFolder(StagesFolder);

            var stageOne = new[]
            {
                new Vector2(-5.5f, 1.5f), new Vector2(-3.5f, 2f),
                new Vector2(-1.5f, 0.5f), new Vector2(0.5f, 1.2f),
                new Vector2(2.5f, 0.2f), new Vector2(4.5f, 1.6f),
                new Vector2(-2f, -1.2f), new Vector2(3f, -1f),
            };

            var stageTwo = new[]
            {
                new Vector2(-6f, -0.5f), new Vector2(-5f, 2f),
                new Vector2(-3f, 0.5f), new Vector2(-1f, 2.2f),
                new Vector2(1f, 0.8f), new Vector2(3f, 2.2f),
                new Vector2(5f, 0.5f), new Vector2(6f, 2f),
                new Vector2(-2f, -1.5f), new Vector2(2f, -1.2f),
            };

            var stageThree = new[]
            {
                new Vector2(-6.5f, 1f), new Vector2(-5f, 2.6f),
                new Vector2(-3.5f, -0.5f), new Vector2(-2.5f, 2f),
                new Vector2(-0.5f, 1f), new Vector2(1f, 2.6f),
                new Vector2(2.5f, 0.5f), new Vector2(4f, 2f),
                new Vector2(5.5f, -0.5f), new Vector2(6f, 2.6f),
                new Vector2(-4.5f, -1.8f), new Vector2(4f, -1.5f),
            };

            return new[]
            {
                EnsureStage("Stage_Samsara", "Samsara", 1, enemy, new Vector2[0], true, new[] { 1 }),
                EnsureStage("Stage_01", "Stage 1", 1, enemy, stageOne),
                EnsureStage("Stage_02", "Stage 2", 2, enemy, stageTwo),
                EnsureStage("Stage_03", "Stage 3", 3, enemy, stageThree),
            };
        }

        private static StageDefinition EnsureStage(
            string fileName,
            string displayName,
            int monsterLevel,
            EnemyDefinition enemy,
            Vector2[] positions,
            bool weaponSelectionStage = false,
            int[] activeDoorIndices = null)
        {
            string path = $"{StagesFolder}/{fileName}.asset";
            var (stage, created) = EnsureAsset<StageDefinition>(path);
            var serialized = new SerializedObject(stage);
            bool dirty = false;

            if (created)
            {
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("monsterLevel").intValue = monsterLevel;
                serialized.FindProperty("weaponSelectionStage").boolValue = weaponSelectionStage;
                if (activeDoorIndices != null)
                {
                    var indices = serialized.FindProperty("activeDoorIndices");
                    indices.arraySize = activeDoorIndices.Length;
                    for (int i = 0; i < activeDoorIndices.Length; i++)
                    {
                        indices.GetArrayElementAtIndex(i).intValue = activeDoorIndices[i];
                    }
                }

                dirty = true;
            }

            // Older stages used timed waves; backfill their fixed encounter when it is still empty.
            // Samsara deliberately stays empty, so it must never receive default enemies.
            SerializedProperty placements = serialized.FindProperty("enemyPlacements");
            if (!weaponSelectionStage && placements != null && placements.arraySize == 0)
            {
                placements.arraySize = positions.Length;
                for (int i = 0; i < positions.Length; i++)
                {
                    SerializedProperty entry = placements.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("enemy").objectReferenceValue = enemy;
                    entry.FindPropertyRelative("position").vector2Value = positions[i];
                }

                dirty = true;
            }

            if (!dirty)
            {
                return stage;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            return stage;
        }

        private static StageProgression EnsureStageProgression(StageDefinition[] stages)
        {
            var (progression, created) = EnsureAsset<StageProgression>(StageProgressionPath);
            if (created)
            {
                SetSerializedArray(progression, "stages", stages);
                EditorUtility.SetDirty(progression);
                AssetDatabase.SaveAssets();
                return progression;
            }

            // Runs must start in Samsara. Insert it once if an existing progression predates it,
            // without reordering or overwriting any stage the designer has authored since.
            StageDefinition intro = stages.Length > 0 ? stages[0] : null;
            if (intro == null)
            {
                return progression;
            }

            var serialized = new SerializedObject(progression);
            SerializedProperty property = serialized.FindProperty("stages");
            bool present = false;
            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue == intro)
                {
                    present = true;
                    break;
                }
            }

            if (!present)
            {
                property.InsertArrayElementAtIndex(0);
                property.GetArrayElementAtIndex(0).objectReferenceValue = intro;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(progression);
                AssetDatabase.SaveAssets();
            }

            return progression;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            // AssetDatabase.CreateFolder needs an existing parent, so create ancestors first. This
            // matters for nested authoring folders such as Weapons/Actions.
            string parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        /// <summary>
        /// Generates the throwaway placeholder art once when any required sprite is missing. On a
        /// normal rebuild the committed PNGs already exist and this is a handful of cheap loads.
        /// </summary>
        private static void EnsurePlaceholderSprites()
        {
            if (LoadSprite(PlayerSpritePath) == null ||
                LoadSprite(EnemySpritePath) == null ||
                LoadSprite(ProjectileSpritePath) == null ||
                LoadSprite(SwordSpritePath) == null ||
                LoadSprite(SpearSpritePath) == null ||
                LoadSprite(BowSpritePath) == null ||
                LoadSprite(ArrowSpritePath) == null ||
                LoadSprite(SlashVfxSpritePath) == null ||
                LoadSprite(ThrustVfxSpritePath) == null)
            {
                PlaceholderArtGenerator.GenerateAll();
            }
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static TileBase EnsureTile(string name, string spritePath)
        {
            string tilePath = $"Assets/_Project/ScriptableObjects/{name}.asset";
            var (tile, created) = EnsureAsset<Tile>(tilePath);
            if (created)
            {
                tile.name = name;
                tile.sprite = LoadSprite(spritePath);
                tile.color = Color.white;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                AssetDatabase.SaveAssets();
            }

            return tile;
        }

        private static void BuildGrid(Transform parent, GameSettings settings, TileBase floorA, TileBase floorB)
        {
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(parent, false);

            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, settings.IsometricRatio, 0f);
            grid.cellGap = Vector3.zero;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform, false);

            var tilemap = groundGo.AddComponent<Tilemap>();
            var tilemapRenderer = groundGo.AddComponent<TilemapRenderer>();
            tilemapRenderer.mode = TilemapRenderer.Mode.Chunk;
            tilemapRenderer.sortingLayerName = settings.GroundSortingLayer;
            tilemapRenderer.sortingOrder = -1000;

            for (int x = -GroundRadiusInCells; x <= GroundRadiusInCells; x++)
            {
                for (int y = -GroundRadiusInCells; y <= GroundRadiusInCells; y++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    tilemap.SetTile(cell, ((x + y) & 1) == 0 ? floorA : floorB);
                }
            }
        }

        private static void BuildObstacles(Transform parent, GameSettings settings)
        {
            var sprite = LoadSprite(RockPath);
            var positions = new[]
            {
                new Vector2(-2f, 1f),
                new Vector2(2.5f, -0.5f),
                new Vector2(0.5f, 2.5f),
                new Vector2(-3f, -1.5f),
                new Vector2(3.5f, 1.5f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var rock = new GameObject($"Prop_Rock_{i}");
                rock.transform.SetParent(parent, false);
                rock.layer = LayerMask.NameToLayer("Environment");
                rock.transform.position = positions[i];

                var spriteRenderer = rock.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingLayerName = settings.EntitySortingLayer;
                rock.AddComponent<IsometricSpriteSorter>();

                var collider = rock.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.7f, 0.35f);
                collider.offset = new Vector2(0f, 0.175f);
            }
        }

        private static void BuildWalls(Transform parent)
        {
            // Invisible collision walls keep the player on the test floor.
            CreateWall(parent, "Wall_Left", new Vector2(-8.5f, 0f), new Vector2(1f, 12f));
            CreateWall(parent, "Wall_Right", new Vector2(8.5f, 0f), new Vector2(1f, 12f));
            CreateWall(parent, "Wall_Bottom", new Vector2(0f, -4.5f), new Vector2(18f, 1f));
            CreateWall(parent, "Wall_Top", new Vector2(0f, 4.5f), new Vector2(18f, 1f));
        }

        private static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.layer = LayerMask.NameToLayer("Environment");
            wall.transform.position = position;

            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static (EnemySpawner, WeaponSelection) BuildCombat(
            GameObject player,
            EnemyDefinition enemyDefinition,
            GameObject enemyPrefab,
            GameObject projectilePrefab,
            GameObject attackVfxPrefab,
            WeaponDefinition[] weapons,
            GameSettings settings)
        {
            var registryGo = new GameObject("Enemy Registry");
            var registry = registryGo.AddComponent<EnemyRegistry>();

            var spawnerGo = new GameObject("Enemy Spawner");
            var spawner = spawnerGo.AddComponent<EnemySpawner>();
            SetSerializedReference(spawner, "enemyPrefab", enemyPrefab.GetComponent<Enemy>());
            SetSerializedReference(spawner, "definition", enemyDefinition);
            SetSerializedReference(spawner, "registry", registry);
            SetSerializedReference(spawner, "target", player.transform);

            var input = player.AddComponent<WeaponInputReader>();
            var weapon = player.AddComponent<WeaponController>();
            SetSerializedReference(weapon, "projectilePrefab", projectilePrefab.GetComponent<Projectile>());
            SetSerializedReference(weapon, "attackVfxPrefab", attackVfxPrefab.GetComponent<AttackVfx>());
            SetSerializedReference(weapon, "weaponInput", input);
            SetSerializedReference(weapon, "movement", player.GetComponent<PlayerMovement>());
            SetSerializedInt(weapon, "enemyMask", LayerMask.GetMask("Enemy"));

            var selectionGo = new GameObject("Weapon Selection");
            var selection = selectionGo.AddComponent<WeaponSelection>();
            SetSerializedArray(selection, "weapons", weapons ?? new WeaponDefinition[0]);
            SetSerializedReference(selection, "controller", weapon);

            var pickupsRoot = new GameObject("Weapon Pickups");
            SetSerializedReference(selection, "pickupsRoot", pickupsRoot);
            BuildWeaponPickups(pickupsRoot.transform, settings, weapons, selection);

            return (spawner, selection);
        }

        private static void BuildWeaponPickups(
            Transform parent,
            GameSettings settings,
            WeaponDefinition[] weapons,
            WeaponSelection selection)
        {
            if (weapons == null || weapons.Length == 0)
            {
                return;
            }

            if (LoadSprite(SwordSpritePath) == null || LoadSprite(SpearSpritePath) == null || LoadSprite(BowSpritePath) == null)
            {
                PlaceholderArtGenerator.GenerateAll();
            }

            IReadOnlyList<Vector2> authored = settings != null ? settings.WeaponPedestalPositions : null;
            int count = authored != null ? authored.Count : 0;
            if (count < weapons.Length)
            {
                Debug.LogWarning("[GameSceneBuilder] GameSettings has fewer weapon positions than weapons; using fallback positions.");
                authored = new[]
                {
                    new Vector2(-4f, -1.2f),
                    new Vector2(0f, -1.8f),
                    new Vector2(4f, -1.2f),
                };
                count = authored.Count;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponDefinition weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                Vector2 position = authored[i % count];
                var pickupGo = new GameObject($"Weapon_Pickup_{weapon.DisplayName}");
                pickupGo.transform.SetParent(parent, false);
                pickupGo.transform.position = new Vector3(position.x, position.y, 0f);
                pickupGo.layer = LayerMask.NameToLayer("Pickup");

                var spriteRenderer = pickupGo.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = weapon.PickupSprite;
                spriteRenderer.sortingLayerName = settings != null ? settings.EntitySortingLayer : "Entities";
                pickupGo.AddComponent<IsometricSpriteSorter>();

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(pickupGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);

                var label = labelGo.AddComponent<TextMesh>();
                label.font = font;
                label.fontSize = 32;
                label.characterSize = 0.05f;
                label.anchor = TextAnchor.LowerCenter;
                label.alignment = TextAlignment.Center;
                label.text = weapon.DisplayName.ToUpperInvariant();
                label.color = new Color32(236, 226, 176, 255);

                var labelRenderer = labelGo.GetComponent<MeshRenderer>();
                labelRenderer.sortingLayerName = settings != null ? settings.EntitySortingLayer : "Entities";
                var labelSorter = labelGo.AddComponent<IsometricSpriteSorter>();
                SetSerializedInt(labelSorter, "orderOffset", 1);

                var trigger = pickupGo.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(0.8f, 0.8f);
                trigger.offset = new Vector2(0f, 0.4f);

                var pickup = pickupGo.AddComponent<WeaponPickup>();
                SetSerializedReference(pickup, "weapon", weapon);
                SetSerializedReference(pickup, "spriteRenderer", spriteRenderer);
                SetSerializedReference(pickup, "label", label);
                SetSerializedReference(pickup, "trigger", trigger);
                SetSerializedReference(pickup, "selection", selection);
            }
        }

        private static (Experience, LevelDirector) BuildProgression(
            GameObject player,
            EnemySpawner spawner,
            MonsterScaling scaling,
            ProgressionDefinition progression,
            StageProgression stageProgression,
            PlayerDefinition playerDefinition)
        {
            var experience = player.AddComponent<Experience>();
            SetSerializedReference(experience, "progression", progression);

            var stats = player.AddComponent<PlayerStats>();
            SetSerializedReference(stats, "definition", playerDefinition);
            SetSerializedReference(stats, "health", player.GetComponent<Health>());
            SetSerializedReference(stats, "weapon", player.GetComponent<WeaponController>());
            SetSerializedReference(stats, "movement", player.GetComponent<PlayerMovement>());

            // The stage owns the monster level and feeds it to the spawner; the director listens to
            // room-clear events and owns door/advance state. Remaining references are wired once the
            // doors, camera and level-up UI exist.
            var directorGo = new GameObject("Level Director");
            var director = directorGo.AddComponent<LevelDirector>();
            SetSerializedReference(director, "spawner", spawner);
            SetSerializedReference(director, "scaling", scaling);
            SetSerializedReference(director, "progression", stageProgression);

            var granterGo = new GameObject("Kill XP Granter");
            var granter = granterGo.AddComponent<KillXpGranter>();
            SetSerializedReference(granter, "spawner", spawner);
            SetSerializedReference(granter, "experience", experience);

            return (experience, director);
        }

        private static StageDoor[] BuildDoors(
            Transform parent,
            GameSettings settings,
            (UpgradeCategoryDefinition shots, UpgradeCategoryDefinition mobility, UpgradeCategoryDefinition vitality) categories,
            LevelDirector director)
        {
            Sprite closedSprite = LoadSprite(DoorClosedSpritePath);
            Sprite openSprite = LoadSprite(DoorOpenSpritePath);
            if (closedSprite == null || openSprite == null)
            {
                PlaceholderArtGenerator.GenerateDoors();
                closedSprite = LoadSprite(DoorClosedSpritePath);
                openSprite = LoadSprite(DoorOpenSpritePath);
            }

            var offers = new[]
            {
                categories.shots,
                categories.mobility,
                categories.vitality,
            };

            IReadOnlyList<Vector2> positions = settings != null ? settings.DoorPositions : null;
            int count = positions != null ? positions.Count : 0;
            if (count < offers.Length)
            {
                Debug.LogWarning("[GameSceneBuilder] GameSettings has fewer door positions than categories; using fallback positions.");
                positions = new[]
                {
                    new Vector2(-3.2f, 3.55f),
                    new Vector2(0f, 3.85f),
                    new Vector2(3.2f, 3.55f),
                };
                count = positions.Count;
            }

            var doors = new StageDoor[offers.Length];
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < offers.Length; i++)
            {
                Vector2 position = positions[i % count];
                var doorGo = new GameObject($"Exit_{i + 1}_{offers[i].DisplayName}");
                doorGo.transform.SetParent(parent, false);
                doorGo.transform.position = new Vector3(position.x, position.y, 0f);

                var spriteRenderer = doorGo.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = closedSprite;
                spriteRenderer.sortingLayerName = settings != null ? settings.EntitySortingLayer : "Entities";
                doorGo.AddComponent<IsometricSpriteSorter>();

                var trigger = doorGo.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(0.9f, 0.8f);
                trigger.offset = new Vector2(0f, 0.4f);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(doorGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);

                var label = labelGo.AddComponent<TextMesh>();
                label.font = font;
                label.fontSize = 32;
                label.characterSize = 0.06f;
                label.anchor = TextAnchor.LowerCenter;
                label.alignment = TextAlignment.Center;
                label.text = offers[i].DisplayName.ToUpperInvariant();
                label.color = offers[i].Color;

                var labelRenderer = labelGo.GetComponent<MeshRenderer>();
                labelRenderer.sortingLayerName = settings != null ? settings.EntitySortingLayer : "Entities";
                var labelSorter = labelGo.AddComponent<IsometricSpriteSorter>();
                SetSerializedInt(labelSorter, "orderOffset", 1);

                var door = doorGo.AddComponent<StageDoor>();
                SetSerializedReference(door, "category", offers[i]);
                SetSerializedReference(door, "spriteRenderer", spriteRenderer);
                SetSerializedReference(door, "label", label);
                SetSerializedReference(door, "lockedSprite", closedSprite);
                SetSerializedReference(door, "openSprite", openSprite);
                SetSerializedReference(door, "levelDirector", director);
                door.SetUnlocked(false);

                doors[i] = door;
            }

            return doors;
        }

        private static LevelUpController BuildHud(
            GameObject player,
            EnemySpawner spawner,
            Experience experience,
            LevelDirector levelDirector,
            UpgradePool upgradePool,
            WeaponSelection selection)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Player health bar, anchored top-left.
            Image barBackground = CreateImage("HealthBar", canvasGo.transform, new Color32(18, 16, 26, 220));
            RectTransform barRect = barBackground.rectTransform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(24f, -24f);
            barRect.sizeDelta = new Vector2(340f, 30f);

            Image healthFill = CreateImage("Fill", barBackground.transform, new Color32(206, 72, 84, 255));
            RectTransform fillRect = healthFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthFill.fillAmount = 1f;

            // Kill counter, anchored top-right.
            Text killLabel = CreateText("KillCounter", canvasGo.transform, font, "Kills: 0", TextAnchor.UpperRight);
            RectTransform killRect = killLabel.rectTransform;
            killRect.anchorMin = new Vector2(1f, 1f);
            killRect.anchorMax = new Vector2(1f, 1f);
            killRect.pivot = new Vector2(1f, 1f);
            killRect.anchoredPosition = new Vector2(-24f, -20f);
            killRect.sizeDelta = new Vector2(360f, 48f);
            killLabel.fontSize = 36;
            killLabel.color = new Color32(236, 236, 244, 255);

            // Equipped weapon, under the health bar, so Samsara's choice is always visible.
            Text weaponLabel = CreateText("WeaponLabel", canvasGo.transform, font, "Weapon: none", TextAnchor.UpperLeft);
            RectTransform weaponRect = weaponLabel.rectTransform;
            weaponRect.anchorMin = new Vector2(0f, 1f);
            weaponRect.anchorMax = new Vector2(0f, 1f);
            weaponRect.pivot = new Vector2(0f, 1f);
            weaponRect.anchoredPosition = new Vector2(24f, -62f);
            weaponRect.sizeDelta = new Vector2(420f, 36f);
            weaponLabel.fontSize = 28;
            weaponLabel.color = new Color32(238, 224, 168, 255);

            // Stage indicator, centered at the top so it reads separately from kills and health.
            Text stageLabel = CreateText("StageLabel", canvasGo.transform, font, "Samsara", TextAnchor.UpperCenter);
            RectTransform stageRect = stageLabel.rectTransform;
            stageRect.anchorMin = new Vector2(0.5f, 1f);
            stageRect.anchorMax = new Vector2(0.5f, 1f);
            stageRect.pivot = new Vector2(0.5f, 1f);
            stageRect.anchoredPosition = new Vector2(0f, -20f);
            stageRect.sizeDelta = new Vector2(400f, 48f);
            stageLabel.fontSize = 36;
            stageLabel.color = new Color32(232, 216, 96, 255);

            // Room objective: live enemies, then the open-exit prompt once the room is clear.
            Text objectiveLabel = CreateText("ObjectiveLabel", canvasGo.transform, font, "Enemies: 0", TextAnchor.UpperCenter);
            RectTransform objectiveRect = objectiveLabel.rectTransform;
            objectiveRect.anchorMin = new Vector2(0.5f, 1f);
            objectiveRect.anchorMax = new Vector2(0.5f, 1f);
            objectiveRect.pivot = new Vector2(0.5f, 1f);
            objectiveRect.anchoredPosition = new Vector2(0f, -66f);
            objectiveRect.sizeDelta = new Vector2(760f, 36f);
            objectiveLabel.fontSize = 26;
            objectiveLabel.color = new Color32(206, 218, 238, 255);

            // XP bar along the bottom, with the current level above it.
            Image xpBackground = CreateImage("XpBar", canvasGo.transform, new Color32(18, 16, 26, 220));
            RectTransform xpRect = xpBackground.rectTransform;
            xpRect.anchorMin = new Vector2(0.5f, 0f);
            xpRect.anchorMax = new Vector2(0.5f, 0f);
            xpRect.pivot = new Vector2(0.5f, 0f);
            xpRect.anchoredPosition = new Vector2(0f, 28f);
            xpRect.sizeDelta = new Vector2(820f, 22f);

            Image xpFill = CreateImage("Fill", xpBackground.transform, new Color32(64, 214, 158, 255));
            RectTransform xpFillRect = xpFill.rectTransform;
            xpFillRect.anchorMin = Vector2.zero;
            xpFillRect.anchorMax = Vector2.one;
            xpFillRect.offsetMin = new Vector2(3f, 3f);
            xpFillRect.offsetMax = new Vector2(-3f, -3f);
            xpFill.type = Image.Type.Filled;
            xpFill.fillMethod = Image.FillMethod.Horizontal;
            xpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            xpFill.fillAmount = 0f;

            Text levelLabel = CreateText("LevelLabel", canvasGo.transform, font, "Lv 1", TextAnchor.MiddleCenter);
            RectTransform levelRect = levelLabel.rectTransform;
            levelRect.anchorMin = new Vector2(0.5f, 0f);
            levelRect.anchorMax = new Vector2(0.5f, 0f);
            levelRect.pivot = new Vector2(0.5f, 0f);
            levelRect.anchoredPosition = new Vector2(0f, 56f);
            levelRect.sizeDelta = new Vector2(300f, 32f);
            levelLabel.fontSize = 26;
            levelLabel.color = new Color32(198, 255, 232, 255);

            // Game-over overlay (hidden until the player dies).
            Image panel = CreateImage("GameOverPanel", canvasGo.transform, new Color32(10, 8, 16, 224));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Text title = CreateText("Title", panel.transform, font, "GAME OVER", TextAnchor.MiddleCenter);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 120f);
            titleRect.sizeDelta = new Vector2(800f, 120f);
            title.fontSize = 84;
            title.color = new Color32(240, 84, 96, 255);

            Button restartButton = CreateButton("RestartButton", panel.transform, font, "RESTART", new Vector2(0f, -20f));
            Button menuButton = CreateButton("MenuButton", panel.transform, font, "MAIN MENU", new Vector2(0f, -140f));

            panel.gameObject.SetActive(false);

            // Level-up choice overlay (hidden until a level is gained).
            Image levelPanel = CreateImage("LevelUpPanel", canvasGo.transform, new Color32(10, 8, 16, 232));
            RectTransform levelPanelRect = levelPanel.rectTransform;
            levelPanelRect.anchorMin = Vector2.zero;
            levelPanelRect.anchorMax = Vector2.one;
            levelPanelRect.offsetMin = Vector2.zero;
            levelPanelRect.offsetMax = Vector2.zero;

            Text levelTitle = CreateText("Title", levelPanel.transform, font, "LEVEL UP", TextAnchor.MiddleCenter);
            RectTransform levelTitleRect = levelTitle.rectTransform;
            levelTitleRect.anchorMin = new Vector2(0.5f, 0.5f);
            levelTitleRect.anchorMax = new Vector2(0.5f, 0.5f);
            levelTitleRect.pivot = new Vector2(0.5f, 0.5f);
            levelTitleRect.anchoredPosition = new Vector2(0f, 250f);
            levelTitleRect.sizeDelta = new Vector2(900f, 100f);
            levelTitle.fontSize = 72;
            levelTitle.color = new Color32(198, 255, 232, 255);

            Text levelHint = CreateText("Hint", levelPanel.transform, font, "Choose an upgrade", TextAnchor.MiddleCenter);
            RectTransform levelHintRect = levelHint.rectTransform;
            levelHintRect.anchorMin = new Vector2(0.5f, 0.5f);
            levelHintRect.anchorMax = new Vector2(0.5f, 0.5f);
            levelHintRect.pivot = new Vector2(0.5f, 0.5f);
            levelHintRect.anchoredPosition = new Vector2(0f, 178f);
            levelHintRect.sizeDelta = new Vector2(900f, 48f);
            levelHint.fontSize = 30;
            levelHint.color = new Color32(206, 206, 218, 255);

            var cards = new UpgradeCardView[UpgradeCardCount];
            for (int i = 0; i < cards.Length; i++)
            {
                float x = (i - (cards.Length - 1) * 0.5f) * 380f;
                cards[i] = CreateUpgradeCard(levelPanel.transform, font, new Vector2(x, -20f));
            }

            levelPanel.gameObject.SetActive(false);

            var hud = canvasGo.AddComponent<GameHud>();
            SetSerializedReference(hud, "healthFill", healthFill);
            SetSerializedReference(hud, "killLabel", killLabel);
            SetSerializedReference(hud, "playerHealth", player.GetComponent<Health>());
            SetSerializedReference(hud, "spawner", spawner);
            SetSerializedReference(hud, "experience", experience);
            SetSerializedReference(hud, "xpFill", xpFill);
            SetSerializedReference(hud, "levelLabel", levelLabel);
            SetSerializedReference(hud, "levelDirector", levelDirector);
            SetSerializedReference(hud, "stageLabel", stageLabel);
            SetSerializedReference(hud, "objectiveLabel", objectiveLabel);
            SetSerializedReference(hud, "weaponLabel", weaponLabel);
            SetSerializedReference(hud, "weaponSelection", selection);

            var gameOver = canvasGo.AddComponent<GameOverController>();
            SetSerializedReference(gameOver, "panel", panel.gameObject);
            SetSerializedReference(gameOver, "restartButton", restartButton);
            SetSerializedReference(gameOver, "menuButton", menuButton);
            SetSerializedReference(gameOver, "playerHealth", player.GetComponent<Health>());

            var levelUp = canvasGo.AddComponent<LevelUpController>();
            SetSerializedReference(levelUp, "experience", experience);
            SetSerializedReference(levelUp, "stats", player.GetComponent<PlayerStats>());
            SetSerializedReference(levelUp, "playerHealth", player.GetComponent<Health>());
            SetSerializedReference(levelUp, "panel", levelPanel.gameObject);
            SetSerializedReference(levelUp, "pool", upgradePool);
            SetSerializedReference(levelUp, "weaponSelection", selection);
            SetSerializedReference(levelUp, "titleLabel", levelTitle);
            SetSerializedReference(levelUp, "hintLabel", levelHint);
            SetSerializedArray(levelUp, "cards", cards);
            return levelUp;
        }

        private static UpgradeCardView CreateUpgradeCard(Transform parent, Font font, Vector2 anchoredPosition)
        {
            Image background = CreateImage("UpgradeCard", parent, new Color32(34, 30, 50, 240));
            RectTransform rect = background.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(340f, 420f);

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            Image icon = CreateImage("Icon", background.transform, Color.white);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -28f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
            icon.enabled = false;

            Text title = CreateText("Title", background.transform, font, "", TextAnchor.MiddleCenter);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -140f);
            titleRect.sizeDelta = new Vector2(-24f, 60f);
            title.fontSize = 30;
            title.color = Color.white;

            Text description = CreateText("Description", background.transform, font, "", TextAnchor.UpperCenter);
            RectTransform descriptionRect = description.rectTransform;
            descriptionRect.anchorMin = new Vector2(0f, 0f);
            descriptionRect.anchorMax = new Vector2(1f, 1f);
            descriptionRect.offsetMin = new Vector2(18f, 24f);
            descriptionRect.offsetMax = new Vector2(-18f, -210f);
            description.fontSize = 24;
            description.color = new Color32(206, 206, 218, 255);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Overflow;

            var view = background.gameObject.AddComponent<UpgradeCardView>();
            SetSerializedReference(view, "button", button);
            SetSerializedReference(view, "icon", icon);
            SetSerializedReference(view, "titleLabel", title);
            SetSerializedReference(view, "descriptionLabel", description);
            return view;
        }

        private static Image CreateImage(string name, Transform parent, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 anchoredPosition)
        {
            Image image = CreateImage(name, parent, new Color32(70, 116, 206, 255));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(340f, 88f);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Label", image.transform, font, label, TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.fontSize = 40;
            text.color = Color.white;

            return button;
        }

        private static CameraFollow BuildCamera(Transform target, GameSettings settings)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            // Match the Pixel Perfect Camera's reference resolution so world space and art pixels align.
            camera.orthographicSize = settings.ReferenceResolution.y / (2f * settings.PixelsPerUnit);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 22, 32, 255);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            var pixelPerfect = cameraGo.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = settings.PixelsPerUnit;
            pixelPerfect.refResolutionX = settings.ReferenceResolution.x;
            pixelPerfect.refResolutionY = settings.ReferenceResolution.y;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;

            var follow = cameraGo.AddComponent<CameraFollow>();
            follow.SetTarget(target);
            return follow;
        }

        private static void BuildLight()
        {
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;
        }

        private static void WireDirector(
            LevelDirector director,
            GameSettings settings,
            GameObject player,
            CameraFollow cameraFollow,
            LevelUpController levelUp,
            StageDoor[] doors,
            WeaponSelection selection)
        {
            SetSerializedReference(director, "settings", settings);
            SetSerializedReference(director, "playerMovement", player.GetComponent<PlayerMovement>());
            SetSerializedReference(director, "cameraFollow", cameraFollow);
            SetSerializedReference(director, "levelUpController", levelUp);
            SetSerializedReference(director, "weaponSelection", selection);
            SetSerializedArray(director, "doors", doors);
        }

        private static void SetSerializedInt(Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedBool(Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AppendMissingReferences(Object target, string propertyName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            var existing = new List<Object>();
            for (int i = 0; i < property.arraySize; i++)
            {
                Object value = property.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value != null)
                {
                    existing.Add(value);
                }
            }

            bool changed = false;
            for (int i = 0; i < values.Length; i++)
            {
                Object value = values[i];
                if (value == null || existing.Contains(value))
                {
                    continue;
                }

                int index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                property.GetArrayElementAtIndex(index).objectReferenceValue = value;
                existing.Add(value);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        private static void SetSerializedReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Assigns a serialized reference only when it is currently empty. Used for wiring the builder
        /// needs but a designer may override: a missing value is filled, an authored one is kept.
        /// </summary>
        private static void SeedReferenceIfMissing(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            if (property.objectReferenceValue != null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Seeds the VFX fields on both new and pre-existing actions. Fields still at their initializer
        /// defaults count as unauthored, so a rebuild backfills them without clobbering tuning.
        /// </summary>
        private static void SeedHitVfxFields(
            WeaponActionDefinition action,
            Sprite sprite,
            Color color,
            float size,
            float duration,
            float sweep)
        {
            var serialized = new SerializedObject(action);
            SerializedProperty spriteProp = serialized.FindProperty("hitVfxSprite");
            SerializedProperty colorProp = serialized.FindProperty("hitVfxColor");
            SerializedProperty sizeProp = serialized.FindProperty("hitVfxSize");
            SerializedProperty durationProp = serialized.FindProperty("hitVfxDuration");
            SerializedProperty sweepProp = serialized.FindProperty("hitVfxSweep");
            if (spriteProp == null || colorProp == null || sizeProp == null || durationProp == null || sweepProp == null)
            {
                Debug.LogError("[GameSceneBuilder] Missing hit-VFX property on a weapon action.");
                return;
            }

            bool changed = false;
            if (sprite != null && spriteProp.objectReferenceValue == null)
            {
                spriteProp.objectReferenceValue = sprite;
                changed = true;
            }

            if (colorProp.colorValue == Color.white && color != Color.white)
            {
                colorProp.colorValue = color;
                changed = true;
            }

            if (Mathf.Approximately(sizeProp.floatValue, 1.4f) && !Mathf.Approximately(size, 1.4f))
            {
                sizeProp.floatValue = size;
                changed = true;
            }

            if (Mathf.Approximately(durationProp.floatValue, 0.22f) && !Mathf.Approximately(duration, 0.22f))
            {
                durationProp.floatValue = duration;
                changed = true;
            }

            if (Mathf.Approximately(sweepProp.floatValue, 0f) && !Mathf.Approximately(sweep, 0f))
            {
                sweepProp.floatValue = sweep;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssets();
        }

        private static void SeedArrayIfEmpty(Object target, string propertyName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            if (property.arraySize > 0)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        private static void SetSerializedFloat(Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedArray(Object target, string propertyName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
