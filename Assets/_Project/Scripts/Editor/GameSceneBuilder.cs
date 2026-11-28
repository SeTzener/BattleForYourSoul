using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
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
using BattleForYourSoul.Superpowers;
using BattleForYourSoul.World;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Builds the isometric test scene from scratch: settings asset, prefabs, definitions, isometric
    /// grid, ground tilemap, obstacles, collision walls, visible exit doors, pixel-perfect camera,
    /// 2D light, the combat loop (enemy registry, fixed-placement spawner, player superpower), the
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
        private const string SuperpowerDefinitionPath = "Assets/_Project/ScriptableObjects/Superpower_Default.asset";
        private const string SuperpowersFolder = "Assets/_Project/ScriptableObjects/Superpowers";
        private const string SuperpowerActionsFolder = "Assets/_Project/ScriptableObjects/Superpowers/Actions";
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
        private const string SwordSpritePath = "Assets/_Project/Art/Items/Superpower_Sword.png";
        private const string SpearSpritePath = "Assets/_Project/Art/Items/Superpower_Spear.png";
        private const string BowSpritePath = "Assets/_Project/Art/Items/Superpower_Bow.png";
        private const string FistsSpritePath = "Assets/_Project/Art/Items/Superpower_Fists.png";
        private const string PunchFramePath = "Assets/_Project/Art/Animation/Anim_Punch_0.png";
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
            // the superpower/action seeders below, and LoadSprite would otherwise cache a null reference.
            EnsurePlaceholderSprites();

            GameSettings settings = EnsureSettings();
            SuperpowerActions actions = EnsureSuperpowerActions();
            SuperpowerDefinition[] superpowers = EnsureSuperpowers(actions);
            int superstrengthIndex = superpowers.Length > 0 ? superpowers.Length - 1 : 0;
            EnsureLegacySuperpowerDefinition(actions);
            EnemyDefinition enemyDefinition = EnsureEnemyDefinition();
            PlayerDefinition playerDefinition = EnsurePlayerDefinition(superpowers.Length > 0 ? superpowers[0] : null);
            ProgressionDefinition progression = EnsureProgressionDefinition();
            MonsterScaling scaling = EnsureMonsterScaling();

            var categories = EnsureUpgradeCategories();
            UpgradeDefinition[] upgrades = EnsureUpgrades(
                categories.upgrades, categories.mobility, categories.vitality, categories.superpowers, superpowers, actions, superstrengthIndex);
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

            (EnemySpawner spawner, SuperpowerSelection selection) = BuildCombat(player, enemyDefinition, enemyPrefab, projectilePrefab, attackVfxPrefab, superpowers, settings);
            (Experience experience, LevelDirector levelDirector) = BuildProgression(player, spawner, scaling, progression, stageProgression, playerDefinition, selection);

            StageDoor[] doors = BuildDoors(environment.transform, settings, categories, levelDirector);
            CameraFollow cameraFollow = BuildCamera(player.transform, settings);
            BuildLight();
            LevelUpController levelUp = BuildHud(player, spawner, experience, levelDirector, upgradePool, selection);

            WireDirector(levelDirector, settings, player, cameraFollow, levelUp, experience, doors, selection,
                player.GetComponent<RunUpgradeState>(), player.GetComponent<PlayerStats>());

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

            // There are three category doors. Pedestals still need four slots, one per superpower,
            // including Superstrength. Keep any authored positions and only trim an obsolete 4th door.
            var serialized = new SerializedObject(settings);
            var defaultDoors = new[]
            {
                new Vector2(-3.2f, 3.55f),
                new Vector2(0f, 3.85f),
                new Vector2(3.2f, 3.55f),
            };
            SerializedProperty doors = serialized.FindProperty("doorPositions");
            bool changedDoors = false;
            if (doors != null)
            {
                if (doors.arraySize == 0)
                {
                    doors.arraySize = defaultDoors.Length;
                    for (int i = 0; i < defaultDoors.Length; i++)
                    {
                        doors.GetArrayElementAtIndex(i).vector2Value = defaultDoors[i];
                    }

                    changedDoors = true;
                }
                else if (doors.arraySize < defaultDoors.Length)
                {
                    int first = doors.arraySize;
                    doors.arraySize = defaultDoors.Length;
                    for (int i = first; i < defaultDoors.Length; i++)
                    {
                        doors.GetArrayElementAtIndex(i).vector2Value = defaultDoors[i];
                    }

                    changedDoors = true;
                }
                else if (doors.arraySize > defaultDoors.Length)
                {
                    // An older four-door setup kept a Superpowers exit; that door is gone.
                    doors.arraySize = defaultDoors.Length;
                    changedDoors = true;
                }
            }

            var defaultPedestals = new[]
            {
                new Vector2(-5.2f, -1f),
                new Vector2(-1.8f, -1.8f),
                new Vector2(1.8f, -1.8f),
                new Vector2(5.2f, -1f),
            };
            SerializedProperty pedestals = serialized.FindProperty("superpowerPedestalPositions");
            bool changedPedestals = false;
            if (pedestals != null)
            {
                if (pedestals.arraySize == 0)
                {
                    pedestals.arraySize = defaultPedestals.Length;
                    for (int i = 0; i < defaultPedestals.Length; i++)
                    {
                        pedestals.GetArrayElementAtIndex(i).vector2Value = defaultPedestals[i];
                    }

                    changedPedestals = true;
                }
                else if (pedestals.arraySize < defaultPedestals.Length)
                {
                    int first = pedestals.arraySize;
                    pedestals.arraySize = defaultPedestals.Length;
                    for (int i = first; i < defaultPedestals.Length; i++)
                    {
                        pedestals.GetArrayElementAtIndex(i).vector2Value = defaultPedestals[i];
                    }

                    changedPedestals = true;
                }
            }

            if (changedDoors || changedPedestals)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        private static (UpgradeCategoryDefinition upgrades, UpgradeCategoryDefinition mobility, UpgradeCategoryDefinition vitality, UpgradeCategoryDefinition superpowers) EnsureUpgradeCategories()
        {
            EnsureFolder(UpgradeCategoriesFolder);

            // The former Shots door is now the generic stage-upgrade door. The other category assets
            // stay wired as placeholders until more stage rewards are authored.
            return (
                EnsureCategory("UpgradeCategory_Upgrades", "Upgrades", "Unique stage upgrades for your superpower.", new Color32(244, 152, 88, 255)),
                EnsureCategory("UpgradeCategory_Mobility", "Mobility", "Move faster and outmaneuver the room.", new Color32(112, 220, 156, 255)),
                EnsureCategory("UpgradeCategory_Vitality", "Vitality", "Survive longer under pressure.", new Color32(236, 120, 160, 255)),
                EnsureCategory("UpgradeCategory_Superpowers", "Superpowers", "Unique and powerful upgrades for your superpower.", new Color32(248, 214, 96, 255)));
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

            // The sprite MUST live on a child. Attack poses (and Hulk/Man of Steel growth) write to
            // this renderer's transform; putting it on the root used to drag the physics body back to
            // a stale position on every attack.
            var visual = new GameObject("Visual");
            visual.layer = player.layer;
            visual.transform.SetParent(player.transform, false);
            var spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = definition != null ? definition.Sprite : LoadSprite(PlayerSpritePath);
            spriteRenderer.sortingLayerName = settings.EntitySortingLayer;
            var sorter = visual.AddComponent<IsometricSpriteSorter>();
            SetSerializedReference(sorter, "targetRenderer", spriteRenderer);

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

            // Attack steps own position directly for their short duration; the movement component
            // simply suspends input while this is active.
            player.AddComponent<AttackMovement>();

            var dash = player.AddComponent<PlayerDash>();
            SetSerializedReference(dash, "definition", definition);
            SetSerializedFloat(dash, "dashSpeed", definition != null ? definition.DashSpeed : 14f);
            SetSerializedFloat(dash, "dashDuration", definition != null ? definition.DashDuration : 0.18f);
            SetSerializedFloat(dash, "dashCooldown", definition != null ? definition.DashCooldown : 1.2f);
            SetSerializedFloat(dash, "invulnerabilityGrace", definition != null ? definition.DashGrace : 0.08f);

            var visuals = player.AddComponent<PlayerVisuals>();
            SetSerializedReference(visuals, "definition", definition);
            SetSerializedReference(visuals, "spriteRenderer", spriteRenderer);

            var posePlayer = player.AddComponent<SuperpowerPosePlayer>();
            SetSerializedReference(posePlayer, "spriteRenderer", spriteRenderer);

            // Run-scoped upgrade record. It lives on the player so every gameplay system can reach it
            // through serialized references without any static state.
            player.AddComponent<RunUpgradeState>();

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

        private struct SuperpowerActions
        {
            public SuperpowerActionDefinition SwordSlash;
            public SuperpowerActionDefinition SwordCleave;
            public SuperpowerActionDefinition SpearThrust;
            public SuperpowerActionDefinition SpearSweep;
            public SuperpowerActionDefinition BowShot;
            public SuperpowerActionDefinition BowSpread;
            public SuperpowerActionDefinition Punch;
            public SuperpowerActionDefinition Uppercut;
            public SuperpowerActionDefinition NorthernStar;
            public SuperpowerActionDefinition BodySlam;
        }

        private struct AnimationSeed
        {
            public string[] FramePaths;
            public float FrameDuration;
            public Vector2 HopOffset;
            public float LungeDistance;
            public float LungeDuration;
        }

        private struct ActionSeed
        {
            public string FileName;
            public string DisplayName;
            public SuperpowerAttackShape Shape;
            public float Cooldown;
            public float Damage;
            public float Range;
            public float ArcDegrees;
            public float LineWidth;
            public int HitCount;
            public float HitDelay;
            public float HitInterval;
            public bool SweepHitsWhileLunging;
            public HitEffects OnHit;
            public AnimationSeed Animation;
            public int ProjectilesPerShot;
            public float SpreadDegrees;
            public float ProjectileSpeed;
            public float ProjectileLifetime;
            public string ProjectileSpritePath;
            public string HitVfxSpritePath;
            public Color HitVfxColor;
            public float HitVfxSize;
            public float HitVfxDuration;
            public float HitVfxSweep;
        }

        private static string[] Anim(string name, int count)
        {
            var paths = new string[count];
            for (int i = 0; i < count; i++)
            {
                paths[i] = $"Assets/_Project/Art/Animation/Anim_{name}_{i}.png";
            }

            return paths;
        }

        /// <summary>Every frame generated by PlaceholderArtGenerator and referenced by a seed.</summary>
        private static string[] RequiredAnimationPaths()
        {
            var paths = new List<string>();
            paths.AddRange(Anim("Punch", 3));
            paths.AddRange(Anim("Uppercut", 4));
            paths.AddRange(Anim("NorthernStar", 6));
            paths.AddRange(Anim("BodySlam", 5));
            paths.AddRange(Anim("Charge", 3));
            paths.AddRange(Anim("Sword_Slash", 2));
            paths.AddRange(Anim("Sword_Cleave", 3));
            paths.AddRange(Anim("Spear_Thrust", 2));
            paths.AddRange(Anim("Spear_Sweep", 3));
            paths.AddRange(Anim("Bow_Shot", 2));
            paths.AddRange(Anim("Bow_Spread", 3));
            return paths.ToArray();
        }

        private static SuperpowerActions EnsureSuperpowerActions()
        {
            EnsureFolder(SuperpowerActionsFolder);

            var foeRoot = new HitEffects
            {
                RootDuration = 0.3f,
                KnockbackForce = 3f,
                KnockbackDuration = 0.18f,
                ContactDamageDisabledDuration = 0.35f,
            };

            return new SuperpowerActions
            {
                SwordSlash = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Sword_Slash", DisplayName = "Slash", Shape = SuperpowerAttackShape.MeleeArc,
                    Cooldown = 0.42f, Damage = 2f, Range = 1.2f, ArcDegrees = 110f, LineWidth = 0.4f,
                    HitCount = 1, HitVfxSpritePath = SlashVfxSpritePath, HitVfxColor = new Color32(250, 250, 255, 255),
                    HitVfxSize = 1.6f, HitVfxDuration = 0.18f, HitVfxSweep = 90f,
                    Animation = new AnimationSeed { FramePaths = Anim("Sword_Slash", 2), FrameDuration = 0.2f, LungeDuration = 0.2f },
                }),
                SwordCleave = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Sword_Cleave", DisplayName = "Cleave", Shape = SuperpowerAttackShape.MeleeArc,
                    Cooldown = 1.4f, Damage = 3f, Range = 1.5f, ArcDegrees = 200f, LineWidth = 0.4f,
                    HitCount = 1, HitVfxSpritePath = SlashVfxSpritePath, HitVfxColor = new Color32(255, 238, 210, 255),
                    HitVfxSize = 2.2f, HitVfxDuration = 0.24f, HitVfxSweep = 160f,
                    Animation = new AnimationSeed { FramePaths = Anim("Sword_Cleave", 3), FrameDuration = 0.45f, LungeDuration = 0.4f },
                }),
                SpearThrust = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Spear_Thrust", DisplayName = "Thrust", Shape = SuperpowerAttackShape.Line,
                    Cooldown = 0.5f, Damage = 2f, Range = 2f, ArcDegrees = 360f, LineWidth = 0.35f,
                    HitCount = 1, HitVfxSpritePath = ThrustVfxSpritePath, HitVfxColor = new Color32(238, 248, 255, 255),
                    HitVfxSize = 2.2f, HitVfxDuration = 0.16f,
                    Animation = new AnimationSeed { FramePaths = Anim("Spear_Thrust", 2), FrameDuration = 0.24f, LungeDistance = 0.3f, LungeDuration = 0.24f },
                }),
                SpearSweep = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Spear_Sweep", DisplayName = "Sweep", Shape = SuperpowerAttackShape.Line,
                    Cooldown = 1.5f, Damage = 3f, Range = 2.4f, ArcDegrees = 360f, LineWidth = 0.9f,
                    HitCount = 1, HitVfxSpritePath = ThrustVfxSpritePath, HitVfxColor = new Color32(255, 240, 210, 255),
                    HitVfxSize = 2.8f, HitVfxDuration = 0.22f,
                    Animation = new AnimationSeed { FramePaths = Anim("Spear_Sweep", 3), FrameDuration = 0.5f, LungeDuration = 0.4f },
                }),
                BowShot = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Bow_Shot", DisplayName = "Shot", Shape = SuperpowerAttackShape.Projectile,
                    Cooldown = 0.34f, Damage = 1f, Range = 8f, ArcDegrees = 360f, LineWidth = 0.4f,
                    HitCount = 1, ProjectilesPerShot = 1, ProjectileSpeed = 15f, ProjectileLifetime = 1.1f,
                    ProjectileSpritePath = ArrowSpritePath,
                    Animation = new AnimationSeed { FramePaths = Anim("Bow_Shot", 2), FrameDuration = 0.16f },
                }),
                BowSpread = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Bow_Spread", DisplayName = "Spread", Shape = SuperpowerAttackShape.Projectile,
                    Cooldown = 1.5f, Damage = 1f, Range = 8f, ArcDegrees = 360f, LineWidth = 0.4f,
                    HitCount = 1, ProjectilesPerShot = 2, SpreadDegrees = 22f, ProjectileSpeed = 12f, ProjectileLifetime = 1f,
                    ProjectileSpritePath = ArrowSpritePath,
                    Animation = new AnimationSeed { FramePaths = Anim("Bow_Spread", 3), FrameDuration = 0.5f },
                }),

                // Superstrength. Cooldowns match the placeholder animation length: the 3-hit combo
                // takes roughly twice as long as a single punch, so its cooldown is doubled there.
                Punch = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Superstrength_Punch", DisplayName = "Punch", Shape = SuperpowerAttackShape.MeleeArc,
                    Cooldown = 0.28f, Damage = 2f, Range = 1.05f, ArcDegrees = 100f, LineWidth = 0.4f,
                    HitCount = 1, OnHit = foeRoot,
                    HitVfxSpritePath = ThrustVfxSpritePath, HitVfxColor = new Color32(196, 244, 176, 255),
                    HitVfxSize = 1.2f, HitVfxDuration = 0.14f,
                    Animation = new AnimationSeed
                    {
                        FramePaths = Anim("Punch", 3), FrameDuration = 0.09f,
                        HopOffset = new Vector2(0.08f, 0.04f), LungeDistance = 0.5f, LungeDuration = 0.18f,
                    },
                }),
                Uppercut = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Superstrength_Uppercut", DisplayName = "Uppercut", Shape = SuperpowerAttackShape.MeleeArc,
                    Cooldown = 1.6f, Damage = 5f, Range = 1.3f, ArcDegrees = 110f, LineWidth = 0.4f,
                    HitCount = 1, SweepHitsWhileLunging = true,
                    OnHit = new HitEffects
                    {
                        RootDuration = 0.3f, KnockbackForce = 6f, KnockbackDuration = 0.3f,
                        ContactDamageDisabledDuration = 0.45f,
                    },
                    HitVfxSpritePath = SlashVfxSpritePath, HitVfxColor = new Color32(255, 226, 128, 255),
                    HitVfxSize = 1.7f, HitVfxDuration = 0.22f, HitVfxSweep = 120f,
                    Animation = new AnimationSeed
                    {
                        FramePaths = Anim("Uppercut", 4), FrameDuration = 0.38f,
                        HopOffset = new Vector2(0f, 0.45f), LungeDistance = 1.5f, LungeDuration = 0.55f,
                    },
                }),
                NorthernStar = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Superstrength_NorthernStar", DisplayName = "Northern Star Strike", Shape = SuperpowerAttackShape.MeleeArc,
                    Cooldown = 0.56f, Damage = 2f, Range = 1.1f, ArcDegrees = 110f, LineWidth = 0.4f,
                    HitCount = 3, HitDelay = 0.04f, HitInterval = 0.17f, OnHit = foeRoot,
                    HitVfxSpritePath = ThrustVfxSpritePath, HitVfxColor = new Color32(188, 226, 255, 255),
                    HitVfxSize = 1.25f, HitVfxDuration = 0.13f,
                    Animation = new AnimationSeed
                    {
                        FramePaths = Anim("NorthernStar", 6), FrameDuration = 0.09f,
                        HopOffset = new Vector2(0.12f, 0.08f), LungeDistance = 0.5f, LungeDuration = 0.52f,
                    },
                }),
                BodySlam = EnsureSuperpowerAction(new ActionSeed
                {
                    FileName = "Action_Superstrength_BodySlam", DisplayName = "Body Slam", Shape = SuperpowerAttackShape.MeleeArc,
                    Cooldown = 1.8f, Damage = 7f, Range = 1.55f, ArcDegrees = 150f, LineWidth = 0.4f,
                    HitCount = 1, SweepHitsWhileLunging = true,
                    OnHit = new HitEffects
                    {
                        RootDuration = 0.3f, KnockbackForce = 8f, KnockbackDuration = 0.35f,
                        ContactDamageDisabledDuration = 0.5f,
                    },
                    HitVfxSpritePath = SlashVfxSpritePath, HitVfxColor = new Color32(236, 168, 120, 255),
                    HitVfxSize = 2.4f, HitVfxDuration = 0.28f, HitVfxSweep = 40f,
                    Animation = new AnimationSeed
                    {
                        FramePaths = Anim("BodySlam", 5), FrameDuration = 0.34f,
                        HopOffset = new Vector2(0f, 0.7f), LungeDistance = 1.5f, LungeDuration = 0.6f,
                    },
                }),
            };
        }

        private static Sprite[] LoadFrames(string[] paths)
        {
            if (paths == null)
            {
                return new Sprite[0];
            }

            var frames = new Sprite[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                frames[i] = LoadSprite(paths[i]);
            }

            return frames;
        }

        private static SuperpowerActionDefinition EnsureSuperpowerAction(ActionSeed seed)
        {
            string path = $"{SuperpowerActionsFolder}/{seed.FileName}.asset";
            var (action, created) = EnsureAsset<SuperpowerActionDefinition>(path);

            Sprite projectileSprite = !string.IsNullOrEmpty(seed.ProjectileSpritePath)
                ? LoadSprite(seed.ProjectileSpritePath)
                : null;
            Sprite hitVfxSprite = !string.IsNullOrEmpty(seed.HitVfxSpritePath)
                ? LoadSprite(seed.HitVfxSpritePath)
                : null;
            Sprite[] frames = LoadFrames(seed.Animation.FramePaths);

            // Placeholder art may have been generated after a previous build seeded this action, so
            // backfill missing references without overwriting anything a designer has authored.
            if (projectileSprite != null)
            {
                SeedReferenceIfMissing(action, "projectileSprite", projectileSprite);
            }

            SeedHitVfxFields(action, hitVfxSprite, seed.HitVfxColor, seed.HitVfxSize, seed.HitVfxDuration, seed.HitVfxSweep);
            SeedAnimationIfEmpty(action, seed.Animation, frames);
            SeedHitEffectsIfEmpty(action, seed.OnHit);

            if (!created)
            {
                return action;
            }

            var serialized = new SerializedObject(action);
            serialized.FindProperty("displayName").stringValue = seed.DisplayName;
            serialized.FindProperty("shape").enumValueIndex = (int)seed.Shape;
            serialized.FindProperty("cooldown").floatValue = seed.Cooldown;
            serialized.FindProperty("damage").floatValue = seed.Damage;
            serialized.FindProperty("range").floatValue = seed.Range;
            serialized.FindProperty("arcDegrees").floatValue = seed.ArcDegrees;
            serialized.FindProperty("lineWidth").floatValue = seed.LineWidth;
            serialized.FindProperty("hitCount").intValue = seed.HitCount;
            serialized.FindProperty("hitDelay").floatValue = seed.HitDelay;
            serialized.FindProperty("hitInterval").floatValue = seed.HitInterval;
            serialized.FindProperty("sweepHitsWhileLunging").boolValue = seed.SweepHitsWhileLunging;
            serialized.FindProperty("projectilesPerShot").intValue = seed.ProjectilesPerShot;
            serialized.FindProperty("spreadDegrees").floatValue = seed.SpreadDegrees;
            serialized.FindProperty("projectileSpeed").floatValue = seed.ProjectileSpeed;
            serialized.FindProperty("projectileLifetime").floatValue = seed.ProjectileLifetime;
            serialized.FindProperty("projectileSprite").objectReferenceValue = projectileSprite;
            WriteAnimation(serialized.FindProperty("animation"), seed.Animation, frames);
            WriteHitEffects(serialized.FindProperty("onHit"), seed.OnHit);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssets();
            return action;
        }

        private static void WriteAnimation(SerializedProperty property, AnimationSeed seed, Sprite[] frames)
        {
            SerializedProperty frameArray = property.FindPropertyRelative("Frames");
            frameArray.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            property.FindPropertyRelative("FrameDuration").floatValue = seed.FrameDuration;
            property.FindPropertyRelative("HopOffset").vector2Value = seed.HopOffset;
            property.FindPropertyRelative("LungeDistance").floatValue = seed.LungeDistance;
            property.FindPropertyRelative("LungeDuration").floatValue = seed.LungeDuration;
        }

        private static void WriteHitEffects(SerializedProperty property, HitEffects effects)
        {
            property.FindPropertyRelative("RootDuration").floatValue = effects.RootDuration;
            property.FindPropertyRelative("RootAffectsSpecialFoes").boolValue = effects.RootAffectsSpecialFoes;
            property.FindPropertyRelative("StunDuration").floatValue = effects.StunDuration;
            property.FindPropertyRelative("KnockbackForce").floatValue = effects.KnockbackForce;
            property.FindPropertyRelative("KnockbackDuration").floatValue = effects.KnockbackDuration;
            property.FindPropertyRelative("KnockbackAffectsSpecialFoes").boolValue = effects.KnockbackAffectsSpecialFoes;
            property.FindPropertyRelative("GuardBreak").boolValue = effects.GuardBreak;
            property.FindPropertyRelative("ContactDamageDisabledDuration").floatValue = effects.ContactDamageDisabledDuration;
        }

        private static void SeedAnimationIfEmpty(SuperpowerActionDefinition action, AnimationSeed seed, Sprite[] frames)
        {
            if (frames.Length == 0)
            {
                return;
            }

            var serialized = new SerializedObject(action);
            SerializedProperty property = serialized.FindProperty("animation");
            SerializedProperty frameArray = property.FindPropertyRelative("Frames");

            if (frameArray.arraySize == 0)
            {
                WriteAnimation(property, seed, frames);
            }
            else
            {
                // Repair blank slots in an otherwise authored animation without replacing its length.
                bool changed = false;
                for (int i = 0; i < frameArray.arraySize && i < frames.Length; i++)
                {
                    SerializedProperty element = frameArray.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue == null && frames[i] != null)
                    {
                        element.objectReferenceValue = frames[i];
                        changed = true;
                    }
                }

                if (!changed)
                {
                    return;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssets();
        }

        private static void SeedHitEffectsIfEmpty(SuperpowerActionDefinition action, HitEffects effects)
        {
            var serialized = new SerializedObject(action);
            SerializedProperty property = serialized.FindProperty("onHit");
            bool hasSeed = effects.RootDuration > 0f || effects.StunDuration > 0f ||
                           effects.KnockbackForce > 0f || effects.GuardBreak ||
                           effects.ContactDamageDisabledDuration > 0f;
            bool hasValue = property.FindPropertyRelative("RootDuration").floatValue > 0f ||
                            property.FindPropertyRelative("StunDuration").floatValue > 0f ||
                            property.FindPropertyRelative("KnockbackForce").floatValue > 0f ||
                            property.FindPropertyRelative("GuardBreak").boolValue ||
                            property.FindPropertyRelative("ContactDamageDisabledDuration").floatValue > 0f;
            if (!hasSeed || hasValue)
            {
                return;
            }

            WriteHitEffects(property, effects);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            AssetDatabase.SaveAssets();
        }

        private static SuperpowerDefinition[] EnsureSuperpowers(SuperpowerActions actions)
        {
            EnsureFolder(SuperpowersFolder);

            return new[]
            {
                EnsureSuperpower("Superpower_Sword", "sword", "Sword", "Close-range slash and a wide cleave.",
                    SwordSpritePath, actions.SwordSlash, actions.SwordCleave),
                EnsureSuperpower("Superpower_Spear", "spear", "Spear", "A two-block thrust and a broad sweep.",
                    SpearSpritePath, actions.SpearThrust, actions.SpearSweep),
                EnsureSuperpower("Superpower_Bow", "bow", "Bow", "Long-range shot and a spread volley.",
                    BowSpritePath, actions.BowShot, actions.BowSpread),
                EnsureSuperpower("Superpower_Superstrength", "superstrength", "Superstrength",
                    "Fast fists, a rooting punch and an Uppercut. Higher inherent defense.",
                    FistsSpritePath, actions.Punch, actions.Uppercut, 12f),
            };
        }

        private static SuperpowerDefinition EnsureSuperpower(
            string fileName,
            string id,
            string displayName,
            string description,
            string spritePath,
            SuperpowerActionDefinition primary,
            SuperpowerActionDefinition special,
            float defense = 0f)
        {
            string path = $"{SuperpowersFolder}/{fileName}.asset";
            var (superpower, created) = EnsureAsset<SuperpowerDefinition>(path);
            if (created)
            {
                var serialized = new SerializedObject(superpower);
                serialized.FindProperty("id").stringValue = id;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("description").stringValue = description;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(superpower);
                AssetDatabase.SaveAssets();
            }

            // Newly required references are backfilled so older assets keep their authored values.
            SeedReferenceIfMissing(superpower, "pickupSprite", LoadSprite(spritePath));
            SeedReferenceIfMissing(superpower, "primaryAction", primary);
            SeedReferenceIfMissing(superpower, "specialAction", special);
            if (defense > 0f)
            {
                var serialized = new SerializedObject(superpower);
                SerializedProperty defenseProp = serialized.FindProperty("defense");
                if (defenseProp != null && Mathf.Approximately(defenseProp.floatValue, 0f))
                {
                    defenseProp.floatValue = defense;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(superpower);
                    AssetDatabase.SaveAssets();
                }
            }

            return superpower;
        }

        private static void EnsureLegacySuperpowerDefinition(SuperpowerActions actions)
        {
            var (definition, _) = EnsureAsset<SuperpowerDefinition>(SuperpowerDefinitionPath);
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

        private static PlayerDefinition EnsurePlayerDefinition(SuperpowerDefinition defaultSuperpower)
        {
            var (definition, created) = EnsureAsset<PlayerDefinition>(PlayerDefinitionPath);
            if (created)
            {
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            SeedReferenceIfMissing(definition, "sprite", LoadSprite(PlayerSpritePath));
            SeedReferenceIfMissing(definition, "startingSuperpower", defaultSuperpower);

            // Charge uses the same frames as a forward punch, played on the sprite child only.
            var dashSerialized = new SerializedObject(definition);
            SerializedProperty dashFrames = dashSerialized.FindProperty("dashAnimation").FindPropertyRelative("Frames");
            if (dashFrames != null && dashFrames.arraySize == 0)
            {
                WriteAnimation(dashSerialized.FindProperty("dashAnimation"),
                    new AnimationSeed
                    {
                        FramePaths = Anim("Charge", 3), FrameDuration = 0.06f,
                        HopOffset = new Vector2(0.1f, 0.02f),
                    },
                    LoadFrames(Anim("Charge", 3)));
                dashSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            // Newer dash fields are missing from assets created before M9; backfill only values
            // that must be positive. A designer is free to author zero cooldown/grace.
            var serialized = new SerializedObject(definition);
            bool changed = SeedPositiveFloat(serialized.FindProperty("dashSpeed"), 14f);
            changed |= SeedPositiveFloat(serialized.FindProperty("dashDuration"), 0.18f);
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            return definition;
        }

        private static bool SeedPositiveFloat(SerializedProperty property, float value)
        {
            if (property == null || property.floatValue > 0f)
            {
                return false;
            }

            property.floatValue = value;
            return true;
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
            UpgradeCategoryDefinition upgradesCategory,
            UpgradeCategoryDefinition mobility,
            UpgradeCategoryDefinition vitality,
            UpgradeCategoryDefinition superpowersCategory,
            SuperpowerDefinition[] superpowers,
            SuperpowerActions actions,
            int superstrengthIndex)
        {
            EnsureFolder(UpgradesFolder);
            Sprite icon = LoadSprite(UpgradeIconSpritePath);
            SuperpowerDefinition[] bow = superpowerFilter(superpowers, 2);
            SuperpowerDefinition[] strength = superpowerFilter(superpowers, superstrengthIndex);

            var upgrades = new[]
            {
                // Projectile cards are Bow-only: other superpowers have nothing to shoot.
                EnsureUpgrade("Upgrade_SharpenedBolts", "sharpened-bolts", "Sharpened Bolts", "Increase projectile damage by 1.", new Color32(232, 96, 96, 255), icon, upgradesCategory, UpgradeStat.DamageFlat, 1f, bow),
                EnsureUpgrade("Upgrade_RapidFire", "rapid-fire", "Rapid Fire", "Fire 12% faster.", new Color32(240, 168, 72, 255), icon, upgradesCategory, UpgradeStat.FireRatePercent, 12f, bow),
                EnsureUpgrade("Upgrade_SplitShot", "split-shot", "Split Shot", "Fire one extra projectile.", new Color32(178, 130, 240, 255), icon, upgradesCategory, UpgradeStat.ExtraProjectiles, 1f, bow),
                EnsureUpgrade("Upgrade_Velocity", "velocity", "Velocity", "Projectiles travel 15% faster.", new Color32(120, 210, 240, 255), icon, upgradesCategory, UpgradeStat.ProjectileSpeedPercent, 15f, bow),

                EnsureUpgrade("Upgrade_FleetFooted", "fleet-footed", "Fleet Footed", "Move 8% faster.", new Color32(120, 214, 120, 255), icon, mobility, UpgradeStat.MoveSpeedPercent, 8f),
                EnsureUpgrade("Upgrade_SwiftStride", "swift-stride", "Swift Stride", "Move 10% faster.", new Color32(128, 232, 180, 255), icon, mobility, UpgradeStat.MoveSpeedPercent, 10f),
                EnsureUpgrade("Upgrade_SureFooting", "sure-footing", "Sure Footing", "Move 6% faster.", new Color32(156, 226, 142, 255), icon, mobility, UpgradeStat.MoveSpeedPercent, 6f),
                EnsureUpgrade("Upgrade_InstantDash", "instant-dash", "Phase Step", "Gain a dash charge that is always ready.", new Color32(132, 236, 224, 255), icon, mobility, UpgradeStat.InstantDashCharges, 1f),

                EnsureUpgrade("Upgrade_Vitality", "vitality", "Vitality", "Gain 20 max health.", new Color32(240, 120, 168, 255), icon, vitality, UpgradeStat.MaxHealthFlat, 20f),
                EnsureUpgrade("Upgrade_IronWill", "iron-will", "Iron Will", "Gain 14 max health.", new Color32(250, 150, 180, 255), icon, vitality, UpgradeStat.MaxHealthFlat, 14f),
                EnsureUpgrade("Upgrade_ToughSkin", "tough-skin", "Tough Skin", "Gain 12 max health.", new Color32(232, 160, 190, 255), icon, vitality, UpgradeStat.MaxHealthFlat, 12f),

                // Superpower-specific placeholders: each superpower has three so a filtered choice stays full.
                EnsureUpgrade("Upgrade_Bow_Split", "bow-split", "Split Arrow", "Fire one additional arrow.", new Color32(244, 176, 96, 255), icon, upgradesCategory, UpgradeStat.ExtraProjectiles, 1f, bow),
                EnsureUpgrade("Upgrade_Bow_Ricochet", "bow-ricochet", "Ricochet", "Arrows bounce to one more target.", new Color32(252, 200, 120, 255), icon, upgradesCategory, UpgradeStat.RicochetBounces, 1f, bow),
                EnsureUpgrade("Upgrade_Bow_Impact", "bow-impact", "Explosive Tip", "Hits burst in a small area.", new Color32(248, 148, 104, 255), icon, upgradesCategory, UpgradeStat.ImpactRadius, 0.35f, bow),

                EnsureUpgrade("Upgrade_Sword_Reach", "sword-reach", "Longer Slash", "Sword attacks reach half a block further.", new Color32(212, 172, 236, 255), icon, vitality, UpgradeStat.RangeFlat, 0.5f, superpowerFilter(superpowers,0)),
                EnsureUpgrade("Upgrade_Sword_Impact", "sword-impact", "Impact Wave", "Sword hits leave a small area of impact.", new Color32(228, 144, 220, 255), icon, vitality, UpgradeStat.ImpactRadius, 0.3f, superpowerFilter(superpowers,0)),
                EnsureUpgrade("Upgrade_Sword_Edge", "sword-edge", "Keen Edge", "Sword attacks deal one more damage.", new Color32(238, 196, 248, 255), icon, vitality, UpgradeStat.DamageFlat, 1f, superpowerFilter(superpowers,0)),

                EnsureUpgrade("Upgrade_Spear_Reach", "spear-reach", "Long Reach", "Spear attacks reach half a block further.", new Color32(132, 236, 176, 255), icon, mobility, UpgradeStat.RangeFlat, 0.5f, superpowerFilter(superpowers,1)),
                EnsureUpgrade("Upgrade_Spear_Broadhead", "spear-broadhead", "Broadhead", "Spear attacks deal 20% more damage.", new Color32(160, 244, 196, 255), icon, mobility, UpgradeStat.DamagePercent, 20f, superpowerFilter(superpowers,1)),
                EnsureUpgrade("Upgrade_Spear_Quick", "spear-quick", "Quick Thrust", "Spear attacks recover 15% faster.", new Color32(184, 248, 212, 255), icon, mobility, UpgradeStat.FireRatePercent, 15f, superpowerFilter(superpowers,1)),

                // Superstrength level-up cards: repeatable, filtered to this superpower.
                EnsureUpgrade("Upgrade_Strength", "strength", "Strength", "Punches deal one more damage.", new Color32(240, 132, 96, 255), icon, vitality, UpgradeStat.Strength, 1f, strength),
                EnsureUpgrade("Upgrade_HeavyFists", "heavy-fists", "Heavy Fists", "Punches deal 15% more damage.", new Color32(248, 156, 104, 255), icon, vitality, UpgradeStat.DamagePercent, 15f, strength),
                EnsureUpgrade("Upgrade_SwiftPunch", "swift-punch", "Swift Punch", "Recover 15% faster between attacks.", new Color32(252, 196, 120, 255), icon, mobility, UpgradeStat.FireRatePercent, 15f, strength),
                EnsureUpgrade("Upgrade_CriticalChance", "critical-chance", "Critical Chance", "Gain a 10% chance to crit (max 100%).", new Color32(252, 224, 132, 255), icon, vitality, UpgradeStat.CriticalChancePercent, 10f, strength),
                EnsureUpgrade("Upgrade_CriticalDamage", "critical-damage", "Critical Damage", "Critical hits deal 20% more damage (max +50%).", new Color32(252, 238, 164, 255), icon, vitality, UpgradeStat.CriticalDamagePercent, 20f, strength),
                EnsureUpgrade("Upgrade_StunChance", "stun-chance", "Stun Chance", "Hits have a 20% chance to stun (max 50% for Superstrength).", new Color32(212, 236, 140, 255), icon, vitality, UpgradeStat.BonusStunChancePercent, 20f, strength),
                EnsureUpgrade("Upgrade_Toughness", "toughness", "Toughness", "Gain 5 defense points.", new Color32(168, 206, 244, 255), icon, vitality, UpgradeStat.DefenseFlat, 5f, strength),
                EnsureUpgrade("Upgrade_SuperstrengthVigor", "superstrength-vigor", "Vigor", "Gain 20 max health.", new Color32(244, 184, 196, 255), icon, vitality, UpgradeStat.MaxHealthFlat, 20f, strength),
                EnsureUpgrade("Upgrade_FirmGrip", "firm-grip", "Firm Grip", "Punches knock foes back further.", new Color32(196, 220, 252, 255), icon, mobility, UpgradeStat.ChargeKnockback, 2f, strength),

                // Tiered Superstrength level-up cards. Unlike the stage-only uniques below, these
                // are repeatable and never appear as stage-clear rewards.
                EnsureUpgrade("Upgrade_StrengthUp_Low", "strength-up-low", "Strength-up Low", "Gain 1 Strength.", new Color32(240, 132, 96, 255), icon, superpowersCategory, UpgradeStat.Strength, 1f, strength),
                EnsureUpgrade("Upgrade_StrengthUp_Mid", "strength-up-mid", "Strength-up Mid", "Gain 3 Strength.", new Color32(248, 156, 104, 255), icon, superpowersCategory, UpgradeStat.Strength, 3f, strength),
                EnsureUpgrade("Upgrade_StrengthUp_High", "strength-up-high", "Strength-up High", "Gain 6 Strength.", new Color32(252, 196, 120, 255), icon, superpowersCategory, UpgradeStat.Strength, 6f, strength),

                EnsureUpgrade("Upgrade_CriticalHit_Low", "critical-hit-low", "Critical Hit Low", "Gain 5% critical chance.", new Color32(252, 224, 132, 255), icon, superpowersCategory, UpgradeStat.CriticalChancePercent, 5f, strength),
                EnsureUpgrade("Upgrade_CriticalHit_Mid", "critical-hit-mid", "Critical Hit Mid", "Gain 10% critical chance.", new Color32(252, 232, 148, 255), icon, superpowersCategory, UpgradeStat.CriticalChancePercent, 10f, strength),
                EnsureUpgrade("Upgrade_CriticalHit_High", "critical-hit-high", "Critical Hit High", "Gain 15% critical chance.", new Color32(252, 240, 164, 255), icon, superpowersCategory, UpgradeStat.CriticalChancePercent, 15f, strength),

                EnsureUpgrade("Upgrade_CriticalDamage_Low", "critical-damage-low", "Critical Damage Low", "Critical hits deal 10% more damage.", new Color32(252, 238, 164, 255), icon, superpowersCategory, UpgradeStat.CriticalDamagePercent, 10f, strength),
                EnsureUpgrade("Upgrade_CriticalDamage_Mid", "critical-damage-mid", "Critical Damage Mid", "Critical hits deal 20% more damage.", new Color32(252, 242, 180, 255), icon, superpowersCategory, UpgradeStat.CriticalDamagePercent, 20f, strength),
                EnsureUpgrade("Upgrade_CriticalDamage_High", "critical-damage-high", "Critical Damage High", "Critical hits deal 30% more damage.", new Color32(252, 246, 196, 255), icon, superpowersCategory, UpgradeStat.CriticalDamagePercent, 30f, strength),

                // Stage-clear upgrades. Each is offered once per run and only after choosing
                // Superstrength; form upgrades share a group so Hulk and Man of Steel never both appear.
                EnsureRichUpgrade(new UpgradeSeed
                {
                    FileName = "Upgrade_Charge", Id = "charge", DisplayName = "Charge",
                    Description = "Dashing through a foe damages and knocks it back.",
                    Color = new Color32(168, 236, 224, 255), Icon = icon, Source = UpgradeSource.StageClear, Category = upgradesCategory,
                    SuperpowerFilter = strength, MaxStacks = 1,
                    Modifiers = new[]
                    {
                        new StatModifier(UpgradeStat.ChargeDamage, 2f),
                        new StatModifier(UpgradeStat.ChargeKnockback, 3f),
                    },
                    Behavior = SuperpowerUpgradeBehavior.Charge,
                }),
                EnsureRichUpgrade(new UpgradeSeed
                {
                    FileName = "Upgrade_SuperCharge", Id = "super-charge", DisplayName = "Super Charge",
                    Description = "Hold Dash up to 2s to charge up to double damage; release to fire. A tap matches Charge.",
                    Color = new Color32(128, 244, 236, 255), Icon = icon, Source = UpgradeSource.StageClear, Category = upgradesCategory,
                    SuperpowerFilter = strength, MaxStacks = 1,
                    Modifiers = new[] { new StatModifier(UpgradeStat.SuperCharge, 2f) },
                    Behavior = SuperpowerUpgradeBehavior.SuperCharge,
                    Requires = new[] { UpgradeAsset("Upgrade_Charge") },
                }),
                EnsureRichUpgrade(new UpgradeSeed
                {
                    FileName = "Upgrade_PowerUp", Id = "power-up", DisplayName = "Power-up",
                    Description = "All damage increased by 20%.",
                    Color = new Color32(248, 132, 112, 255), Icon = icon, Source = UpgradeSource.StageClear, Category = upgradesCategory,
                    SuperpowerFilter = strength, MaxStacks = 1,
                    Modifiers = new[] { new StatModifier(UpgradeStat.DamagePercent, 20f) },
                    Behavior = SuperpowerUpgradeBehavior.PowerUp,
                }),
                EnsureRichUpgrade(new UpgradeSeed
                {
                    FileName = "Upgrade_Hulk", Id = "hulk", DisplayName = "Hulk",
                    Description = "Lower health grows stronger and greener. Exclusive with Man of Steel.",
                    Color = new Color32(112, 220, 120, 255), Icon = icon, Source = UpgradeSource.StageClear, Category = upgradesCategory,
                    SuperpowerFilter = strength, MaxStacks = 1, ExclusiveGroup = "superstrength-form",
                    Modifiers = new[]
                    {
                        new StatModifier(UpgradeStat.MaxHealthFlat, 20f),
                        new StatModifier(UpgradeStat.DefenseFlat, 20f),
                    },
                    Behavior = SuperpowerUpgradeBehavior.Hulk,
                }),
                EnsureRichUpgrade(new UpgradeSeed
                {
                    FileName = "Upgrade_ManOfSteel", Id = "man-of-steel", DisplayName = "Man of Steel",
                    Description = "Lower health grows denser and steelier. Exclusive with Hulk.",
                    Color = new Color32(184, 204, 228, 255), Icon = icon, Source = UpgradeSource.StageClear, Category = upgradesCategory,
                    SuperpowerFilter = strength, MaxStacks = 1, ExclusiveGroup = "superstrength-form",
                    Modifiers = new[] { new StatModifier(UpgradeStat.DefenseFlat, 30f) },
                    Behavior = SuperpowerUpgradeBehavior.ManOfSteel,
                }),
                EnsureRichUpgrade(new UpgradeSeed
                {
                    FileName = "Upgrade_NorthernStar", Id = "northern-star-strike", DisplayName = "Northern Star Strike",
                    Description = "Primary becomes a 3-hit combo. Each hit deals full damage; cooldown doubles with the animation.",
                    Color = new Color32(180, 224, 252, 255), Icon = icon, Source = UpgradeSource.StageClear, Category = upgradesCategory,
                    SuperpowerFilter = strength, MaxStacks = 1,
                    PrimaryOverride = actions.NorthernStar,
                }),
                EnsureRichUpgrade(new UpgradeSeed
                {
                    FileName = "Upgrade_BodySlam", Id = "body-slam", DisplayName = "Body Slam",
                    Description = "Uppercut becomes a leaping slam. The jump is sprite animation only.",
                    Color = new Color32(240, 184, 132, 255), Icon = icon, Source = UpgradeSource.StageClear, Category = upgradesCategory,
                    SuperpowerFilter = strength, MaxStacks = 1,
                    SpecialOverride = actions.BodySlam,
                }),
            };

            return upgrades;
        }

        private static SuperpowerDefinition[] superpowerFilter(SuperpowerDefinition[] superpowers, int index)
        {
            return superpowers != null && index < superpowers.Length ? new[] { superpowers[index] } : new SuperpowerDefinition[0];
        }

        private static UpgradeDefinition UpgradeAsset(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<UpgradeDefinition>($"{UpgradesFolder}/{fileName}.asset");
        }

        private struct UpgradeSeed
        {
            public string FileName;
            public string Id;
            public string DisplayName;
            public string Description;
            public Color32 Color;
            public Sprite Icon;
            public UpgradeSource Source;
            public UpgradeCategoryDefinition Category;
            public SuperpowerDefinition[] SuperpowerFilter;
            public StatModifier[] Modifiers;
            public SuperpowerUpgradeBehavior Behavior;
            public int MaxStacks;
            public string ExclusiveGroup;
            public UpgradeDefinition[] Requires;
            public SuperpowerActionDefinition PrimaryOverride;
            public SuperpowerActionDefinition SpecialOverride;
        }

        private static UpgradeDefinition EnsureRichUpgrade(UpgradeSeed seed)
        {
            string path = $"{UpgradesFolder}/{seed.FileName}.asset";
            var (upgrade, created) = EnsureAsset<UpgradeDefinition>(path);

            if (!created)
            {
                // Stale generated assets can predate uniqueness, prerequisites, overrides or effect
                // metadata. Backfill only empty/default fields; authored non-default values win.
                var backfill = new SerializedObject(upgrade);
                backfill.FindProperty("category").objectReferenceValue = seed.Category;
                SeedStringIfEmpty(backfill.FindProperty("id"), seed.Id);
                SeedStringIfEmpty(backfill.FindProperty("displayName"), seed.DisplayName);
                SeedStringIfEmpty(backfill.FindProperty("description"), seed.Description);
                SeedStringIfEmpty(backfill.FindProperty("exclusiveGroup"), seed.ExclusiveGroup);

                SerializedProperty maxStacks = backfill.FindProperty("maxStacks");
                if (maxStacks.intValue == 0 && seed.MaxStacks != 0)
                {
                    maxStacks.intValue = seed.MaxStacks;
                }

                SerializedProperty behavior = backfill.FindProperty("behavior");
                if (behavior.intValue == 0 && seed.Behavior != 0)
                {
                    behavior.intValue = (int)seed.Behavior;
                }

                SerializedProperty source = backfill.FindProperty("source");
                if (source.intValue == (int)UpgradeSource.LevelUp && seed.Source != UpgradeSource.LevelUp)
                {
                    source.intValue = (int)seed.Source;
                }

                SerializedProperty filter = backfill.FindProperty("superpowerFilter");
                if (filter.arraySize == 0 && seed.SuperpowerFilter != null && seed.SuperpowerFilter.Length > 0)
                {
                    SetObjectArray(filter, seed.SuperpowerFilter);
                }

                SerializedProperty requires = backfill.FindProperty("requires");
                if (requires.arraySize == 0 && seed.Requires != null && seed.Requires.Length > 0)
                {
                    SetObjectArray(requires, seed.Requires);
                }

                SeedReferenceIfMissing(backfill.FindProperty("primaryOverride"), seed.PrimaryOverride);
                SeedReferenceIfMissing(backfill.FindProperty("specialOverride"), seed.SpecialOverride);

                SerializedProperty backfillModifiers = backfill.FindProperty("modifiers");
                if (backfillModifiers.arraySize == 0 && seed.Modifiers != null && seed.Modifiers.Length > 0)
                {
                    backfillModifiers.arraySize = seed.Modifiers.Length;
                    for (int i = 0; i < backfillModifiers.arraySize; i++)
                    {
                        SerializedProperty element = backfillModifiers.GetArrayElementAtIndex(i);
                        element.FindPropertyRelative("stat").enumValueIndex = (int)seed.Modifiers[i].Stat;
                        element.FindPropertyRelative("value").floatValue = seed.Modifiers[i].Value;
                    }
                }

                backfill.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(upgrade);
                AssetDatabase.SaveAssets();
                return upgrade;
            }

            var serialized = new SerializedObject(upgrade);
            serialized.FindProperty("id").stringValue = seed.Id;
            serialized.FindProperty("displayName").stringValue = seed.DisplayName;
            serialized.FindProperty("description").stringValue = seed.Description;
            serialized.FindProperty("color").colorValue = seed.Color;
            serialized.FindProperty("icon").objectReferenceValue = seed.Icon;
            serialized.FindProperty("category").objectReferenceValue = seed.Category;
            serialized.FindProperty("maxStacks").intValue = seed.MaxStacks;
            serialized.FindProperty("exclusiveGroup").stringValue = seed.ExclusiveGroup ?? "";
            serialized.FindProperty("behavior").intValue = (int)seed.Behavior;
            serialized.FindProperty("source").intValue = (int)seed.Source;

            SerializedProperty modifiers = serialized.FindProperty("modifiers");
            modifiers.arraySize = seed.Modifiers != null ? seed.Modifiers.Length : 0;
            for (int i = 0; i < modifiers.arraySize; i++)
            {
                SerializedProperty element = modifiers.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("stat").enumValueIndex = (int)seed.Modifiers[i].Stat;
                element.FindPropertyRelative("value").floatValue = seed.Modifiers[i].Value;
            }

            SetObjectArray(serialized.FindProperty("superpowerFilter"), seed.SuperpowerFilter ?? new SuperpowerDefinition[0]);
            SetObjectArray(serialized.FindProperty("requires"), seed.Requires ?? new UpgradeDefinition[0]);
            serialized.FindProperty("primaryOverride").objectReferenceValue = seed.PrimaryOverride;
            serialized.FindProperty("specialOverride").objectReferenceValue = seed.SpecialOverride;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(upgrade);
            AssetDatabase.SaveAssets();
            return upgrade;
        }

        private static void SetObjectArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
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
            SuperpowerDefinition[] superpowerFilter)
        {
            string path = $"{UpgradesFolder}/{fileName}.asset";
            var (upgrade, created) = EnsureAsset<UpgradeDefinition>(path);

            // Category is required for filtered rewards, so backfill it on older assets too.
            SeedReferenceIfMissing(upgrade, "category", category);

            if (!created)
            {
                if (superpowerFilter != null && superpowerFilter.Length > 0)
                {
                    SeedArrayIfEmpty(upgrade, "superpowerFilter", superpowerFilter);
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

            // Use the SerializedObject already open: a separate SerializedObject here would be
            // overwritten when the pending modifiers above are applied.
            SetObjectArray(serialized.FindProperty("superpowerFilter"), superpowerFilter ?? new SuperpowerDefinition[0]);

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
                // Samsara enables every exit; there is no Superpowers door, only the three categories.
                EnsureStage("Stage_Samsara", "Samsara", 1, enemy, new Vector2[0], true, null),
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
            bool superpowerSelectionStage = false,
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
                serialized.FindProperty("superpowerSelectionStage").boolValue = superpowerSelectionStage;
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

            // Older Samsara assets only enabled the second exit; open all four so the Superpowers
            // category is actually reachable. Other stages keep their authored door selection.
            if (!created && superpowerSelectionStage)
            {
                SerializedProperty indices = serialized.FindProperty("activeDoorIndices");
                if (indices != null && indices.arraySize == 1 && indices.GetArrayElementAtIndex(0).intValue == 1)
                {
                    indices.arraySize = 0;
                    dirty = true;
                }
            }

            // Older stages used timed waves; backfill their fixed encounter when it is still empty.
            // Samsara deliberately stays empty, so it must never receive default enemies.
            SerializedProperty placements = serialized.FindProperty("enemyPlacements");
            if (!superpowerSelectionStage && placements != null && placements.arraySize == 0)
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
            // matters for nested authoring folders such as Superpowers/Actions.
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
            bool missing = LoadSprite(PlayerSpritePath) == null ||
                           LoadSprite(EnemySpritePath) == null ||
                           LoadSprite(ProjectileSpritePath) == null ||
                           LoadSprite(SwordSpritePath) == null ||
                           LoadSprite(SpearSpritePath) == null ||
                           LoadSprite(BowSpritePath) == null ||
                           LoadSprite(FistsSpritePath) == null ||
                           LoadSprite(ArrowSpritePath) == null ||
                           LoadSprite(SlashVfxSpritePath) == null ||
                           LoadSprite(ThrustVfxSpritePath) == null;

            // Check every authored pose, not just Punch: a single missing frame would otherwise leave
            // Uppercut / Northern Star / Body Slam / dash animations with a blank slot.
            if (!missing)
            {
                string[] frames = RequiredAnimationPaths();
                for (int i = 0; i < frames.Length; i++)
                {
                    if (LoadSprite(frames[i]) == null)
                    {
                        missing = true;
                        break;
                    }
                }
            }

            if (missing)
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

        private static (EnemySpawner, SuperpowerSelection) BuildCombat(
            GameObject player,
            EnemyDefinition enemyDefinition,
            GameObject enemyPrefab,
            GameObject projectilePrefab,
            GameObject attackVfxPrefab,
            SuperpowerDefinition[] superpowers,
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

            var dash = player.GetComponent<PlayerDash>();
            SetSerializedReference(dash, "bodyCollider", player.GetComponent<Collider2D>());
            SetSerializedReference(dash, "enemyRegistry", registry);
            SetSerializedReference(dash, "health", player.GetComponent<Health>());

            var input = player.AddComponent<SuperpowerInputReader>();
            var superpower = player.AddComponent<SuperpowerController>();
            SetSerializedReference(superpower, "projectilePrefab", projectilePrefab.GetComponent<Projectile>());
            SetSerializedReference(superpower, "attackVfxPrefab", attackVfxPrefab.GetComponent<AttackVfx>());
            SetSerializedReference(superpower, "superpowerInput", input);
            SetSerializedReference(superpower, "movement", player.GetComponent<PlayerMovement>());
            SetSerializedReference(superpower, "attackMove", player.GetComponent<AttackMovement>());
            SetSerializedInt(superpower, "enemyMask", LayerMask.GetMask("Enemy"));
            SetSerializedReference(superpower, "health", player.GetComponent<Health>());
            SetSerializedReference(superpower, "bodyCollider", player.GetComponent<Collider2D>());
            SetSerializedReference(superpower, "enemyRegistry", registry);

            var selectionGo = new GameObject("Superpower Selection");
            var selection = selectionGo.AddComponent<SuperpowerSelection>();
            SetSerializedArray(selection, "superpowers", superpowers ?? new SuperpowerDefinition[0]);
            SetSerializedReference(selection, "controller", superpower);

            var pickupsRoot = new GameObject("Superpower Pickups");
            SetSerializedReference(selection, "pickupsRoot", pickupsRoot);
            BuildSuperpowerPickups(pickupsRoot.transform, settings, superpowers, selection);

            return (spawner, selection);
        }

        private static void BuildSuperpowerPickups(
            Transform parent,
            GameSettings settings,
            SuperpowerDefinition[] superpowers,
            SuperpowerSelection selection)
        {
            if (superpowers == null || superpowers.Length == 0)
            {
                return;
            }

            if (LoadSprite(SwordSpritePath) == null || LoadSprite(SpearSpritePath) == null || LoadSprite(BowSpritePath) == null)
            {
                PlaceholderArtGenerator.GenerateAll();
            }

            IReadOnlyList<Vector2> authored = settings != null ? settings.SuperpowerPedestalPositions : null;
            int count = authored != null ? authored.Count : 0;
            if (count < superpowers.Length)
            {
                Debug.LogWarning("[GameSceneBuilder] GameSettings has fewer superpower positions than superpowers; using fallback positions.");
                authored = new[]
                {
                    new Vector2(-5.2f, -1f),
                    new Vector2(-1.8f, -1.8f),
                    new Vector2(1.8f, -1.8f),
                    new Vector2(5.2f, -1f),
                };
                count = authored.Count;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < superpowers.Length; i++)
            {
                SuperpowerDefinition superpower = superpowers[i];
                if (superpower == null)
                {
                    continue;
                }

                Vector2 position = authored[i % count];
                var pickupGo = new GameObject($"Superpower_Pickup_{superpower.DisplayName}");
                pickupGo.transform.SetParent(parent, false);
                pickupGo.transform.position = new Vector3(position.x, position.y, 0f);
                pickupGo.layer = LayerMask.NameToLayer("Pickup");

                var spriteRenderer = pickupGo.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = superpower.PickupSprite;
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
                label.text = superpower.DisplayName.ToUpperInvariant();
                label.color = new Color32(236, 226, 176, 255);

                var labelRenderer = labelGo.GetComponent<MeshRenderer>();
                labelRenderer.sortingLayerName = settings != null ? settings.EntitySortingLayer : "Entities";
                var labelSorter = labelGo.AddComponent<IsometricSpriteSorter>();
                SetSerializedInt(labelSorter, "orderOffset", 1);

                var trigger = pickupGo.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(0.8f, 0.8f);
                trigger.offset = new Vector2(0f, 0.4f);

                var pickup = pickupGo.AddComponent<SuperpowerPickup>();
                SetSerializedReference(pickup, "superpower", superpower);
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
            PlayerDefinition playerDefinition,
            SuperpowerSelection selection)
        {
            var experience = player.AddComponent<Experience>();
            SetSerializedReference(experience, "progression", progression);

            var stats = player.AddComponent<PlayerStats>();
            SetSerializedReference(stats, "definition", playerDefinition);
            SetSerializedReference(stats, "health", player.GetComponent<Health>());
            SetSerializedReference(stats, "superpower", player.GetComponent<SuperpowerController>());
            SetSerializedReference(stats, "movement", player.GetComponent<PlayerMovement>());
            SetSerializedReference(stats, "dash", player.GetComponent<PlayerDash>());
            SetSerializedReference(stats, "superpowerSelection", selection);
            SetSerializedReference(stats, "runUpgrades", player.GetComponent<RunUpgradeState>());

            var dash = player.GetComponent<PlayerDash>();
            SetSerializedReference(dash, "stats", stats);
            SetSerializedReference(dash, "superpowers", player.GetComponent<SuperpowerController>());
            SetSerializedReference(dash, "posePlayer", player.GetComponent<SuperpowerPosePlayer>());

            var superpowerController = player.GetComponent<SuperpowerController>();
            SetSerializedReference(superpowerController, "playerStats", stats);
            SetSerializedReference(superpowerController, "posePlayer", player.GetComponent<SuperpowerPosePlayer>());

            // PlayerVisuals lives on the prefab, so its runtime stats/health references can only be
            // wired on the scene instance once those components have been added.
            var visuals = player.GetComponent<PlayerVisuals>();
            // Visual child, never the root: the root only owns physics and colliders.
            SetSerializedReference(visuals, "spriteRenderer", player.GetComponentInChildren<SpriteRenderer>(true));
            SetSerializedReference(visuals, "movement", player.GetComponent<PlayerMovement>());
            SetSerializedReference(visuals, "stats", stats);
            SetSerializedReference(visuals, "health", player.GetComponent<Health>());

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
            (UpgradeCategoryDefinition upgrades, UpgradeCategoryDefinition mobility, UpgradeCategoryDefinition vitality, UpgradeCategoryDefinition superpowers) categories,
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

            // All doors can grant a stage upgrade; only the Upgrades door is authored today.
            var offers = new[]
            {
                categories.upgrades,
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

        /// <summary>
        /// Builds a virtual Gamepad from uGUI controls. FloatingJoystick and OnScreenButton components
        /// with Gamepad control paths are merged by the Input System into one device, so existing
        /// Player/Move, Dash and attack actions work without any touch-specific gameplay code.
        /// </summary>
        private static GameObject BuildMobileControls(Transform canvas, Font font, SuperpowerController superpower)
        {
            var root = new GameObject("MobileControls", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Sprite circle = EnsureCircleSprite();

            // Transparent raycast zone: touching anywhere in the left half spawns the pad there.
            Image moveZone = CreateImage("MoveZone", root.transform, new Color32(255, 255, 255, 0));
            RectTransform zoneRect = moveZone.rectTransform;
            zoneRect.anchorMin = Vector2.zero;
            zoneRect.anchorMax = new Vector2(0.5f, 1f);
            zoneRect.offsetMin = Vector2.zero;
            zoneRect.offsetMax = Vector2.zero;
            moveZone.raycastTarget = true;

            // Base and knob are hidden while idle so no fixed control sits near the border.
            // The larger bright circle behind a darker one reads as a ring, so the pad stays
            // legible over both light and dark floor tiles.
            Image pad = CreateImage("MovePad", root.transform, new Color32(120, 196, 255, 150), circle);
            // Centered anchor: runtime positions are computed in the root canvas' centered local space.
            PlaceAtAnchor(pad.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(168f, 168f));
            pad.raycastTarget = false;
            pad.gameObject.SetActive(false);

            Image padInner = CreateImage("MovePadInner", pad.transform, new Color32(28, 34, 52, 210), circle);
            PlaceAtAnchor(padInner.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            padInner.raycastTarget = false;

            Image knob = CreateImage("Knob", pad.transform, new Color32(240, 246, 255, 245), circle);
            PlaceAtAnchor(knob.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f));
            knob.raycastTarget = false;

            var joystick = moveZone.gameObject.AddComponent<FloatingJoystick>();
            SetSerializedReference(joystick, "touchZone", zoneRect);
            SetSerializedReference(joystick, "baseVisual", pad.rectTransform);
            SetSerializedReference(joystick, "knob", knob.rectTransform);
            SetSerializedFloat(joystick, "movementRange", 56f);
            SetSerializedString(joystick, "m_ControlPath", "<Gamepad>/leftStick");

            // Inset from the bottom-right corner leaves room for rounded corners and notches.
            CreateTouchButton(
                "DashButton", root.transform, font, circle, new Color32(214, 140, 62, 235),
                "DASH", new Vector2(-200f, 290f), new Vector2(120f, 120f), "<Gamepad>/buttonSouth");
            CreateTouchButton(
                "PrimaryButton", root.transform, font, circle, new Color32(66, 124, 206, 235),
                "ATK", new Vector2(-200f, 150f), new Vector2(140f, 140f), "<Gamepad>/buttonWest", superpower);
            CreateTouchButton(
                "SecondaryButton", root.transform, font, circle, new Color32(146, 92, 196, 235),
                "ALT", new Vector2(-370f, 150f), new Vector2(120f, 120f), "<Gamepad>/buttonNorth", superpower);

            root.SetActive(false);
            return root;
        }

        private static Image CreateTouchButton(
            string name,
            Transform parent,
            Font font,
            Sprite circle,
            Color32 color,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            string controlPath,
            SuperpowerController superpower = null)
        {
            Image image = CreateImage(name, parent, color, circle);
            PlaceAtAnchor(image.rectTransform, new Vector2(1f, 0f), anchoredPosition, size);

            var onScreenButton = image.gameObject.AddComponent<OnScreenButton>();
            SetSerializedString(onScreenButton, "m_ControlPath", controlPath);

            Text text = CreateText("Label", image.transform, font, label, TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.fontSize = 26;
            text.color = Color.white;

            if (superpower != null)
            {
                var cooldown = image.gameObject.AddComponent<AttackButtonCooldown>();
                SetSerializedReference(cooldown, "superpower", superpower);
                SetSerializedReference(cooldown, "button", onScreenButton);
                SetSerializedReference(cooldown, "image", image);
                SetSerializedReference(cooldown, "label", text);
            }

            return image;
        }

        private static void PlaceAtAnchor(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static LevelUpController BuildHud(
            GameObject player,
            EnemySpawner spawner,
            Experience experience,
            LevelDirector levelDirector,
            UpgradePool upgradePool,
            SuperpowerSelection selection)
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

            // Every edge-anchored HUD element lives under this rect so the notch / Dynamic Island
            // on iOS cannot cover the health bar or the kill counter in landscape.
            var safeAreaGo = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            RectTransform safeAreaRect = safeAreaGo.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
            safeAreaGo.AddComponent<SafeAreaFitter>();

            // Authored inactive: MobileControlsVisibility activates it on touch-first platforms.
            GameObject mobileControls = BuildMobileControls(
                safeAreaGo.transform,
                font,
                player.GetComponent<SuperpowerController>());

            // Player health bar, anchored top-left.
            Image barBackground = CreateImage("HealthBar", safeAreaGo.transform, new Color32(18, 16, 26, 220));
            RectTransform barRect = barBackground.rectTransform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(24f, -24f);
            barRect.sizeDelta = new Vector2(340f, 30f);

            // Pale ghost sits behind the red fill: it keeps marking the lost health for a moment
            // after damage, then shrinks to the real value. Created first so it draws underneath.
            // Bars use a sprite-less solid-colour Image, so the fill is driven by the rect's right
            // anchor rather than Image.fillAmount (which Filled mode ignores without a sprite).
            Image healthGhost = CreateImage("Ghost", barBackground.transform, new Color32(252, 222, 180, 210));
            RectTransform ghostRect = healthGhost.rectTransform;
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.one;
            ghostRect.offsetMin = Vector2.zero;
            ghostRect.offsetMax = Vector2.zero;
            healthGhost.raycastTarget = false;

            Image healthFill = CreateImage("Fill", barBackground.transform, new Color32(206, 72, 84, 255));
            RectTransform fillRect = healthFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            healthFill.raycastTarget = false;

            // Kill counter, anchored top-right.
            Text killLabel = CreateText("KillCounter", safeAreaGo.transform, font, "Kills: 0", TextAnchor.UpperRight);
            RectTransform killRect = killLabel.rectTransform;
            killRect.anchorMin = new Vector2(1f, 1f);
            killRect.anchorMax = new Vector2(1f, 1f);
            killRect.pivot = new Vector2(1f, 1f);
            killRect.anchoredPosition = new Vector2(-24f, -20f);
            killRect.sizeDelta = new Vector2(360f, 48f);
            killLabel.fontSize = 36;
            killLabel.color = new Color32(236, 236, 244, 255);

            // Equipped superpower, under the health bar, so Samsara's choice is always visible.
            Text superpowerLabel = CreateText("SuperpowerLabel", safeAreaGo.transform, font, "Superpower: none", TextAnchor.UpperLeft);
            RectTransform superpowerRect = superpowerLabel.rectTransform;
            superpowerRect.anchorMin = new Vector2(0f, 1f);
            superpowerRect.anchorMax = new Vector2(0f, 1f);
            superpowerRect.pivot = new Vector2(0f, 1f);
            superpowerRect.anchoredPosition = new Vector2(24f, -62f);
            superpowerRect.sizeDelta = new Vector2(420f, 36f);
            superpowerLabel.fontSize = 28;
            superpowerLabel.color = new Color32(238, 224, 168, 255);

            // Stage indicator, centered at the top so it reads separately from kills and health.
            Text stageLabel = CreateText("StageLabel", safeAreaGo.transform, font, "Samsara", TextAnchor.UpperCenter);
            RectTransform stageRect = stageLabel.rectTransform;
            stageRect.anchorMin = new Vector2(0.5f, 1f);
            stageRect.anchorMax = new Vector2(0.5f, 1f);
            stageRect.pivot = new Vector2(0.5f, 1f);
            stageRect.anchoredPosition = new Vector2(0f, -20f);
            stageRect.sizeDelta = new Vector2(400f, 48f);
            stageLabel.fontSize = 36;
            stageLabel.color = new Color32(232, 216, 96, 255);

            // Room objective: live enemies, then the open-exit prompt once the room is clear.
            Text objectiveLabel = CreateText("ObjectiveLabel", safeAreaGo.transform, font, "Enemies: 0", TextAnchor.UpperCenter);
            RectTransform objectiveRect = objectiveLabel.rectTransform;
            objectiveRect.anchorMin = new Vector2(0.5f, 1f);
            objectiveRect.anchorMax = new Vector2(0.5f, 1f);
            objectiveRect.pivot = new Vector2(0.5f, 1f);
            objectiveRect.anchoredPosition = new Vector2(0f, -66f);
            objectiveRect.sizeDelta = new Vector2(760f, 36f);
            objectiveLabel.fontSize = 26;
            objectiveLabel.color = new Color32(206, 218, 238, 255);

            // XP bar along the bottom, with the current level above it.
            Image xpBackground = CreateImage("XpBar", safeAreaGo.transform, new Color32(18, 16, 26, 220));
            RectTransform xpRect = xpBackground.rectTransform;
            xpRect.anchorMin = new Vector2(0.5f, 0f);
            xpRect.anchorMax = new Vector2(0.5f, 0f);
            xpRect.pivot = new Vector2(0.5f, 0f);
            xpRect.anchoredPosition = new Vector2(0f, 28f);
            xpRect.sizeDelta = new Vector2(820f, 22f);

            Image xpFill = CreateImage("Fill", xpBackground.transform, new Color32(64, 214, 158, 255));
            RectTransform xpFillRect = xpFill.rectTransform;
            xpFillRect.anchorMin = Vector2.zero;
            xpFillRect.anchorMax = new Vector2(0f, 1f);
            xpFillRect.offsetMin = Vector2.zero;
            xpFillRect.offsetMax = Vector2.zero;
            xpFill.raycastTarget = false;

            Text levelLabel = CreateText("LevelLabel", safeAreaGo.transform, font, "Lv 1", TextAnchor.MiddleCenter);
            RectTransform levelRect = levelLabel.rectTransform;
            levelRect.anchorMin = new Vector2(0.5f, 0f);
            levelRect.anchorMax = new Vector2(0.5f, 0f);
            levelRect.pivot = new Vector2(0.5f, 0f);
            levelRect.anchoredPosition = new Vector2(0f, 56f);
            levelRect.sizeDelta = new Vector2(300f, 32f);
            levelLabel.fontSize = 26;
            levelLabel.color = new Color32(198, 255, 232, 255);

            // Game-over overlay (hidden until the player dies).
            Image panel = CreateImage("GameOverPanel", safeAreaGo.transform, new Color32(10, 8, 16, 224));
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
            Image levelPanel = CreateImage("LevelUpPanel", safeAreaGo.transform, new Color32(10, 8, 16, 232));
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
            SetSerializedReference(hud, "healthGhost", healthGhost);
            SetSerializedReference(hud, "killLabel", killLabel);
            SetSerializedReference(hud, "playerHealth", player.GetComponent<Health>());
            SetSerializedReference(hud, "spawner", spawner);
            SetSerializedReference(hud, "experience", experience);
            SetSerializedReference(hud, "xpFill", xpFill);
            SetSerializedReference(hud, "levelLabel", levelLabel);
            SetSerializedReference(hud, "levelDirector", levelDirector);
            SetSerializedReference(hud, "stageLabel", stageLabel);
            SetSerializedReference(hud, "objectiveLabel", objectiveLabel);
            SetSerializedReference(hud, "superpowerLabel", superpowerLabel);
            SetSerializedReference(hud, "superpowerSelection", selection);

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
            SetSerializedReference(levelUp, "superpowerSelection", selection);
            SetSerializedReference(levelUp, "runUpgrades", player.GetComponent<RunUpgradeState>());
            SetSerializedReference(levelUp, "titleLabel", levelTitle);
            SetSerializedReference(levelUp, "hintLabel", levelHint);
            SetSerializedArray(levelUp, "cards", cards);

            var mobileVisibility = canvasGo.AddComponent<MobileControlsVisibility>();
            SetSerializedReference(mobileVisibility, "controlsRoot", mobileControls);

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

        private const string CircleSpritePath = "Assets/_Project/Art/UI/Circle.png";

        /// <summary>
        /// Generates (once) a white, antialiased circle used by the round touch controls. Keeping it
        /// white lets the Image color tint it, so every button shares one sprite.
        /// </summary>
        private static Sprite EnsureCircleSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            if (existing != null)
                return existing;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            const float center = (size - 1) * 0.5f;
            const float radius = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string absolutePath = Path.Combine(projectRoot, CircleSpritePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(CircleSpritePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CircleSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
        }

        private static Image CreateImage(string name, Transform parent, Color32 color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
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
            Experience experience,
            StageDoor[] doors,
            SuperpowerSelection selection,
            RunUpgradeState runUpgrades,
            PlayerStats playerStats)
        {
            SetSerializedReference(director, "settings", settings);
            SetSerializedReference(director, "playerMovement", player.GetComponent<PlayerMovement>());
            SetSerializedReference(director, "cameraFollow", cameraFollow);
            SetSerializedReference(director, "levelUpController", levelUp);
            SetSerializedReference(director, "experience", experience);
            SetSerializedReference(director, "superpowerSelection", selection);
            SetSerializedReference(director, "runUpgrades", runUpgrades);
            SetSerializedReference(director, "playerStats", playerStats);
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

        private static void SetSerializedString(Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[GameSceneBuilder] No serialized property '{propertyName}' on {target.GetType().Name}.");
                return;
            }

            property.stringValue = value;
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

        /// <summary>Reference backfill for a property on a SerializedObject that is already open.</summary>
        private static void SeedReferenceIfMissing(SerializedProperty property, Object value)
        {
            if (property != null && property.objectReferenceValue == null && value != null)
            {
                property.objectReferenceValue = value;
            }
        }

        /// <summary>String backfill that leaves designer-authored text untouched.</summary>
        private static void SeedStringIfEmpty(SerializedProperty property, string value)
        {
            if (property == null || string.IsNullOrEmpty(value) || !string.IsNullOrEmpty(property.stringValue))
            {
                return;
            }

            property.stringValue = value;
        }

        /// <summary>
        /// Seeds the VFX fields on both new and pre-existing actions. Fields still at their initializer
        /// defaults count as unauthored, so a rebuild backfills them without clobbering tuning.
        /// </summary>
        private static void SeedHitVfxFields(
            SuperpowerActionDefinition action,
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
                Debug.LogError("[GameSceneBuilder] Missing hit-VFX property on a superpower action.");
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
