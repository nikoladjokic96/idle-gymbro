using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGymBro.EditorTools
{
    // Cuts the two forearms out of a body sprite so they can be ROTATED at runtime instead of
    // swapped frame by frame.
    //
    // Frame-swapping a limb that travels a long way looks stepped no matter how many frames are
    // drawn, and cross-fading it draws both positions at once. A curl is really just the forearm
    // rotating about a fixed elbow, so one rotating sprite is smooth at any frame rate and needs
    // no animation art at all.
    //
    // The forearms cost nothing to produce: they are exactly the difference between the normal
    // body and the same body drawn with the arms ending at the elbow. Both come from the same
    // generation pass, so the cut-out is pixel-aligned with the body by construction.
    //
    // Outputs, per tier:
    //   <body>_forearm_l.png / _forearm_r.png  — full-canvas sprites, so they need no repositioning
    //   the elbow pivot for each, logged and written next to the art as a .pivot.txt
    public static class ForearmExtractor
    {
        private const float AlphaThreshold = 0.35f;
        private const int FeatherRadius = 1;

        // The elbow is the top of the forearm blob. Sampling a band of rows rather than the single
        // topmost pixel keeps one stray antialiased pixel from throwing the pivot off.
        private const int ElbowSampleRows = 12;

        // Derives BOTH pieces of the rig from two poses of the same body — no new art, no image
        // model, no network. Given the arms-down body and the same body with the forearms raised:
        //
        //   opaque in BOTH  = everything except the forearms (they occupy different places in the
        //                     two poses) -> the armless body
        //   opaque in DOWN pose only = the forearm in its resting position -> the sprite to rotate
        //
        // This works because every workout frame was authored with the head, torso, hips and legs
        // pinned pixel-identical; the forearms are the only thing that moves between them.
        public static void ExtractFromPoses(string bodyPath, string raisedPath, string outputPrefix, float bandMinY, float bandMaxY)
        {
            Texture2D body = Load(bodyPath);
            Texture2D raised = Load(raisedPath);

            if (body == null || raised == null)
            {
                Debug.LogError($"[ForearmExtractor] Could not load '{bodyPath}' or '{raisedPath}'.");
                return;
            }

            if (body.width != raised.width || body.height != raised.height)
            {
                Debug.LogError($"[ForearmExtractor] Size mismatch between the two poses.");
                return;
            }

            int w = body.width;
            int h = body.height;
            Color[] bodyPixels = body.GetPixels();
            Color[] raisedPixels = raised.GetPixels();

            int yTop = Mathf.Clamp(Mathf.RoundToInt((1f - bandMinY) * h), 0, h);
            int yBottom = Mathf.Clamp(Mathf.RoundToInt((1f - bandMaxY) * h), 0, h);

            var armlessMask = new float[w * h];
            var forearmMask = new float[w * h];
            int armlessCount = 0;
            int forearmCount = 0;

            for (int i = 0; i < armlessMask.Length; i++)
            {
                bool inBody = bodyPixels[i].a > AlphaThreshold;
                bool inRaised = raisedPixels[i].a > AlphaThreshold;

                if (inBody && inRaised)
                {
                    armlessMask[i] = 1f;
                    armlessCount++;
                    continue;
                }

                // Only the resting-pose forearm, and only inside the arm band, so an incidental
                // repaint elsewhere in the frame cannot leak into the cut-out or drag the elbow.
                int y = i / w;

                if (inBody && !inRaised && y >= yBottom && y < yTop)
                {
                    forearmMask[i] = 1f;
                    forearmCount++;
                }
            }

            if (forearmCount < 200)
            {
                Debug.LogError($"[ForearmExtractor] Only {forearmCount} forearm pixels in band " +
                               $"[{bandMinY:0.00}..{bandMaxY:0.00}] — check the band or the pose pair.");
                return;
            }

            WriteMasked(bodyPixels, Feather(armlessMask, w, h), w, h, $"{outputPrefix}_armless.png");
            Debug.Log($"[ForearmExtractor] {Path.GetFileNameWithoutExtension(outputPrefix)}_armless.png: {armlessCount} px.");

            float[] feathered = Feather(forearmMask, w, h);
            WriteSide(bodyPixels, feathered, w, h, 0, w / 2, $"{outputPrefix}_forearm_l.png", "left");
            WriteSide(bodyPixels, feathered, w, h, w / 2, w, $"{outputPrefix}_forearm_r.png", "right");

            UnityEngine.Object.DestroyImmediate(body);
            UnityEngine.Object.DestroyImmediate(raised);
        }

        private static void WriteMasked(Color[] source, float[] mask, int w, int h, string outputPath)
        {
            var pixels = new Color[w * h];

            for (int i = 0; i < pixels.Length; i++)
            {
                float a = mask[i] * source[i].a;
                pixels[i] = a <= 0.004f ? Color.clear : new Color(source[i].r, source[i].g, source[i].b, a);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string absolute = ToAbsolute(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? ".");
            File.WriteAllBytes(absolute, png);
        }

        public static void Extract(string bodyPath, string noForearmPath, string outputPrefix)
        {
            Extract(bodyPath, noForearmPath, outputPrefix, 0f, 1f);
        }

        // bandMinY/bandMaxY (top-down fractions) restrict the cut-out to the rows the arm occupies.
        // Without it the diff also picks up the incidental repainting the image model does around
        // the shoulders and neck, which drags the computed elbow up to shoulder height and would
        // make the forearm swing from the wrong joint.
        public static void Extract(string bodyPath, string noForearmPath, string outputPrefix, float bandMinY, float bandMaxY)
        {
            Texture2D body = Load(bodyPath);
            Texture2D noForearm = Load(noForearmPath);

            if (body == null || noForearm == null)
            {
                Debug.LogError($"[ForearmExtractor] Could not load '{bodyPath}' or '{noForearmPath}'.");
                return;
            }

            if (body.width != noForearm.width || body.height != noForearm.height)
            {
                Debug.LogError($"[ForearmExtractor] Size mismatch: {body.width}x{body.height} vs {noForearm.width}x{noForearm.height}.");
                return;
            }

            int w = body.width;
            int h = body.height;
            Color[] bodyPixels = body.GetPixels();
            Color[] strippedPixels = noForearm.GetPixels();

            int yTop = Mathf.Clamp(Mathf.RoundToInt((1f - bandMinY) * h), 0, h);
            int yBottom = Mathf.Clamp(Mathf.RoundToInt((1f - bandMaxY) * h), 0, h);

            // A forearm pixel is one the body draws and the elbow-stump version does not.
            var mask = new float[w * h];
            int count = 0;

            for (int i = 0; i < mask.Length; i++)
            {
                int y = i / w;

                if (y < yBottom || y >= yTop)
                {
                    continue;
                }

                if (bodyPixels[i].a > AlphaThreshold && strippedPixels[i].a < AlphaThreshold)
                {
                    mask[i] = 1f;
                    count++;
                }
            }

            if (count < 200)
            {
                Debug.LogError($"[ForearmExtractor] Only {count} differing pixels — the stump image probably still has forearms.");
                return;
            }

            mask = Feather(mask, w, h);

            WriteSide(bodyPixels, mask, w, h, 0, w / 2, $"{outputPrefix}_forearm_l.png", "left");
            WriteSide(bodyPixels, mask, w, h, w / 2, w, $"{outputPrefix}_forearm_r.png", "right");

            UnityEngine.Object.DestroyImmediate(body);
            UnityEngine.Object.DestroyImmediate(noForearm);
        }

        private static void WriteSide(Color[] source, float[] mask, int w, int h, int xMin, int xMax, string outputPath, string label)
        {
            var pixels = new Color[w * h];
            int kept = 0;
            int topY = -1;
            long elbowXSum = 0;
            int elbowSamples = 0;

            // Rows run bottom-up, so the elbow is the HIGHEST y that still has forearm in it.
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    int i = y * w + x;
                    float a = mask[i] * source[i].a;

                    if (a <= 0.004f)
                    {
                        continue;
                    }

                    pixels[i] = new Color(source[i].r, source[i].g, source[i].b, a);
                    kept++;

                    if (topY < 0)
                    {
                        topY = y;
                    }

                    if (topY - y < ElbowSampleRows)
                    {
                        elbowXSum += x;
                        elbowSamples++;
                    }
                }
            }

            if (kept == 0 || elbowSamples == 0)
            {
                Debug.LogError($"[ForearmExtractor] {label}: no forearm pixels found in x[{xMin}..{xMax}].");
                return;
            }

            float elbowX = (float)elbowXSum / elbowSamples;
            float pivotX = elbowX / w;
            float pivotY = (float)topY / h;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            string absolute = ToAbsolute(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? ".");
            File.WriteAllBytes(absolute, png);

            // The pivot travels with the art as plain text so the scene bootstrap can read it back
            // without re-deriving it from pixels on every build.
            File.WriteAllText(
                Path.ChangeExtension(absolute, ".pivot.txt"),
                $"{pivotX.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                $"{pivotY.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            Debug.Log($"[ForearmExtractor] {Path.GetFileName(outputPath)}: {kept} px, " +
                      $"elbow pivot ({pivotX:0.0000}, {pivotY:0.0000}) -> {png.Length} bytes.");
        }

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

        private static string ToAbsolute(string path)
        {
            return path.StartsWith("Assets/")
                ? Path.Combine(Application.dataPath, path.Substring("Assets/".Length))
                : path;
        }

        private static Texture2D Load(string path)
        {
            string absolute = ToAbsolute(path);

            if (!File.Exists(absolute))
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(File.ReadAllBytes(absolute)) ? tex : null;
        }

        // Job file, one line each:  <bodyPng>|<noForearmPng>|<outputPrefix>
        public static void RunFromArgsFile()
        {
            string listPath = Environment.GetEnvironmentVariable("IGB_FOREARM_LIST");

            if (string.IsNullOrEmpty(listPath) || !File.Exists(listPath))
            {
                Debug.LogError("[ForearmExtractor] Set IGB_FOREARM_LIST to a readable job file.");
                return;
            }

            int ok = 0;

            foreach (string raw in File.ReadAllLines(listPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) { continue; }

                string[] parts = line.Split('|');

                if (parts.Length == 5)
                {
                    ExtractFromPoses(parts[0], parts[1], parts[2],
                        float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture));
                    ok++;
                    continue;
                }

                if (parts.Length != 3)
                {
                    Debug.LogError($"[ForearmExtractor] Bad job line: {line}");
                    continue;
                }

                Extract(parts[0], parts[1], parts[2]);
                ok++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[ForearmExtractor] Processed {ok} job(s).");
        }
    }
}
