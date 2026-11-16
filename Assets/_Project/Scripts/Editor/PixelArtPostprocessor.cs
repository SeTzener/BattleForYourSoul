using UnityEditor;
using UnityEngine;
using BattleForYourSoul.Core;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Applies pixel-art import settings to every texture dropped under <c>Assets/_Project/Art</c>,
    /// so new art is point-filtered, uncompressed and at the project PPU without hand-editing its
    /// importer. Alignment is left alone so a dropped sprite keeps whatever pivot it was authored
    /// with.
    /// </summary>
    public sealed class PixelArtPostprocessor : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art/";
        private const string SettingsAssetPath = "Assets/_Project/ScriptableObjects/GameSettings.asset";
        private const int FallbackPixelsPerUnit = 32;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            if (!(assetImporter is TextureImporter importer))
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = LoadPixelsPerUnit();
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
        }

        private static int LoadPixelsPerUnit()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsAssetPath);
            return settings != null ? settings.PixelsPerUnit : FallbackPixelsPerUnit;
        }
    }
}
