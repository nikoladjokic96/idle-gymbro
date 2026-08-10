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

        // World height one character layer spans, in Unity units (the original 192px / 128 PPU).
        // Every layer's PPU is derived from this so all layers stay pixel-aligned to each other.
        private const float CharacterWorldHeightUnits = 1.5f;

        private static readonly Color SkinTone = new Color(0.87f, 0.62f, 0.44f);
        private static readonly Color DarkBrown = new Color(0.15f, 0.10f, 0.06f);
        private static readonly Color DarkGray = new Color(0.15f, 0.15f, 0.18f);

        // Real art dropped into OutputFolder MUST survive a scene rebuild. CoreLoopSceneBootstrap
        // calls Generate() on every build, so an unconditional write would silently destroy hours
        // of art the first time anyone rebuilds the scene. Existing files are therefore kept;
        // overwriting is an explicit, separate menu action.
        private static bool _overwriteExisting;
        private static int _written;
        private static int _kept;

        [MenuItem("IdleGymBro/Generate Placeholder Character Art")]
        public static void Generate()
        {
            Run(false);
        }

        [MenuItem("IdleGymBro/DANGER — Regenerate Placeholder Character Art (overwrites real art)")]
        public static void Regenerate()
        {
            if (!EditorUtility.DisplayDialog(
                    "Overwrite character art?",
                    $"This OVERWRITES every PNG in {OutputFolder} with generated placeholders.\n\n" +
                    "Any real art in that folder will be lost.",
                    "Overwrite", "Cancel"))
            {
                return;
            }

            Run(true);
        }

        private static void Run(bool overwriteExisting)
        {
            _overwriteExisting = overwriteExisting;
            _written = 0;
            _kept = 0;

            EnsureFolder(OutputFolder);

            int count = 0;

            for (int tierIndex = 0; tierIndex < 6; tierIndex++)
            {
                LabelAndWrite($"body_tier{tierIndex + 1}", BuildBodyPixels(tierIndex), "BODY" + (tierIndex + 1));
                count++;
            }

            LabelAndWrite("head_01", BuildRectPixels(64, 12, 150, 184, SkinTone), "HEAD");
            count++;

            LabelAndWrite("hair_01", BuildRectPixels(64, 13, 170, 190, DarkBrown), "HAIR");
            count++;

            LabelAndWrite("beard_01", BuildRectPixels(64, 11, 148, 162, DarkBrown), "BEARD");
            count++;

            LabelAndWrite("shorts_01", BuildRectPixels(64, 24, 64, 90, DarkGray), "SHORTS");
            count++;

            // Cosmetic variants (for the wardrobe): distinct colors/shapes per slot.
            LabelAndWrite("hair_02", BuildRectPixels(64, 13, 170, 190, new Color(0.85f, 0.70f, 0.35f)), "HAIR2");
            count++;

            LabelAndWrite("hair_03", BuildRectPixels(64, 10, 176, 188, new Color(0.55f, 0.55f, 0.58f)), "HAIR3");
            count++;

            LabelAndWrite("beard_02", BuildRectPixels(64, 8, 150, 160, DarkBrown), "BEARD2");
            count++;

            LabelAndWrite("shorts_02", BuildRectPixels(64, 24, 64, 90, new Color(0.60f, 0.16f, 0.16f)), "SHORTS2");
            count++;

            LabelAndWrite("shorts_03", BuildRectPixels(64, 24, 64, 90, new Color(0.16f, 0.22f, 0.50f)), "SHORTS3");
            count++;

            AssetDatabase.SaveAssets();
            Debug.Log($"[PlaceholderArtGenerator] {count} sprites ready ({_written} generated, {_kept} kept).");
        }

        // Bakes a magenta identifier into the bottom-left corner (an empty area on every
        // silhouette) so the placeholder self-identifies in the Project window / when opened.
        private static void LabelAndWrite(string fileName, Color[] pixels, string label)
        {
            PixelFont.DrawLabel(pixels, Width, Height, label, 2, 2, new Color(1f, 0f, 1f, 1f), 1);
            WriteSprite(fileName, pixels);
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

            if (_overwriteExisting || !File.Exists(absolutePath))
            {
                File.WriteAllBytes(absolutePath, png);
                _written++;
            }
            else
            {
                _kept++;
            }

            // Import settings are (re)applied either way, so a hand-authored PNG dropped into the
            // slot still gets the pivot/PPU/filtering the layer stack depends on.
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

            // MUST be Single. In Multiple mode Unity keeps the previously sliced sub-sprite rects
            // in the .meta, so swapping the PNG for art of a different size leaves the sprite
            // cropping a stale rectangle of the new image — which for the painted art was an empty
            // corner, i.e. an invisible character with everything else wired correctly.
            importer.spriteImportMode = SpriteImportMode.Single;

            // PPU is DERIVED from the texture height so every character layer occupies the same
            // world height (CharacterWorldHeightUnits) no matter what resolution the art is drawn
            // at. The 128x192 placeholders and 848x1264 painted art therefore render identically —
            // dropping in higher-res art never rescales the character or breaks layer alignment.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            importer.spritePixelsPerUnit = texture != null && texture.height > 0
                ? texture.height / CharacterWorldHeightUnits
                : Height / CharacterWorldHeightUnits;

            // Point filtering is for the pixel-art placeholders; painted art at 6x the resolution
            // needs bilinear or its edges alias badly when scaled down on a phone.
            importer.filterMode = texture != null && texture.height > Height ? FilterMode.Bilinear : FilterMode.Point;
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
