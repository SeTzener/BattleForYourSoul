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
            EnsureFolder("Assets/_Project/Art/Items");

            GenerateFloorTiles();
            GeneratePlayer();
            GenerateRock();
            GenerateEnemyGrunt();
            GenerateProjectileBolt();
            GenerateUpgradeIcon();
            GenerateDoors();
            GenerateWeapons();
            GenerateAttackVfx();

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

        /// <summary>
        /// Generates the visible locked/open exit placeholders. The open sprite is mostly white so
        /// StageDoor can tint it with the category colour at runtime.
        /// </summary>
        public static void GenerateDoors()
        {
            LoadSettings();
            EnsureFolder("Assets/_Project/Art/Environment");

            const int w = 32;
            const int h = 40;

            var stone = new Color32(116, 114, 128, 255);
            var stoneDark = new Color32(74, 72, 86, 255);
            var stoneLight = new Color32(158, 156, 170, 255);
            var opening = new Color32(30, 26, 42, 255);
            var bar = new Color32(94, 90, 106, 255);
            var metal = new Color32(184, 180, 166, 255);

            // Stone frame: a rounded arch over straight jambs.
            var frame = new Color32[w * h];
            FillRect(frame, w, h, 4, 0, 24, 31, stone);
            FillEllipse(frame, w, h, 16, 31, 12, 9, stone);
            FillRect(frame, w, h, 4, 0, 3, 31, stoneDark);
            FillRect(frame, w, h, 25, 0, 3, 31, stoneDark);
            FillRect(frame, w, h, 6, 2, 2, 28, stoneLight);
            FillEllipse(frame, w, h, 16, 34, 9, 5, stoneLight);

            // Carve the doorway back out of the frame.
            FillRect(frame, w, h, 8, 0, 16, 31, opening);
            FillEllipse(frame, w, h, 16, 31, 8, 7, opening);

            // Locked: iron bars and a plate across the doorway.
            var locked = (Color32[])frame.Clone();
            FillRect(locked, w, h, 11, 0, 2, 31, bar);
            FillRect(locked, w, h, 19, 0, 2, 31, bar);
            FillRect(locked, w, h, 8, 10, 16, 2, bar);
            FillRect(locked, w, h, 8, 21, 16, 2, bar);
            FillRect(locked, w, h, 13, 15, 6, 6, metal);
            FillRect(locked, w, h, 15, 17, 2, 2, opening);
            WriteSprite("Assets/_Project/Art/Environment/Door_Closed.png", locked, w, h, SpriteAlignment.BottomCenter);

            // Open: a glowing white portal, tinted per category by StageDoor.
            var open = (Color32[])frame.Clone();
            var glowEdge = new Color32(148, 196, 232, 190);
            var glowCore = new Color32(246, 252, 255, 255);
            FillEllipse(open, w, h, 16, 22, 7, 14, glowEdge);
            FillRect(open, w, h, 10, 0, 12, 23, glowEdge);
            FillEllipse(open, w, h, 16, 22, 5, 12, glowCore);
            FillRect(open, w, h, 12, 0, 8, 23, glowCore);
            WriteSprite("Assets/_Project/Art/Environment/Door_Open.png", open, w, h, SpriteAlignment.BottomCenter);
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

        private static void GenerateWeapons()
        {
            var steel = new Color32(206, 216, 230, 255);
            var steelDark = new Color32(140, 154, 178, 255);
            var wood = new Color32(150, 104, 64, 255);
            var gold = new Color32(232, 186, 92, 255);
            var stringColor = new Color32(238, 226, 188, 255);

            var sword = new Color32[16 * 16];
            FillRect(sword, 16, 16, 7, 6, 2, 9, steel);
            sword[15 * 16 + 7] = steel;
            sword[15 * 16 + 8] = steel;
            FillRect(sword, 16, 16, 7, 7, 1, 6, steelDark);
            FillRect(sword, 16, 16, 4, 5, 8, 1, gold);
            FillRect(sword, 16, 16, 7, 2, 2, 3, wood);
            FillRect(sword, 16, 16, 6, 1, 4, 1, gold);
            WriteSprite("Assets/_Project/Art/Items/Weapon_Sword.png", sword, 16, 16, SpriteAlignment.BottomCenter);

            var spear = new Color32[16 * 16];
            FillRect(spear, 16, 16, 7, 1, 2, 10, wood);
            FillRect(spear, 16, 16, 7, 11, 2, 3, steel);
            spear[15 * 16 + 7] = steel;
            spear[15 * 16 + 8] = steel;
            FillRect(spear, 16, 16, 5, 11, 2, 1, steelDark);
            FillRect(spear, 16, 16, 9, 11, 2, 1, steelDark);
            WriteSprite("Assets/_Project/Art/Items/Weapon_Spear.png", spear, 16, 16, SpriteAlignment.BottomCenter);

            var bow = new Color32[16 * 16];
            FillRect(bow, 16, 16, 4, 2, 1, 12, stringColor);
            FillRect(bow, 16, 16, 6, 2, 2, 2, wood);
            FillRect(bow, 16, 16, 8, 4, 2, 4, wood);
            FillRect(bow, 16, 16, 9, 8, 1, 3, wood);
            FillRect(bow, 16, 16, 8, 11, 2, 2, wood);
            FillRect(bow, 16, 16, 6, 13, 2, 1, wood);
            WriteSprite("Assets/_Project/Art/Items/Weapon_Bow.png", bow, 16, 16, SpriteAlignment.BottomCenter);

            var arrow = new Color32[8 * 8];
            FillRect(arrow, 8, 8, 1, 3, 5, 2, wood);
            FillRect(arrow, 8, 8, 6, 2, 2, 4, steel);
            FillRect(arrow, 8, 8, 0, 2, 1, 2, gold);
            FillRect(arrow, 8, 8, 0, 5, 1, 2, gold);
            WriteSprite("Assets/_Project/Art/VFX/Projectile_Arrow.png", arrow, 8, 8, SpriteAlignment.Center);
        }

        /// <summary>
        /// Throwaway attack VFX. Both sprites point along +X so the runtime only has to rotate them to
        /// the attack direction. They are white so each action can tint them.
        /// </summary>
        private static void GenerateAttackVfx()
        {
            const int w = 32;
            const int h = 32;

            // Sword: a crescent sweeping from -75 to +75 degrees, thick in the middle and tapered at
            // the tips. A dim wide pass underneath a bright narrow core reads as a slash at any size.
            var slash = new Color32[w * h];
            FillArc(slash, w, h, 16, 16, 8, 15, 78f, new Color32(150, 200, 255, 110));
            FillArc(slash, w, h, 16, 16, 10, 14, 70f, new Color32(255, 255, 255, 255));
            WriteSprite("Assets/_Project/Art/VFX/VFX_Slash.png", slash, w, h, SpriteAlignment.Center);

            // Spear: a narrow tapered streak with a bright core, pointing right.
            var thrust = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f - 16) / 15f;
                    float ny = (y + 0.5f - 16) / 15f;
                    if (nx < -0.95f || nx > 1f)
                    {
                        continue;
                    }

                    float taper = Mathf.Clamp01(1f - Mathf.Max(0f, nx) * 0.85f);
                    float thickness = 0.3f * taper;
                    if (Mathf.Abs(ny) > thickness)
                    {
                        continue;
                    }

                    float edge = 1f - Mathf.Abs(ny) / thickness;
                    thrust[y * w + x] = edge > 0.5f
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(150, 210, 255, 130);
                }
            }

            WriteSprite("Assets/_Project/Art/VFX/VFX_Thrust.png", thrust, w, h, SpriteAlignment.Center);
        }

        /// <summary>Fills an arc centred on +X, tapering toward its ends for a crescent silhouette.</summary>
        private static void FillArc(Color32[] px, int w, int h, int cx, int cy, int inner, int outer, float halfAngleDegrees, Color32 color)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    if (radius < inner || radius > outer)
                    {
                        continue;
                    }

                    float angle = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                    if (angle > halfAngleDegrees)
                    {
                        continue;
                    }

                    float t = angle / halfAngleDegrees;
                    float thickness = (outer - inner) * (1f - t * t * 0.75f);
                    if (radius <= inner + thickness)
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
