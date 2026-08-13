using System.IO;
using UnityEditor;
using UnityEngine;
using IdleGymBro.Data;

namespace IdleGymBro.EditorTools
{
    // Lifts the dumbbells out of each workout frame into their own sprite, so they can be drawn
    // ABOVE the shorts instead of underneath them.
    //
    // The weights are painted into the body art, and the shorts are a layer on top of the body, so
    // any overlap put the shorts in front of the iron — the "dumbbells pass through the shorts" bug.
    // Shortening the shorts dodged it but cost the garment its length; this fixes the depth order
    // instead, which is what was actually wrong.
    //
    // The extracted sprite re-draws the SAME pixels in the SAME place, one layer higher. Nothing is
    // erased from the body frame: where the shorts do not cover the iron, the two copies coincide
    // exactly and the result is unchanged.
    //
    // Identifying the iron: dark, not skin-toned, AND not already that colour in the tier's static
    // pose. That last clause is what keeps the navy briefs out — they are dark in every frame, while
    // a dumbbell is dark only where it has been added on top of the body.
    public static class HeldItemExtractor
    {
        private const string ArtFolder = "Assets/_Game/Art/Character/Placeholders";
        private const int TierCount = 6;
        private const int MaxFrames = 8;

        [MenuItem("IdleGymBro/Extract Held Items")]
        public static void ExtractMenu()
        {
            Debug.Log($"[HeldItemExtractor] {Extract()} held-item sprites written.");
        }

        public static int Extract()
        {
            int written = 0;

            for (int tier = 1; tier <= TierCount; tier++)
            {
                Texture2D basePose = Load($"{ArtFolder}/body_tier{tier}.png");

                if (basePose == null)
                {
                    continue;
                }

                Color[] baseline = basePose.GetPixels();
                int w = basePose.width;
                int h = basePose.height;

                // The briefs are excluded by SHAPE, not by a bounding box.
                //
                // Briefs and dumbbells share a palette — measured on tier 5: briefs 38,41,63 against
                // iron 46,55,76 — so no colour test separates them. "Dark here, not dark in the
                // static pose" almost works, except the model redraws the waistband a pixel or two
                // between frames, and that fringe came out as a shorts-sized slab drawn over the
                // shorts. A bounding box around the briefs fixed that but swallowed the dumbbells
                // resting against the hips at the bottom of a curl. Dilating the static briefs mask
                // kills the fringe and nothing else.
                bool[] brief = DilatedDarkMask(baseline, w, h, 3);
                Object.DestroyImmediate(basePose);

                for (int f = 1; f <= MaxFrames; f++)
                {
                    string framePath = $"{ArtFolder}/body_tier{tier}_work{f}.png";
                    Texture2D frame = Load(framePath);

                    if (frame == null)
                    {
                        continue;
                    }

                    if (frame.width != w || frame.height != h)
                    {
                        Object.DestroyImmediate(frame);
                        continue;
                    }

                    Color[] px = frame.GetPixels();
                    Object.DestroyImmediate(frame);

                    var held = new Color[w * h];
                    int kept = 0;

                    for (int i = 0; i < px.Length; i++)
                    {
                        if (!IsIron(px[i]) || IsIron(baseline[i]))
                        {
                            continue;
                        }

                        if (brief[i])
                        {
                            continue; // the waistband's redraw fringe, not iron
                        }

                        held[i] = px[i];
                        kept++;
                    }

                    // A frame where the arms happen not to hold anything visible simply gets an
                    // empty sprite; the animator clears the layer for it either way.
                    Write($"{ArtFolder}/body_tier{tier}_work{f}_held.png", held, w, h);
                    written++;

                    if (kept == 0)
                    {
                        Debug.LogWarning($"[HeldItemExtractor] tier {tier} frame {f}: no iron found.");
                    }
                }
            }

            AssetDatabase.Refresh();
            return written;
        }

        // Dark and colour-neutral-to-cool: the steel of a dumbbell. Skin is bright and warm
        // (r clearly above b), so the two do not overlap.
        private static bool IsIron(Color c)
        {
            return c.a > 0.5f && c.r < 0.42f && c.b >= c.r - 0.02f;
        }

        // Every dark pixel of the static pose (the briefs, and the outline), grown by `radius`.
        // Only the grown fringe matters — the pixels themselves are already excluded by the
        // "was it dark in the static pose too" test.
        private static bool[] DilatedDarkMask(Color[] pixels, int w, int h, int radius)
        {
            var seed = new bool[w * h];

            for (int i = 0; i < pixels.Length; i++)
            {
                seed[i] = IsIron(pixels[i]);
            }

            var grown = new bool[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!seed[y * w + x])
                    {
                        continue;
                    }

                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int sy = y + dy;
                        if (sy < 0 || sy >= h) { continue; }

                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int sx = x + dx;
                            if (sx < 0 || sx >= w) { continue; }

                            grown[sy * w + sx] = true;
                        }
                    }
                }
            }

            return grown;
        }

        private static void Write(string path, Color[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static Texture2D Load(string path)
        {
            string absolute = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));

            if (!File.Exists(absolute))
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(File.ReadAllBytes(absolute)) ? tex : null;
        }
    }
}
