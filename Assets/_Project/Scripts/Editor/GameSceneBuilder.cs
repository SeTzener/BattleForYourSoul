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
using BattleForYourSoul.UI;
using BattleForYourSoul.World;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Builds the isometric test scene from scratch: settings asset, prefabs, definitions, isometric
    /// grid, ground tilemap, obstacles, collision walls, pixel-perfect camera, 2D light, the combat
    /// loop (enemy registry, spawner, player weapon) and the HUD.
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

        private const string FloorTileAPath = "Assets/_Project/Art/Environment/Tile_Floor_A.png";
        private const string FloorTileBPath = "Assets/_Project/Art/Environment/Tile_Floor_B.png";
        private const string RockPath = "Assets/_Project/Art/Environment/Prop_Rock.png";
        private const string PlayerSpritePath = "Assets/_Project/Art/Characters/Player.png";
        private const string EnemySpritePath = "Assets/_Project/Art/Characters/Enemy_Grunt.png";
        private const string ProjectileSpritePath = "Assets/_Project/Art/VFX/Projectile_Bolt.png";

        private const int GroundRadiusInCells = 8;
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

            BuildCamera(player.transform, settings);
            BuildLight();
            BuildHud(player, spawner);

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

        private static void BuildHud(GameObject player, EnemySpawner spawner)
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

            var hud = canvasGo.AddComponent<GameHud>();
            SetSerializedReference(hud, "healthFill", healthFill);
            SetSerializedReference(hud, "killLabel", killLabel);
            SetSerializedReference(hud, "playerHealth", player.GetComponent<Health>());
            SetSerializedReference(hud, "spawner", spawner);

            var gameOver = canvasGo.AddComponent<GameOverController>();
            SetSerializedReference(gameOver, "panel", panel.gameObject);
            SetSerializedReference(gameOver, "restartButton", restartButton);
            SetSerializedReference(gameOver, "menuButton", menuButton);
            SetSerializedReference(gameOver, "playerHealth", player.GetComponent<Health>());
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
    }
}
