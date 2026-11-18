using System.IO;
using UnityEditor;
using UnityEngine;
using BattleForYourSoul.Core;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Generates the temporary pixel-art placeholders the M2 test scene needs, and imports them with
    /// pixel-art-correct settings (point filter, fixed PPU, no compression, no mipmaps).
    ///
    /// These are throwaway assets. Replace them with real art later; the import settings applied here
    /// are the ones real pixel art should keep.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        private const string SettingsAssetPath = "Assets/_Project/ScriptableObjects/GameSettings.asset";

        // Fallbacks used when GameSettings has not been created yet. When it exists, its values win,
        // so PPU and tile size stay defined in one place.
        private static int _ppu = 32;
        private static Vector2Int _tileSize = new Vector2Int(32, 16);

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        [MenuItem("Tools/BattleForYourSoul/Generate Placeholder Art")]
        public static void GenerateAll()
        {
            LoadSettings();

            EnsureFolder("Assets/_Project/Art/Environment");
            EnsureFolder("Assets/_Project/Art/Characters");
            EnsureFolder("Assets/_Project/Art/VFX");
            EnsureFolder("Assets/_Project/Art/UI");

            GenerateFloorTiles();
            GeneratePlayer();
            GenerateRock();
            GenerateEnemyGrunt();
            GenerateProjectileBolt();
            GenerateUpgradeIcon();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlaceholderArtGenerator] Placeholder art generated.");
        }

        private static void LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsAssetPath);
            if (settings != null)
            {
                _ppu = settings.PixelsPerUnit;
                _tileSize = settings.TileSizeInPixels;
            }
        }

        private static void GenerateFloorTiles()
        {
            CreateFloorTile(
                "Assets/_Project/Art/Environment/Tile_Floor_A.png",
                new Color32(86, 125, 70, 255),
                new Color32(70, 105, 58, 255));

            CreateFloorTile(
                "Assets/_Project/Art/Environment/Tile_Floor_B.png",
                new Color32(78, 115, 64, 255),
                new Color32(63, 96, 52, 255));
        }

        private static void CreateFloorTile(string path, Color32 fill, Color32 edge)
        {
            int w = _tileSize.x;
            int h = _tileSize.y;

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - w * 0.5f) / (w * 0.5f);
                    float dy = Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f);
                    float d = dx + dy;

                    Color32 c = Clear;
                    if (d <= 1f)
                    {
                        c = d > 0.78f ? edge : fill;
                    }

                    px[y * w + x] = c;
                }
            }

            WriteSprite(path, px, w, h, SpriteAlignment.Center);
        }

        private static void GeneratePlayer()
        {
            const int w = 16;
            const int h = 24;

            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Clear;
            }

            var skin = new Color32(240, 196, 150, 255);
            var hair = new Color32(90, 58, 42, 255);
            var shirt = new Color32(70, 116, 206, 255);
            var shirtDark = new Color32(48, 84, 158, 255);
            var pants = new Color32(58, 58, 78, 255);
            var boot = new Color32(40, 34, 30, 255);

            // Legs and boots (origin is bottom-left; feet sit on y = 0).
            FillRect(px, w, h, 5, 0, 3, 7, pants);
            FillRect(px, w, h, 9, 0, 3, 7, pants);
            FillRect(px, w, h, 5, 0, 3, 2, boot);
            FillRect(px, w, h, 9, 0, 3, 2, boot);

            // Torso and arms.
            FillRect(px, w, h, 4, 8, 8, 9, shirt);
            FillRect(px, w, h, 2, 10, 2, 6, shirtDark);
            FillRect(px, w, h, 12, 10, 2, 6, shirtDark);
            FillRect(px, w, h, 2, 8, 2, 2, skin);
            FillRect(px, w, h, 12, 8, 2, 2, skin);

            // Head and hair.
            FillEllipse(px, w, h, 8, 20, 4, 4, skin);
            FillEllipse(px, w, h, 8, 22, 4, 2, hair);

            WriteSprite("Assets/_Project/Art/Characters/Player.png", px, w, h, SpriteAlignment.BottomCenter);
        }

        private static void GenerateRock()
        {
            const int w = 32;
            const int h = 24;

            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Clear;
            }

            var rock = new Color32(126, 124, 132, 255);
            var rockDark = new Color32(92, 90, 100, 255);
            var rockLight = new Color32(158, 156, 164, 255);

            FillEllipse(px, w, h, 16, 10, 15, 9, rock);
            FillEllipse(px, w, h, 16, 6, 15, 5, rockDark);
            FillEllipse(px, w, h, 11, 14, 6, 4, rockLight);

            WriteSprite("Assets/_Project/Art/Environment/Prop_Rock.png", px, w, h, SpriteAlignment.BottomCenter);
        }

        private static void GenerateEnemyGrunt()
        {
            const int w = 16;
            const int h = 16;

            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Clear;
            }

            var body = new Color32(112, 72, 138, 255);
            var bodyDark = new Color32(78, 48, 102, 255);
            var belly = new Color32(154, 112, 182, 255);
            var eye = new Color32(244, 244, 248, 255);
            var pupil = new Color32(28, 18, 40, 255);
            var horn = new Color32(222, 214, 190, 255);
            var foot = new Color32(58, 36, 76, 255);

            // Feet (origin is bottom-left, so feet sit on y = 0).
            FillRect(px, w, h, 3, 0, 4, 2, foot);
            FillRect(px, w, h, 9, 0, 4, 2, foot);

            // Body and belly.
            FillEllipse(px, w, h, 8, 6, 6, 5, body);
            FillEllipse(px, w, h, 8, 5, 4, 3, belly);
            FillEllipse(px, w, h, 8, 3, 6, 2, bodyDark);

            // Horns.
            FillRect(px, w, h, 3, 10, 2, 3, horn);
            FillRect(px, w, h, 11, 10, 2, 3, horn);

            // Eyes.
            FillRect(px, w, h, 4, 7, 3, 3, eye);
            FillRect(px, w, h, 9, 7, 3, 3, eye);
            FillRect(px, w, h, 5, 7, 1, 2, pupil);
            FillRect(px, w, h, 10, 7, 1, 2, pupil);

            WriteSprite("Assets/_Project/Art/Characters/Enemy_Grunt.png", px, w, h, SpriteAlignment.BottomCenter);
        }

        private static void GenerateProjectileBolt()
        {
            const int w = 8;
            const int h = 8;

            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Clear;
            }

            var glow = new Color32(122, 190, 255, 150);
            var glowEdge = new Color32(86, 150, 236, 90);
            var core = new Color32(232, 250, 255, 255);

            FillDiamond(px, w, h, 4, 4, 4, 4, glowEdge);
            FillDiamond(px, w, h, 4, 4, 3, 3, glow);
            FillDiamond(px, w, h, 4, 4, 1, 2, core);

            WriteSprite("Assets/_Project/Art/VFX/Projectile_Bolt.png", px, w, h, SpriteAlignment.Center);
        }

        private static void GenerateUpgradeIcon()
        {
            const int w = 16;
            const int h = 16;

            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Clear;
            }

            // A plain white rune so the level-up card can tint it per upgrade.
            var white = new Color32(255, 255, 255, 255);

            FillDiamond(px, w, h, 8, 8, 7, 7, white);
            FillDiamond(px, w, h, 8, 8, 5, 5, Clear);
            FillRect(px, w, h, 7, 4, 2, 8, white);
            FillRect(px, w, h, 4, 7, 8, 2, white);

            WriteSprite("Assets/_Project/Art/UI/Upgrade_Icon.png", px, w, h, SpriteAlignment.Center);
        }

        private static void FillDiamond(Color32[] px, int w, int h, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                if (y < 0 || y >= h)
                {
                    continue;
                }

                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= w)
                    {
                        continue;
                    }

                    float d = Mathf.Abs(x + 0.5f - cx) / rx + Mathf.Abs(y + 0.5f - cy) / ry;
                    if (d <= 1f)
                    {
                        px[y * w + x] = color;
                    }
                }
            }
        }

        private static void FillRect(Color32[] px, int w, int h, int x0, int y0, int rw, int rh, Color32 color)
        {
            for (int y = y0; y < y0 + rh; y++)
            {
                if (y < 0 || y >= h)
                {
                    continue;
                }

                for (int x = x0; x < x0 + rw; x++)
                {
                    if (x < 0 || x >= w)
                    {
                        continue;
                    }

                    px[y * w + x] = color;
                }
            }
        }

        private static void FillEllipse(Color32[] px, int w, int h, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                if (y < 0 || y >= h)
                {
                    continue;
                }

                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= w)
                    {
                        continue;
                    }

                    float nx = (x + 0.5f - cx) / rx;
                    float ny = (y + 0.5f - cy) / ry;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        px[y * w + x] = color;
                    }
                }
            }
        }

        private static void WriteSprite(string assetPath, Color32[] pixels, int w, int h, SpriteAlignment alignment)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(pixels);
            tex.Apply();

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string absolutePath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllBytes(absolutePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = _ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)alignment;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
