using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGymBro.EditorTools
{
    // Generates the UI's surfaces: flat, white, antialiased rounded rectangles and a circle.
    //
    // Why generate instead of using a kit: every ready-made kit bakes its look into the pixels —
    // a border line, a gloss highlight, a drop shadow along the bottom edge. That shadow is what
    // made the modals look like they were floating on a smudge, and no amount of tinting removes
    // something that is already painted into the sprite.
    //
    // These are pure white shapes, so colour comes entirely from the palette at runtime
    // (Image.color), and there is no fake 3D to fight. The 9-slice border is written to match the
    // corner radius, so one sprite serves a 120px button and a 900px modal without distortion.
    public static class UiShapeGenerator
    {
        private const string OutputFolder = "Assets/_Game/Art/UI/Shapes";

        [MenuItem("IdleGymBro/Generate UI Shapes")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            // Side = 2*radius + a few pixels of stretchable middle. Keeping the source small keeps
            // the corners crisp: 9-slice never scales the corner regions.
            WriteRoundedRect("panel", 96, 96, 28f);
            WriteRoundedRect("panel_soft", 72, 72, 16f);
            WriteCircle("circle", 160);

            AssetDatabase.SaveAssets();
            Debug.Log("[UiShapeGenerator] 3 UI shapes generated.");
        }

        private static void WriteRoundedRect(string fileName, int width, int height, float radius)
        {
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float coverage = RoundedRectCoverage(x + 0.5f, y + 0.5f, width, height, radius);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, coverage);
                }
            }

            Write(fileName, pixels, width, height, Mathf.CeilToInt(radius) + 1);
        }

        private static void WriteCircle(string fileName, int size)
        {
            var pixels = new Color[size * size];
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Coverage(r - d));
                }
            }

            Write(fileName, pixels, size, size, 0);
        }

        // Signed distance to a rounded rectangle, converted to antialiased coverage.
        private static float RoundedRectCoverage(float px, float py, int width, int height, float radius)
        {
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            float dx = Mathf.Abs(px - halfW) - (halfW - radius);
            float dy = Mathf.Abs(py - halfH) - (halfH - radius);

            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);

            return Coverage(radius - (outside + inside));
        }

        // One pixel of feather across the edge — enough to read as smooth at any scale, narrow
        // enough that the shape never looks blurred.
        private static float Coverage(float signedDistance)
        {
            return Mathf.Clamp01(signedDistance + 0.5f);
        }

        private static void Write(string fileName, Color[] pixels, int width, int height, int border)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string path = $"{OutputFolder}/{fileName}.png";
            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError($"[UiShapeGenerator] No TextureImporter for {path}.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border > 0 ? new Vector4(border, border, border, border) : Vector4.zero;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
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

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
