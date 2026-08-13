using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGymBro.EditorTools
{
    // Cuts a pair of shorts to fit EACH muscle tier, by tracing that tier's own hips.
    //
    // The shorts used to be one sprite for all six tiers, downscaled from the old painted art. The
    // painted tiers shared a composition, so one cut was close enough; the pixel-art tiers do not —
    // tier 6's hips are far wider than tier 1's — and the single pair visibly hung off the skinny
    // character's sides.
    //
    // Generating them is better than drawing six by hand: the garment is derived from the body it
    // has to cover, so it fits by construction and keeps fitting if the bodies are ever regenerated.
    //
    // Two measurements make it work:
    //   * the waistline is found from the body's navy briefs, not guessed from a fraction of the
    //     canvas — every tier draws them, and they mark exactly where a garment should sit;
    //   * per row, the hips are the opaque run that CONTAINS THE CENTRE COLUMN. At hip height the
    //     silhouette is arm | gap | hips | gap | arm, so taking the whole row's extent would stretch
    //     the shorts across both arms.
    public static class ShortsGenerator
    {
        private const string ArtFolder = "Assets/_Game/Art/Character/Placeholders";
        private const int TierCount = 6;

        // How far below the briefs the leg openings sit, as a fraction of canvas height.
        private const float HemDropFraction = 0.11f;

        private struct Style
        {
            public string Id;
            public Color Base;
            public Color Shade;
            public Color Band;
            public Color Stripe;
        }

        private static readonly Style[] Styles =
        {
            new Style { Id = "shorts_01", Base = new Color32(0xB0, 0x2A, 0x33, 0xFF), Shade = new Color32(0x76, 0x18, 0x22, 0xFF), Band = new Color32(0x5E, 0x12, 0x1B, 0xFF), Stripe = new Color32(0xE8, 0xD8, 0xC0, 0xFF) },
            new Style { Id = "shorts_02", Base = new Color32(0x3A, 0x40, 0x4E, 0xFF), Shade = new Color32(0x24, 0x29, 0x33, 0xFF), Band = new Color32(0x1A, 0x1E, 0x26, 0xFF), Stripe = new Color32(0x8A, 0x93, 0xA5, 0xFF) },
            new Style { Id = "shorts_03", Base = new Color32(0x2C, 0x5F, 0xA8, 0xFF), Shade = new Color32(0x1C, 0x3F, 0x74, 0xFF), Band = new Color32(0x14, 0x2E, 0x57, 0xFF), Stripe = new Color32(0xD8, 0xE4, 0xF2, 0xFF) },
        };

        [MenuItem("IdleGymBro/Generate Shorts Per Tier")]
        public static void GenerateAll()
        {
            Debug.Log($"[ShortsGenerator] {Generate()} shorts sprites written.");
        }

        public static int Generate()
        {
            int written = 0;

            for (int tier = 1; tier <= TierCount; tier++)
            {
                string bodyPath = $"{ArtFolder}/body_tier{tier}.png";
                Texture2D body = Load(bodyPath);

                if (body == null)
                {
                    Debug.LogWarning($"[ShortsGenerator] Missing {bodyPath}; tier {tier} skipped.");
                    continue;
                }

                if (!TryFindWaist(body, out int waistTop, out int briefsBottom))
                {
                    Debug.LogWarning($"[ShortsGenerator] tier {tier}: could not locate the briefs; skipped.");
                    Object.DestroyImmediate(body);
                    continue;
                }

                // Full length again. The shorts used to be cut short to stay clear of the dumbbells,
                // because the weights are painted into the body and the shorts are a layer above it.
                // HeldItemExtractor now re-draws the iron ABOVE the shorts instead, so the garment no
                // longer has to dodge it and can be an actual pair of shorts.
                int hem = Mathf.Max(0, briefsBottom - Mathf.RoundToInt(body.height * HemDropFraction));

                foreach (Style style in Styles)
                {
                    Color[] pixels = Paint(body, waistTop, briefsBottom, hem, style);
                    string path = $"{ArtFolder}/{style.Id}_tier{tier}.png";
                    Write(path, pixels, body.width, body.height);
                    written++;
                }

                Object.DestroyImmediate(body);
            }

            AssetDatabase.Refresh();
            return written;
        }

        // The briefs are the only navy FILL on an otherwise skin-toned sprite. Measured from the
        // art: fill 62,68,93 with shades 38,41,63 and 26,28,42 and a highlight 82,87,109.
        //
        // Two things this has to get right, both learned the hard way:
        //   * the darkest navy (26,28,42) is also the colour of the outline drawn around the WHOLE
        //     body, so a loose "blue-ish and dark" test matches every row from scalp to toes and the
        //     shorts end up covering the entire character;
        //   * hence a RUN of at least MinBriefsRun matching pixels is required — a garment is a
        //     filled area, a one-pixel outline is not.
        private const int MinBriefsRun = 5;

        private static bool IsBriefs(Color c)
        {
            return c.a > 0.5f
                && c.b - c.r > 0.09f
                && c.b - c.g > 0.05f
                && c.b > 0.20f
                && c.b < 0.60f;
        }

        private static bool TryFindWaist(Texture2D tex, out int waistTop, out int briefsBottom)
        {
            waistTop = 0;
            briefsBottom = 0;

            int w = tex.width;
            int h = tex.height;
            Color[] pixels = tex.GetPixels();

            int top = -1;
            int bottom = -1;

            for (int y = 0; y < h; y++)
            {
                int run = 0;
                bool rowHasBriefs = false;

                for (int x = 0; x < w && !rowHasBriefs; x++)
                {
                    run = IsBriefs(pixels[y * w + x]) ? run + 1 : 0;
                    rowHasBriefs = run >= MinBriefsRun;
                }

                if (!rowHasBriefs)
                {
                    continue;
                }

                if (bottom < 0 || y < bottom) { bottom = y; }
                if (y > top) { top = y; }
            }

            if (top < 0 || top - bottom < 3)
            {
                return false;
            }

            waistTop = top;        // rows run bottom-up: the highest row is the waistband
            briefsBottom = bottom;
            return true;
        }

        private static Color[] Paint(Texture2D body, int waistTop, int briefsBottom, int hem, Style style)
        {
            int w = body.width;
            int h = body.height;
            Color[] src = body.GetPixels();
            var outPixels = new Color[w * h];
            int bandRows = 3;

            // The waistband's own span defines what counts as "the body" further down. Runs that do
            // not overlap it are arms, which at hip height sit right beside the hips.
            if (!TryRuns(src, w, waistTop, out List<(int L, int R)> waistRuns) || waistRuns.Count == 0)
            {
                return outPixels;
            }

            (int L, int R) hips = Widest(waistRuns);

            // Average brightness of the body the garment will cover, so shading is relative to this
            // tier's own skin tone instead of an absolute guess.
            float meanLuma = MeanLuma(src, w, hem, waistTop, hips.L, hips.R);

            for (int y = hem; y <= waistTop; y++)
            {
                if (!TryRuns(src, w, y, out List<(int L, int R)> runs))
                {
                    continue;
                }

                // Every run overlapping the hips is painted — ONE above the crotch, TWO once the
                // legs separate. The previous version took the single run nearest the centre, so
                // below the crotch it clothed whichever leg it happened to find first and left the
                // other one bare: the "one trouser leg longer than the other" bug.
                foreach ((int L, int R) run in runs)
                {
                    if (run.R < hips.L || run.L > hips.R || run.R - run.L < 3)
                    {
                        continue;
                    }

                    int xL = Mathf.Max(run.L, hips.L - 1);
                    int xR = Mathf.Min(run.R, hips.R + 1);

                    for (int x = xL; x <= xR; x++)
                    {
                        Color c = style.Base;

                        if (y > waistTop - bandRows)
                        {
                            c = style.Band;                   // waistband
                        }
                        else if (x == xL || x == xR || y == hem)
                        {
                            c = style.Shade;                  // outline hugging the silhouette
                        }
                        else if ((x == xL + 2 || x == xR - 2) && y < waistTop - bandRows - 1)
                        {
                            c = style.Stripe;                 // side stripe
                        }
                        else
                        {
                            // Take the LIGHTING from the body underneath. A flat fill reads as a
                            // sticker laid over the sprite; borrowing the leg's own light and shade
                            // makes the cloth wrap the same volume the pixel art already describes.
                            c = Shade(c, src[y * w + x], meanLuma);
                        }

                        outPixels[y * w + x] = c;
                    }
                }
            }

            return outPixels;
        }

        private static float Luma(Color c)
        {
            return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        }

        private static float MeanLuma(Color[] src, int w, int yFrom, int yTo, int xFrom, int xTo)
        {
            float sum = 0f;
            int n = 0;

            for (int y = yFrom; y <= yTo; y++)
            {
                for (int x = xFrom; x <= xTo; x++)
                {
                    Color c = src[y * w + x];

                    if (c.a > 0.5f)
                    {
                        sum += Luma(c);
                        n++;
                    }
                }
            }

            return n > 0 ? sum / n : 0.5f;
        }

        // Pushes the garment colour towards its own light/dark by however far the body pixel under
        // it sits from the average. Quantised to three steps: smooth shading on a 96x144 sprite
        // would just be noise, and pixel art wants bands.
        private static Color Shade(Color garment, Color body, float meanLuma)
        {
            if (body.a <= 0.5f)
            {
                return garment;
            }

            float delta = Luma(body) - meanLuma;
            float step = delta > 0.07f ? 0.16f : delta < -0.07f ? -0.20f : 0f;

            if (step == 0f)
            {
                return garment;
            }

            return new Color(
                Mathf.Clamp01(garment.r + step * 0.9f),
                Mathf.Clamp01(garment.g + step * 0.9f),
                Mathf.Clamp01(garment.b + step * 0.9f),
                1f);
        }

        // All opaque horizontal runs in a row.
        private static bool TryRuns(Color[] pixels, int w, int y, out List<(int L, int R)> runs)
        {
            runs = new List<(int L, int R)>();
            int start = -1;

            for (int x = 0; x < w; x++)
            {
                bool opaque = pixels[y * w + x].a > 0.5f;

                if (opaque && start < 0)
                {
                    start = x;
                }
                else if (!opaque && start >= 0)
                {
                    runs.Add((start, x - 1));
                    start = -1;
                }
            }

            if (start >= 0)
            {
                runs.Add((start, w - 1));
            }

            return runs.Count > 0;
        }

        private static (int L, int R) Widest(List<(int L, int R)> runs)
        {
            (int L, int R) best = runs[0];

            foreach ((int L, int R) run in runs)
            {
                if (run.R - run.L > best.R - best.L)
                {
                    best = run;
                }
            }

            return best;
        }

        private static void Write(string path, Color[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string absolute = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            File.WriteAllBytes(absolute, png);
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
