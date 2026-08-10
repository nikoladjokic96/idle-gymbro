using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGymBro.EditorTools
{
    // Isolates a single cosmetic layer (hair, beard, shorts) out of a generated character image.
    //
    // Why this exists: the wardrobe stacks each cosmetic as its own transparent sprite over the
    // body, so every layer must be pixel-aligned with the body it sits on. Image models cannot
    // reliably draw "just the hair, floating, in the exact place it would sit". What they CAN do
    // is EDIT a reference image and leave the composition intact. So we generate
    //   base    = the character with no hair
    //   variant = the SAME image edited to add hair
    // and recover the layer as the pixels that changed. Alignment is correct by construction.
    //
    // The region band exists because an edit also nudges nearby detail (the face shifts slightly
    // when hair is added); restricting the diff to the band the garment occupies keeps that noise
    // out of the layer.
    public static class CosmeticLayerExtractor
    {
        // A pixel counts as "changed" when its colour moves this far (0-1 per channel, summed RGB)
        // or its alpha moves this much. Low enough to catch soft painted edges, high enough to
        // ignore recompression noise.
        // Tuned against the hair pass: at 0.18 the edit's faint re-draw of the face (eyebrows, the
        // line of the smile) survived as a ghost inside the hair layer. 0.30 keeps the garment,
        // which differs strongly from bare skin, and drops incidental repainting.
        private const float ColorThreshold = 0.30f;
        private const float AlphaThreshold = 0.35f;

        // Feather the recovered mask so the layer's edge blends instead of showing a hard cut.
        private const int FeatherRadius = 2;

        public static void Extract(string basePath, string variantPath, string outputPath, float bandMinY, float bandMaxY)
        {
            Texture2D baseTex = Load(basePath);
            Texture2D varTex = Load(variantPath);

            if (baseTex == null || varTex == null)
            {
                Debug.LogError($"[CosmeticLayerExtractor] Could not load '{basePath}' or '{variantPath}'.");
                return;
            }

            if (baseTex.width != varTex.width || baseTex.height != varTex.height)
            {
                Debug.LogError($"[CosmeticLayerExtractor] Size mismatch: base {baseTex.width}x{baseTex.height} vs variant {varTex.width}x{varTex.height}. " +
                               "Both must come from the same aspect ratio so the layer lines up.");
                return;
            }

            int w = baseTex.width;
            int h = baseTex.height;
            Color[] basePixels = baseTex.GetPixels();
            Color[] varPixels = varTex.GetPixels();

            // Texture rows run bottom-up; the band is expressed top-down because that is how a
            // person describes "the top quarter of the character".
            int yTop = Mathf.Clamp(Mathf.RoundToInt((1f - bandMinY) * h), 0, h);
            int yBottom = Mathf.Clamp(Mathf.RoundToInt((1f - bandMaxY) * h), 0, h);

            var mask = new float[w * h];
            int changed = 0;

            for (int y = yBottom; y < yTop; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color b = basePixels[i];
                    Color v = varPixels[i];

                    float colorDelta = Mathf.Abs(b.r - v.r) + Mathf.Abs(b.g - v.g) + Mathf.Abs(b.b - v.b);
                    float alphaDelta = Mathf.Abs(b.a - v.a);

                    // Only keep pixels the variant actually paints — a pixel that went transparent
                    // is something the edit REMOVED and belongs to the body, not to this layer.
                    if (v.a > 0.1f && (colorDelta > ColorThreshold || alphaDelta > AlphaThreshold))
                    {
                        mask[i] = 1f;
                        changed++;
                    }
                }
            }

            if (changed == 0)
            {
                Debug.LogError($"[CosmeticLayerExtractor] No differing pixels in band [{bandMinY:0.00}..{bandMaxY:0.00}] for '{outputPath}'. " +
                               "The edit probably changed nothing, or the band is wrong.");
                return;
            }

            mask = Feather(mask, w, h);

            var outPixels = new Color[w * h];
            int kept = 0;

            for (int i = 0; i < outPixels.Length; i++)
            {
                Color c = varPixels[i];
                float a = c.a * mask[i];

                // Fully clear (not just alpha-0) outside the mask: leaving the source RGB behind
                // bloats the PNG and makes every preview look like the whole character survived.
                if (a <= 0.002f)
                {
                    outPixels[i] = Color.clear;
                    continue;
                }

                outPixels[i] = new Color(c.r, c.g, c.b, a);
                kept++;
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels(outPixels);
            outTex.Apply();

            byte[] png = outTex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(outTex);
            UnityEngine.Object.DestroyImmediate(baseTex);
            UnityEngine.Object.DestroyImmediate(varTex);

            string absolute = outputPath.StartsWith("Assets/")
                ? Path.Combine(Application.dataPath, outputPath.Substring("Assets/".Length))
                : outputPath;

            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? ".");
            File.WriteAllBytes(absolute, png);

            Debug.Log($"[CosmeticLayerExtractor] {Path.GetFileName(outputPath)}: {changed} changed px in band " +
                      $"[{bandMinY:0.00}..{bandMaxY:0.00}], {kept} px kept after feather -> {png.Length} bytes.");
        }

        // Box-blurs the binary mask so the cut edge is soft rather than stair-stepped.
        private static float[] Feather(float[] mask, int w, int h)
        {
            var result = new float[mask.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    int n = 0;

                    for (int dy = -FeatherRadius; dy <= FeatherRadius; dy++)
                    {
                        int sy = y + dy;
                        if (sy < 0 || sy >= h) { continue; }

                        for (int dx = -FeatherRadius; dx <= FeatherRadius; dx++)
                        {
                            int sx = x + dx;
                            if (sx < 0 || sx >= w) { continue; }

                            sum += mask[sy * w + sx];
                            n++;
                        }
                    }

                    result[y * w + x] = n > 0 ? sum / n : 0f;
                }
            }

            return result;
        }

        // Loads a PNG off disk into a fresh readable texture (bypasses the asset importer, so the
        // source does not need Read/Write enabled and may live outside the project).
        private static Texture2D Load(string path)
        {
            string absolute = path.StartsWith("Assets/")
                ? Path.Combine(Application.dataPath, path.Substring("Assets/".Length))
                : path;

            if (!File.Exists(absolute))
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(File.ReadAllBytes(absolute)) ? tex : null;
        }

        // Driven by an args file so the shell pipeline can queue many extractions in one Unity run:
        // each line is  <basePng>|<variantPng>|<outputPng>|<bandMinY>|<bandMaxY>
        public static void RunFromArgsFile()
        {
            string listPath = Environment.GetEnvironmentVariable("IGB_EXTRACT_LIST");

            if (string.IsNullOrEmpty(listPath) || !File.Exists(listPath))
            {
                Debug.LogError("[CosmeticLayerExtractor] Set IGB_EXTRACT_LIST to a readable job file.");
                return;
            }

            int ok = 0;
            foreach (string raw in File.ReadAllLines(listPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) { continue; }

                string[] parts = line.Split('|');
                if (parts.Length != 5)
                {
                    Debug.LogError($"[CosmeticLayerExtractor] Bad job line: {line}");
                    continue;
                }

                Extract(parts[0], parts[1], parts[2],
                    float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture));
                ok++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[CosmeticLayerExtractor] Processed {ok} job(s).");
        }
    }
}
