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
using BattleForYourSoul.World;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Builds the isometric test scene from scratch: settings asset, prefabs, definitions, isometric
    /// grid, ground tilemap, obstacles, collision walls, pixel-perfect camera, 2D light, the combat
    /// loop (enemy registry, spawner, player weapon), the stage/XP progression and the HUD.
    ///
    /// This is authoring tooling, not gameplay. Re-running it regenerates the scene deterministically.
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
        private const string WeaponDefinitionPath = "Assets/_Project/ScriptableObjects/Weapon_Default.asset";
        private const string EnemyDefinitionPath = "Assets/_Project/ScriptableObjects/Enemy_Grunt.asset";
        private const string ProgressionDefinitionPath = "Assets/_Project/ScriptableObjects/Progression_Default.asset";
        private const string MonsterScalingPath = "Assets/_Project/ScriptableObjects/MonsterScaling_Default.asset";
        private const string UpgradesFolder = "Assets/_Project/ScriptableObjects/Upgrades";

        private const string FloorTileAPath = "Assets/_Project/Art/Environment/Tile_Floor_A.png";
        private const string FloorTileBPath = "Assets/_Project/Art/Environment/Tile_Floor_B.png";
        private const string RockPath = "Assets/_Project/Art/Environment/Prop_Rock.png";
        private const string PlayerSpritePath = "Assets/_Project/Art/Characters/Player.png";
        private const string EnemySpritePath = "Assets/_Project/Art/Characters/Enemy_Grunt.png";
        private const string ProjectileSpritePath = "Assets/_Project/Art/VFX/Projectile_Bolt.png";
        private const string UpgradeIconSpritePath = "Assets/_Project/Art/UI/Upgrade_Icon.png";

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

            GameSettings settings = EnsureSettings();
            GameObject playerPrefab = EnsurePlayerPrefab(settings);
            EnemyDefinition enemyDefinition = EnsureEnemyDefinition();
            WeaponDefinition weaponDefinition = EnsureWeaponDefinition();
            ProgressionDefinition progression = EnsureProgressionDefinition();
            MonsterScaling scaling = EnsureMonsterScaling();
            UpgradeDefinition[] upgrades = EnsureUpgrades();
            GameObject enemyPrefab = EnsureEnemyPrefab(settings, enemyDefinition);
            GameObject projectilePrefab = EnsureProjectilePrefab(settings);

            TileBase floorA = EnsureTile("Tile_Floor_A", FloorTileAPath);
            TileBase floorB = EnsureTile("Tile_Floor_B", FloorTileBPath);

            var environment = new GameObject("Environment");

            BuildGrid(environment.transform, settings, floorA, floorB);
            BuildObstacles(environment.transform, settings);
            BuildWalls(environment.transform);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = Vector3.zero;

            EnemySpawner spawner = BuildCombat(player, weaponDefinition, enemyDefinition, enemyPrefab, projectilePrefab);
            (Experience experience, LevelDirector levelDirector) = BuildProgression(player, spawner, scaling, progression);

            BuildCamera(player.transform, settings);
            BuildLight();
            BuildHud(player, spawner, experience, levelDirector, upgrades);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log($"[GameSceneBuilder] Built {GameScenePath}");
        }

        private static GameSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsAssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
                AssetDatabase.SaveAssets();
            }

            return settings;
        }

        private static GameObject EnsurePlayerPrefab(GameSettings settings)
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
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            spriteRenderer.sortingLayerName = settings.EntitySortingLayer;
            spriteRenderer.sortingOrder = 0;

            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.45f, 0.5f);
            collider.offset = new Vector2(0f, 0.25f);

            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerVisuals>();

            var health = player.AddComponent<Health>();
            SetSerializedFloat(health, "maxHealth", PlayerMaxHealth);

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
            spriteRenderer.sprite = definition != null ? definition.Sprite : AssetDatabase.LoadAssetAtPath<Sprite>(EnemySpritePath);
            spriteRenderer.sortingLayerName = settings.EntitySortingLayer;
            spriteRenderer.sortingOrder = 0;

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

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(enemy, EnemyPrefabPath);
            Object.DestroyImmediate(enemy);
            return prefab;
        }

        private static GameObject EnsureProjectilePrefab(GameSettings settings)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(ProjectilePrefabPath);
            }

            var projectile = new GameObject("Projectile");
            projectile.layer = LayerMask.NameToLayer("Projectile");

            var spriteRenderer = projectile.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectileSpritePath);
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

            projectile.AddComponent<Projectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(projectile, ProjectilePrefabPath);
            Object.DestroyImmediate(projectile);
            return prefab;
        }

        private static WeaponDefinition EnsureWeaponDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(definition, WeaponDefinitionPath);
            }

            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static EnemyDefinition EnsureEnemyDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, EnemyDefinitionPath);
            }

            SetSerializedReference(definition, "sprite", AssetDatabase.LoadAssetAtPath<Sprite>(EnemySpritePath));
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static ProgressionDefinition EnsureProgressionDefinition()
        {
            var progression = AssetDatabase.LoadAssetAtPath<ProgressionDefinition>(ProgressionDefinitionPath);
            if (progression == null)
            {
                progression = ScriptableObject.CreateInstance<ProgressionDefinition>();
                AssetDatabase.CreateAsset(progression, ProgressionDefinitionPath);
            }

            EditorUtility.SetDirty(progression);
            AssetDatabase.SaveAssets();
            return progression;
        }

        private static MonsterScaling EnsureMonsterScaling()
        {
            var scaling = AssetDatabase.LoadAssetAtPath<MonsterScaling>(MonsterScalingPath);
            if (scaling == null)
            {
                scaling = ScriptableObject.CreateInstance<MonsterScaling>();
                AssetDatabase.CreateAsset(scaling, MonsterScalingPath);
            }

            EditorUtility.SetDirty(scaling);
            AssetDatabase.SaveAssets();
            return scaling;
        }

        private static UpgradeDefinition[] EnsureUpgrades()
        {
            EnsureFolder(UpgradesFolder);
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(UpgradeIconSpritePath);

            return new[]
            {
                EnsureUpgrade("Upgrade_SharpenedBolts", "sharpened-bolts", "Sharpened Bolts", "Increase projectile damage by 1.", new Color32(232, 96, 96, 255), icon, UpgradeStat.DamageFlat, 1f),
                EnsureUpgrade("Upgrade_RapidFire", "rapid-fire", "Rapid Fire", "Fire 12% faster.", new Color32(240, 168, 72, 255), icon, UpgradeStat.FireRatePercent, 12f),
                EnsureUpgrade("Upgrade_FleetFooted", "fleet-footed", "Fleet Footed", "Move 8% faster.", new Color32(120, 214, 120, 255), icon, UpgradeStat.MoveSpeedPercent, 8f),
                EnsureUpgrade("Upgrade_Vitality", "vitality", "Vitality", "Gain 20 max health.", new Color32(240, 120, 168, 255), icon, UpgradeStat.MaxHealthFlat, 20f),
                EnsureUpgrade("Upgrade_SplitShot", "split-shot", "Split Shot", "Fire one extra projectile.", new Color32(178, 130, 240, 255), icon, UpgradeStat.ExtraProjectiles, 1f),
                EnsureUpgrade("Upgrade_Velocity", "velocity", "Velocity", "Projectiles travel 15% faster.", new Color32(120, 210, 240, 255), icon, UpgradeStat.ProjectileSpeedPercent, 15f),
            };
        }

        private static UpgradeDefinition EnsureUpgrade(string fileName, string id, string displayName, string description, Color32 color, Sprite icon, UpgradeStat stat, float value)
        {
            string path = $"{UpgradesFolder}/{fileName}.asset";
            var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
            if (upgrade == null)
            {
                upgrade = ScriptableObject.CreateInstance<UpgradeDefinition>();
                AssetDatabase.CreateAsset(upgrade, path);
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

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(upgrade);
            AssetDatabase.SaveAssets();
            return upgrade;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static TileBase EnsureTile(string name, string spritePath)
        {
            string tilePath = $"Assets/_Project/ScriptableObjects/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = name;
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();
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
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RockPath);
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
                spriteRenderer.sortingOrder = 0;

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

        private static EnemySpawner BuildCombat(
            GameObject player,
            WeaponDefinition weaponDefinition,
            EnemyDefinition enemyDefinition,
            GameObject enemyPrefab,
            GameObject projectilePrefab)
        {
            var registryGo = new GameObject("Enemy Registry");
            var registry = registryGo.AddComponent<EnemyRegistry>();

            var spawnerGo = new GameObject("Enemy Spawner");
            var spawner = spawnerGo.AddComponent<EnemySpawner>();
            SetSerializedReference(spawner, "enemyPrefab", enemyPrefab.GetComponent<Enemy>());
            SetSerializedReference(spawner, "definition", enemyDefinition);
            SetSerializedReference(spawner, "registry", registry);
            SetSerializedReference(spawner, "target", player.transform);

            var weapon = player.AddComponent<WeaponController>();
            SetSerializedReference(weapon, "definition", weaponDefinition);
            SetSerializedReference(weapon, "projectilePrefab", projectilePrefab.GetComponent<Projectile>());
            SetSerializedReference(weapon, "registry", registry);

            return spawner;
        }

        private static (Experience, LevelDirector) BuildProgression(GameObject player, EnemySpawner spawner, MonsterScaling scaling, ProgressionDefinition progression)
        {
            var experience = player.AddComponent<Experience>();
            SetSerializedReference(experience, "progression", progression);

            var stats = player.AddComponent<PlayerStats>();
            SetSerializedReference(stats, "health", player.GetComponent<Health>());
            SetSerializedReference(stats, "weapon", player.GetComponent<WeaponController>());
            SetSerializedReference(stats, "movement", player.GetComponent<PlayerMovement>());

            // The stage owns the monster level and feeds it to the spawner; the spawner in turn counts
            // kills for it. Both references are assigned here because the objects are created together.
            var directorGo = new GameObject("Level Director");
            var director = directorGo.AddComponent<LevelDirector>();
            SetSerializedReference(director, "spawner", spawner);
            SetSerializedReference(director, "scaling", scaling);
            SetSerializedReference(spawner, "levelDirector", director);

            var granterGo = new GameObject("Kill XP Granter");
            var granter = granterGo.AddComponent<KillXpGranter>();
            SetSerializedReference(granter, "spawner", spawner);
            SetSerializedReference(granter, "experience", experience);

            return (experience, director);
        }

        private static void BuildHud(GameObject player, EnemySpawner spawner, Experience experience, LevelDirector levelDirector, UpgradeDefinition[] upgrades)
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

            // Stage indicator, centered at the top so it reads separately from kills and health.
            Text stageLabel = CreateText("StageLabel", canvasGo.transform, font, "Stage 1", TextAnchor.UpperCenter);
            RectTransform stageRect = stageLabel.rectTransform;
            stageRect.anchorMin = new Vector2(0.5f, 1f);
            stageRect.anchorMax = new Vector2(0.5f, 1f);
            stageRect.pivot = new Vector2(0.5f, 1f);
            stageRect.anchoredPosition = new Vector2(0f, -20f);
            stageRect.sizeDelta = new Vector2(400f, 48f);
            stageLabel.fontSize = 36;
            stageLabel.color = new Color32(232, 216, 96, 255);

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
            SetSerializedArray(levelUp, "cards", cards);
            SetSerializedArray(levelUp, "upgrades", upgrades);
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

        private static void BuildCamera(Transform target, GameSettings settings)
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
        }

        private static void BuildLight()
        {
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;
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
