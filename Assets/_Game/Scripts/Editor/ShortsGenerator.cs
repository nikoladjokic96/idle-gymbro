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

                foreach (Style style in Styles)
                {
                    Color[] pixels = Paint(body, waistTop, briefsBottom, style);
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

        private static Color[] Paint(Texture2D body, int waistTop, int briefsBottom, Style style)
        {
            int w = body.width;
            int h = body.height;
            Color[] src = body.GetPixels();
            var outPixels = new Color[w * h];

            int hem = Mathf.Max(0, briefsBottom - Mathf.RoundToInt(h * HemDropFraction));
            int bandRows = 3;
            int centre = w / 2;

            for (int y = hem; y <= waistTop; y++)
            {
                if (!TryHipSpan(src, w, y, centre, out int xL, out int xR))
                {
                    continue;
                }

                // Legs separate over the lower half of the garment; above that it is one piece.
                int splitTop = hem + Mathf.RoundToInt((waistTop - hem) * 0.45f);
                bool split = y < splitTop;
                int gapHalf = split ? Mathf.Max(1, (xR - xL) / 14) : 0;

                for (int x = xL; x <= xR; x++)
                {
                    if (split && Mathf.Abs(x - centre) <= gapHalf)
                    {
                        continue;
                    }

                    Color c = style.Base;

                    if (y > waistTop - bandRows)
                    {
                        c = style.Band;                       // waistband
                    }
                    else if (x == xL || x == xR)
                    {
                        c = style.Shade;                      // outline hugging the silhouette
                    }
                    else if (y == hem || (split && Mathf.Abs(x - centre) <= gapHalf + 1))
                    {
                        c = style.Shade;                      // hem and inner leg edge
                    }
                    else if (x == xL + 2 && y < waistTop - bandRows - 1)
                    {
                        c = style.Stripe;                     // side stripe, left leg only
                    }
                    else if (x == xR - 2 && y < waistTop - bandRows - 1)
                    {
                        c = style.Stripe;
                    }

                    outPixels[y * w + x] = c;
                }
            }

            return outPixels;
        }

        // The opaque run that contains the centre column — i.e. the torso/hips, never the arms.
        private static bool TryHipSpan(Color[] pixels, int w, int y, int centre, out int xL, out int xR)
        {
            xL = 0;
            xR = 0;

            if (pixels[y * w + centre].a <= 0.5f)
            {
                // Centre is transparent (the legs have already split): fall back to the run nearest
                // the centre so the hem still lands on the thighs.
                int probe = -1;
                for (int d = 1; d < w / 2; d++)
                {
                    if (centre - d >= 0 && pixels[y * w + centre - d].a > 0.5f) { probe = centre - d; break; }
                    if (centre + d < w && pixels[y * w + centre + d].a > 0.5f) { probe = centre + d; break; }
                }

                if (probe < 0)
                {
                    return false;
                }

                centre = probe;
            }

            xL = centre;
            while (xL - 1 >= 0 && pixels[y * w + xL - 1].a > 0.5f) { xL--; }

            xR = centre;
            while (xR + 1 < w && pixels[y * w + xR + 1].a > 0.5f) { xR++; }

            return xR - xL >= 4;
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
