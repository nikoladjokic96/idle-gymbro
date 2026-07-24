using UnityEditor;
using UnityEngine;
using System.IO;

namespace IdleGymBro.EditorTools
{
    // Generates real PNG placeholder character sprites with correct import settings, so future
    // hand-drawn pixel art can replace these files 1:1 (same path, same pivot/PPU/filtering).
    public static class PlaceholderArtGenerator
    {
        private const string OutputFolder = "Assets/_Game/Art/Character/Placeholders";
        private const int Width = 128;
        private const int Height = 192;

        private static readonly Color SkinTone = new Color(0.87f, 0.62f, 0.44f);
        private static readonly Color DarkBrown = new Color(0.15f, 0.10f, 0.06f);
        private static readonly Color DarkGray = new Color(0.15f, 0.15f, 0.18f);

        [MenuItem("IdleGymBro/Generate Placeholder Character Art")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            int count = 0;

            for (int tierIndex = 0; tierIndex < 6; tierIndex++)
            {
                WriteSprite($"body_tier{tierIndex + 1}", BuildBodyPixels(tierIndex));
                count++;
            }

            WriteSprite("head_01", BuildRectPixels(64, 12, 150, 184, SkinTone));
            count++;

            WriteSprite("hair_01", BuildRectPixels(64, 13, 170, 190, DarkBrown));
            count++;

            WriteSprite("beard_01", BuildRectPixels(64, 11, 148, 162, DarkBrown));
            count++;

            WriteSprite("shorts_01", BuildRectPixels(64, 24, 64, 90, DarkGray));
            count++;

            AssetDatabase.SaveAssets();
            Debug.Log($"[PlaceholderArtGenerator] {count} sprites generated.");
        }

        private static Color[] BuildBodyPixels(int tierIndex)
        {
            var pixels = new Color[Width * Height];

            // Legs: two vertical rects, centered at x=64±11, width 10 (half-width 5).
            FillRect(pixels, 53 - 5, 53 + 5, 8, 78, SkinTone);
            FillRect(pixels, 75 - 5, 75 + 5, 8, 78, SkinTone);

            // Torso: half-width grows with tier (bulkier physique at higher tiers).
            int torsoHalfWidth = 14 + 4 * tierIndex;
            FillRect(pixels, 64 - torsoHalfWidth, 64 + torsoHalfWidth, 78, 150, SkinTone);

            // Shoulders/arms: flush against the torso sides, width grows with tier.
            int armWidth = 8 + tierIndex;
            FillRect(pixels, 64 - torsoHalfWidth - armWidth, 64 - torsoHalfWidth, 110, 148, SkinTone);
            FillRect(pixels, 64 + torsoHalfWidth, 64 + torsoHalfWidth + armWidth, 110, 148, SkinTone);

            // Head.
            FillRect(pixels, 64 - 12, 64 + 12, 150, 184, SkinTone);

            return pixels;
        }

        private static Color[] BuildRectPixels(int centerX, int halfWidth, int yMin, int yMax, Color color)
        {
            var pixels = new Color[Width * Height];
            FillRect(pixels, centerX - halfWidth, centerX + halfWidth, yMin, yMax, color);
            return pixels;
        }

        private static void FillRect(Color[] pixels, int xMin, int xMax, int yMin, int yMax, Color color)
        {
            xMin = Mathf.Clamp(xMin, 0, Width - 1);
            xMax = Mathf.Clamp(xMax, 0, Width - 1);
            yMin = Mathf.Clamp(yMin, 0, Height - 1);
            yMax = Mathf.Clamp(yMax, 0, Height - 1);

            for (int y = yMin; y <= yMax; y++)
            {
                int rowOffset = y * Width;
                for (int x = xMin; x <= xMax; x++)
                {
                    pixels[rowOffset + x] = color;
                }
            }
        }

        private static void WriteSprite(string fileName, Color[] pixels)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string path = $"{OutputFolder}/{fileName}.png";
            string absolutePath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            File.WriteAllBytes(absolutePath, png);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(path);
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[PlaceholderArtGenerator] Could not get TextureImporter for {path}.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 128;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);

            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
