using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace IdleGymBro.EditorTools
{
    // Turns the project's own 5x7 bitmap font into a real TMP font asset, so the HUD renders in the
    // same pixel grid as the art instead of in LiberationSans.
    //
    // Why build one instead of importing a pixel TTF: the glyphs already exist in-repo (PixelFont,
    // used for the baked placeholder labels), so there is no third-party font to license, ship or
    // keep in sync — and a bitmap atlas is exactly what a pixel font wants. A TTF would be rasterised
    // by TMP into an SDF and come back softened, which is the look this is replacing.
    //
    // Rendered at 1x into the atlas and displayed at integer multiples of the glyph height: any
    // other size lands glyph pixels on fractional screen pixels and the text shimmers.
    public static class PixelFontAssetGenerator
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;
        public const int Advance = GlyphWidth + 1; // one blank column between characters

        // Every text in the UI snaps to a multiple of this, so a glyph pixel is always a whole
        // number of screen pixels. 7 would be crisp but unreadably small on a phone.
        public const int PointSize = GlyphHeight;

        private const int Padding = 1;
        private const string FontFolder = "Assets/_Game/Art/UI/Font";
        private const string AtlasPath = FontFolder + "/pixel_font_atlas.png";
        private const string AssetPath = FontFolder + "/PixelFont.asset";
        private const string MaterialPath = FontFolder + "/PixelFont Material.mat";

        [MenuItem("IdleGymBro/Generate Pixel Font")]
        public static void GenerateMenu()
        {
            Debug.Log($"[PixelFontAssetGenerator] {Generate()} glyphs in the font asset.");
        }

        public static int Generate()
        {
            EnsureFolder();

            var chars = new List<char>(PixelFont.Glyphs.Keys);
            chars.Sort();

            int cellW = GlyphWidth + Padding * 2;
            int cellH = GlyphHeight + Padding * 2;
            int columns = 16;
            int rows = Mathf.CeilToInt(chars.Count / (float)columns);
            int atlasW = Mathf.NextPowerOfTwo(columns * cellW);
            int atlasH = Mathf.NextPowerOfTwo(rows * cellH);

            var pixels = new Color32[atlasW * atlasH];
            var glyphs = new List<Glyph>(chars.Count);
            var characters = new List<TMP_Character>(chars.Count);

            for (int i = 0; i < chars.Count; i++)
            {
                char c = chars[i];
                byte[] rowBits = PixelFont.Glyphs[c];

                int col = i % columns;
                int row = i / columns;
                int originX = col * cellW + Padding;

                // Atlas rows run bottom-up; lay the grid out from the top so the sheet reads in
                // character order when opened.
                int originY = atlasH - (row + 1) * cellH + Padding;

                for (int gy = 0; gy < GlyphHeight; gy++)
                {
                    byte bits = rowBits[gy];
                    int y = originY + (GlyphHeight - 1 - gy); // bit row 0 is the glyph's TOP

                    for (int gx = 0; gx < GlyphWidth; gx++)
                    {
                        if (((bits >> (GlyphWidth - 1 - gx)) & 1) == 0)
                        {
                            continue;
                        }

                        int x = originX + gx;
                        pixels[y * atlasW + x] = new Color32(255, 255, 255, 255);
                    }
                }

                var metrics = new GlyphMetrics(
                    GlyphWidth,
                    GlyphHeight,
                    0f,             // bearingX
                    GlyphHeight,    // bearingY: the glyph sits entirely above the baseline
                    Advance);

                var rect = new GlyphRect(originX, originY, GlyphWidth, GlyphHeight);
                var glyph = new Glyph((uint)i, metrics, rect, 1f, 0);

                glyphs.Add(glyph);
                characters.Add(new TMP_Character(c, glyph));
            }

            var atlas = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, false);
            atlas.SetPixels32(pixels);
            atlas.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, AtlasPath.Substring("Assets/".Length)), atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureAtlasImporter();

            var importedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

            if (importedAtlas == null)
            {
                Debug.LogError("[PixelFontAssetGenerator] Atlas failed to import; font not built.");
                return 0;
            }

            BuildFontAsset(importedAtlas, glyphs, characters, atlasW, atlasH);
            AssetDatabase.SaveAssets();
            return characters.Count;
        }

        private static void BuildFontAsset(Texture2D atlas, List<Glyph> glyphs, List<TMP_Character> characters, int atlasW, int atlasH)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            bool isNew = font == null;

            if (isNew)
            {
                font = ScriptableObject.CreateInstance<TMP_FontAsset>();
                AssetDatabase.CreateAsset(font, AssetPath);
            }

            font.faceInfo = new FaceInfo
            {
                familyName = "IdleGymBro Pixel",
                styleName = "Regular",
                pointSize = PointSize,
                scale = 1f,
                lineHeight = GlyphHeight + 2,
                ascentLine = GlyphHeight,
                capLine = GlyphHeight,
                meanLine = GlyphHeight * 0.6f,
                baseline = 0f,
                descentLine = 0f,
                superscriptOffset = GlyphHeight,
                superscriptSize = 0.5f,
                subscriptOffset = -GlyphHeight * 0.25f,
                subscriptSize = 0.5f,
                underlineOffset = -1f,
                underlineThickness = 1f,
                strikethroughOffset = GlyphHeight * 0.4f,
                strikethroughThickness = 1f,
                tabWidth = Advance * 4,
            };

            font.atlasTextures = new[] { atlas };
            font.atlasPopulationMode = AtlasPopulationMode.Static; // never try to rasterise a TTF

            // atlasWidth/Height/Padding/RenderMode are read-only properties — only the font-asset
            // creator window writes them. Reach the backing fields directly.
            //
            // NOT via SerializedObject: `new SerializedObject(font)` snapshots every serialized
            // field at construction, so ApplyModifiedProperties writes that whole snapshot back and
            // silently reverted the glyph and character tables set around it. The asset then saved
            // with one character and a 0x0 atlas — which renders as a completely blank HUD without
            // a single warning.
            SetPrivateField(font, "m_AtlasWidth", atlasW);
            SetPrivateField(font, "m_AtlasHeight", atlasH);
            SetPrivateField(font, "m_AtlasPadding", Padding);
            SetPrivateField(font, "m_AtlasRenderMode", GlyphRenderMode.RASTER);

            font.glyphTable.Clear();
            font.glyphTable.AddRange(glyphs);
            font.characterTable.Clear();
            font.characterTable.AddRange(characters);

            // The Bitmap shader samples the atlas directly. The SDF shaders expect a distance field
            // and would render this as a smear.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material == null)
            {
                material = new Material(Shader.Find("TextMeshPro/Bitmap"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = Shader.Find("TextMeshPro/Bitmap");
            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            font.material = material;

            // Rebuilds the lookup dictionaries from the tables written above; without it the font
            // reports zero characters at runtime and every string renders empty.
            font.ReadFontAssetDefinition();

            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            // Read the asset back the way the game will: a font that is correct in memory and wrong
            // on disk looks identical from here, and the whole HUD goes blank at runtime.
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
            var reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);

            if (reloaded == null || reloaded.characterTable.Count != characters.Count || reloaded.atlasWidth != atlasW)
            {
                Debug.LogError($"[PixelFontAssetGenerator] Font did not persist: on disk it has " +
                               $"{(reloaded == null ? 0 : reloaded.characterTable.Count)} characters and a " +
                               $"{(reloaded == null ? 0 : reloaded.atlasWidth)}px atlas, expected {characters.Count} and {atlasW}.");
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (field == null)
            {
                Debug.LogError($"[PixelFontAssetGenerator] TMP_FontAsset has no field '{fieldName}' in this TMP version.");
                return;
            }

            field.SetValue(target, value);
        }

        private static void ConfigureAtlasImporter()
        {
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;

            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;   // the whole point: no smoothing
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Art/UI"))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Art", "UI");
            }

            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Art/UI", "Font");
            }
        }
    }
}
