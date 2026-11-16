using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using BattleForYourSoul.Core;
using BattleForYourSoul.Player;
using BattleForYourSoul.World;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Builds the isometric test scene from scratch: settings asset, player prefab, isometric grid,
    /// ground tilemap, obstacles, collision walls, pixel-perfect camera and 2D light.
    ///
    /// This is authoring tooling, not gameplay. Re-running it regenerates the scene deterministically.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string GameScenePath = ScenesFolder + "/Game.unity";

        private const string SettingsAssetPath = "Assets/_Project/ScriptableObjects/GameSettings.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string FloorTileAPath = "Assets/_Project/Art/Environment/Tile_Floor_A.png";
        private const string FloorTileBPath = "Assets/_Project/Art/Environment/Tile_Floor_B.png";
        private const string RockPath = "Assets/_Project/Art/Environment/Prop_Rock.png";
        private const string PlayerSpritePath = "Assets/_Project/Art/Characters/Player.png";

        private const int GroundRadiusInCells = 8;

        [MenuItem("Tools/BattleForYourSoul/Build Game Scene")]
        public static void BuildGameScene()
        {
            // Create the scene first: EditorSceneManager.NewScene unloads unreferenced asset objects
            // (notably Tile assets), so anything loaded from the AssetDatabase before this call can
            // come back as a "fake null". Loading assets after the scene switch keeps them valid.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameSettings settings = EnsureSettings();
            GameObject playerPrefab = EnsurePlayerPrefab(settings);
            TileBase floorA = EnsureTile("Tile_Floor_A", FloorTileAPath);
            TileBase floorB = EnsureTile("Tile_Floor_B", FloorTileBPath);

            var environment = new GameObject("Environment");

            BuildGrid(environment.transform, settings, floorA, floorB);
            BuildObstacles(environment.transform, settings);
            BuildWalls(environment.transform);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = Vector3.zero;

            BuildCamera(player.transform, settings);
            BuildLight();

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
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null)
            {
                return existing;
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

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            return prefab;
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
    }
}
