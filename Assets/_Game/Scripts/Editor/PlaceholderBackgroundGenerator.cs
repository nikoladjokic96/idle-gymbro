using UnityEditor;
using UnityEngine;
using System.IO;

namespace IdleGymBro.EditorTools
{
    // Generates one full-screen placeholder background per location, each filled with a distinct
    // flat color, a darker ground band, and a big baked label — so the real background art can
    // replace each file 1:1 and it is obvious which location it belongs to.
    public static class PlaceholderBackgroundGenerator
    {
        private const string OutputFolder = "Assets/_Game/Art/Backgrounds/Placeholders";
        private const int Width = 1080;
        private const int Height = 1920;

        // World height one background spans, in Unity units (the original 1920px / 128 PPU).
        // PPU is DERIVED from this so a background of ANY resolution covers the same world area —
        // the pixel-art backgrounds (216x384) must frame identically to these 1080x1920 placeholders.
        private const float BackgroundWorldHeightUnits = 15f;

        private static readonly Color LabelColor = new Color(1f, 0f, 1f, 1f);

        private struct Bg
        {
            public string FileName;
            public string Label;
            public Color Base;

            public Bg(string fileName, string label, Color baseColor)
            {
                FileName = fileName;
                Label = label;
                Base = baseColor;
            }
        }

        // See PlaceholderArtGenerator: the bootstrap regenerates on every scene build, so existing
        // files are kept rather than overwritten — otherwise real backgrounds die on first rebuild.
        private static bool _overwriteExisting;
        private static int _written;
        private static int _kept;

        [MenuItem("IdleGymBro/Generate Placeholder Backgrounds")]
        public static void Generate()
        {
            Run(false);
        }

        [MenuItem("IdleGymBro/DANGER — Regenerate Placeholder Backgrounds (overwrites real art)")]
        public static void Regenerate()
        {
            if (!EditorUtility.DisplayDialog(
                    "Overwrite backgrounds?",
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

            var backgrounds = new[]
            {
                new Bg("bg_home", "HOME", new Color(0.35f, 0.30f, 0.28f)),
                new Bg("bg_street", "STREET", new Color(0.30f, 0.32f, 0.38f)),
                new Bg("bg_basic_gym", "GYM", new Color(0.22f, 0.28f, 0.34f)),
                new Bg("bg_hardcore_gym", "HARDCORE", new Color(0.20f, 0.20f, 0.24f)),
                new Bg("bg_beach", "BEACH", new Color(0.20f, 0.45f, 0.62f)),
                new Bg("bg_olympia", "OLYMPIA", new Color(0.42f, 0.34f, 0.14f)),
            };

            foreach (Bg bg in backgrounds)
            {
                Write(bg);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PlaceholderBackgroundGenerator] {backgrounds.Length} backgrounds ready ({_written} generated, {_kept} kept).");
        }

        private static void Write(Bg bg)
        {
            var pixels = new Color[Width * Height];

            // Flat base color everywhere.
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = bg.Base;
            }

            // Darker ground band across the bottom third (a rough floor reference for the character).
            Color ground = bg.Base * 0.6f;
            ground.a = 1f;
            FillRect(pixels, 0, Width - 1, 0, Height / 3, ground);

            // Big centered name + a smaller "BACKGROUND" caption above it.
            int nameWidth = PixelFont.MeasureWidth(bg.Label, 10);
            PixelFont.DrawLabel(pixels, Width, Height, bg.Label, (Width - nameWidth) / 2, 1500, LabelColor, 10);

            int captionWidth = PixelFont.MeasureWidth("BACKGROUND", 5);
            PixelFont.DrawLabel(pixels, Width, Height, "BACKGROUND", (Width - captionWidth) / 2, 1650, LabelColor, 5);

            WriteSprite(bg.FileName, pixels);
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

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(path);
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[PlaceholderBackgroundGenerator] Could not get TextureImporter for {path}.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;

            // Single, for the same reason as the character art: Multiple mode remembers sliced
            // sub-sprite rects, so replacing this PNG with painted art of another size would crop
            // a stale rectangle instead of showing the new image.
            importer.spriteImportMode = SpriteImportMode.Single;

            // Derived, not hardcoded: a 1080x1920 placeholder and a 216x384 pixel-art background
            // both resolve to the same 15 world units tall, so swapping one for the other cannot
            // silently rescale the scene (a fixed 128 would render the small art at 1/5 size).
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            importer.spritePixelsPerUnit = texture != null && texture.height > 0
                ? texture.height / BackgroundWorldHeightUnits
                : 128f;

            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
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
