using System.IO;
using UnityEditor;
using UnityEngine;
using IdleGymBro.Data;

namespace IdleGymBro.EditorTools
{
    // Bakes, for every animation frame, how far that frame's head has moved from the head in the
    // tier's static pose — so the animator can carry the hair/beard/blink layers along with it.
    //
    // Why this is needed: the cosmetic layers are ONE sprite each, drawn on top of an animated body.
    // That worked while the body art was hand-authored to keep the skull pixel-identical between
    // frames. The generated clips do not: the head bobs with the breath and turns to watch the
    // dumbbell, so static hair visibly detaches. Authoring hair per frame is not an option — the
    // layer had to be recovered as a diff against a base pose, and that technique does not survive
    // PixelLab (see docs/pixellab-migration.md §3).
    //
    // The anchor is measured, not guessed: the topmost opaque row plus the horizontal centre of
    // mass of the rows just below it. Sampling is clamped to the middle of the canvas so a dumbbell
    // raised to shoulder height cannot drag the centroid sideways.
    public static class FrameAnchorBaker
    {
        private const string TiersFolder = "Assets/_Game/Data/MuscleTiers";

        // Fractions of the sprite, not pixel counts: the same numbers hold if the canvas changes.
        private const float HeadBandHeightFraction = 0.14f; // ~20 px of a 144 px canvas: skull, not neck
        private const float CentreBandHalfWidth = 0.22f;    // ~21 px either side of centre
        private const float AlphaFloor = 0.35f;

        [MenuItem("IdleGymBro/Bake Frame Anchors")]
        public static void BakeAll()
        {
            Debug.Log($"[FrameAnchorBaker] {Bake()} tier(s) baked.");
        }

        public static int Bake()
        {
            string[] guids = AssetDatabase.FindAssets("t:MuscleTierData", new[] { TiersFolder });
            int baked = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tier = AssetDatabase.LoadAssetAtPath<MuscleTierData>(path);

                if (tier == null || tier.BodySprite == null)
                {
                    continue;
                }

                if (!TryMeasure(tier.BodySprite, out Vector2 baseAnchor))
                {
                    Debug.LogWarning($"[FrameAnchorBaker] {tier.name}: static pose has no opaque head band; skipped.");
                    continue;
                }

                if (!TryMeasureHips(tier.BodySprite, out Vector2 baseHips))
                {
                    baseHips = Vector2.zero;
                }

                var so = new SerializedObject(tier);
                WriteOffsets(so, "_idleHeadOffsets", tier.IdleFrames, baseAnchor);
                WriteOffsets(so, "_workoutHeadOffsets", tier.WorkoutFrames, baseAnchor);
                WriteHipOffsets(so, "_idleHipOffsets", tier.IdleFrames, baseHips);
                WriteHipOffsets(so, "_workoutHipOffsets", tier.WorkoutFrames, baseHips);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(tier);
                baked++;
            }

            AssetDatabase.SaveAssets();
            return baked;
        }

        private static void WriteOffsets(SerializedObject so, string propertyName, Sprite[] frames, Vector2 baseAnchor)
        {
            SerializedProperty prop = so.FindProperty(propertyName);

            if (prop == null)
            {
                return;
            }

            int count = frames?.Length ?? 0;
            prop.arraySize = count;

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Vector2.zero;

                if (frames[i] != null && TryMeasure(frames[i], out Vector2 anchor))
                {
                    offset = anchor - baseAnchor;
                }

                prop.GetArrayElementAtIndex(i).vector2Value = offset;
            }
        }

        private static void WriteHipOffsets(SerializedObject so, string propertyName, Sprite[] frames, Vector2 baseHips)
        {
            SerializedProperty prop = so.FindProperty(propertyName);

            if (prop == null)
            {
                return;
            }

            int count = frames?.Length ?? 0;
            prop.arraySize = count;

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Vector2.zero;

                if (frames[i] != null && TryMeasureHips(frames[i], out Vector2 hips))
                {
                    offset = hips - baseHips;
                }

                prop.GetArrayElementAtIndex(i).vector2Value = offset;
            }
        }

        // Where the hips sit in this frame: the centre of the navy briefs, which every tier draws
        // and which moves with the legs. Measured the same way ShortsGenerator finds the waist — a
        // RUN of navy pixels, so the 1px body outline (the same darkest navy) cannot trigger it.
        private static bool TryMeasureHips(Sprite sprite, out Vector2 anchor)
        {
            anchor = Vector2.zero;

            if (!TryLoad(sprite, out Color[] pixels, out int w, out int h))
            {
                return false;
            }

            double sumX = 0d;
            double sumY = 0d;
            int n = 0;

            // Centre columns only. The dumbbells share the briefs palette, so a weight held out to
            // the side would drag the centroid several pixels and the shorts would lurch with it.
            int xMin = Mathf.Clamp(Mathf.RoundToInt(w * (0.5f - CentreBandHalfWidth)), 0, w - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(w * (0.5f + CentreBandHalfWidth)), 0, w - 1);

            for (int y = 0; y < h; y++)
            {
                int run = 0;

                for (int x = xMin; x <= xMax; x++)
                {
                    Color c = pixels[y * w + x];
                    bool navy = c.a > AlphaFloor && c.b - c.r > 0.09f && c.b - c.g > 0.05f && c.b > 0.20f && c.b < 0.60f;
                    run = navy ? run + 1 : 0;

                    if (run >= 5)
                    {
                        sumX += x;
                        sumY += y;
                        n++;
                    }
                }
            }

            if (n == 0)
            {
                return false;
            }

            anchor = new Vector2((float)(sumX / n), (float)(sumY / n));
            return true;
        }

        private static bool TryLoad(Sprite sprite, out Color[] pixels, out int w, out int h)
        {
            pixels = null;
            w = 0;
            h = 0;

            string path = AssetDatabase.GetAssetPath(sprite);
            string absolute = path.StartsWith("Assets/")
                ? Path.Combine(Application.dataPath, path.Substring("Assets/".Length))
                : path;

            if (!File.Exists(absolute))
            {
                return false;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            if (!tex.LoadImage(File.ReadAllBytes(absolute)))
            {
                Object.DestroyImmediate(tex);
                return false;
            }

            w = tex.width;
            h = tex.height;
            pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return true;
        }

        // Reads the PNG off disk rather than the imported Sprite: sprite textures are not
        // Read/Write enabled (and turning that on for every frame would bloat the build).
        private static bool TryMeasure(Sprite sprite, out Vector2 anchor)
        {
            anchor = Vector2.zero;

            string path = AssetDatabase.GetAssetPath(sprite);
            string absolute = path.StartsWith("Assets/")
                ? Path.Combine(Application.dataPath, path.Substring("Assets/".Length))
                : path;

            if (!File.Exists(absolute))
            {
                return false;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            if (!tex.LoadImage(File.ReadAllBytes(absolute)))
            {
                Object.DestroyImmediate(tex);
                return false;
            }

            int w = tex.width;
            int h = tex.height;
            Color[] pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);

            int xMin = Mathf.Clamp(Mathf.RoundToInt(w * (0.5f - CentreBandHalfWidth)), 0, w - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(w * (0.5f + CentreBandHalfWidth)), 0, w - 1);

            // Rows run bottom-up, so scanning down from the top means walking y backwards.
            int topRow = -1;
            for (int y = h - 1; y >= 0 && topRow < 0; y--)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (pixels[y * w + x].a > AlphaFloor)
                    {
                        topRow = y;
                        break;
                    }
                }
            }

            if (topRow < 0)
            {
                return false;
            }

            int band = Mathf.Max(1, Mathf.RoundToInt(h * HeadBandHeightFraction));
            int bottomRow = Mathf.Max(0, topRow - band + 1);

            double sumX = 0d;
            int n = 0;

            for (int y = bottomRow; y <= topRow; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    if (pixels[y * w + x].a > AlphaFloor)
                    {
                        sumX += x;
                        n++;
                    }
                }
            }

            if (n == 0)
            {
                return false;
            }

            // +y is up, matching Unity's world axes, so the animator can use the value directly.
            anchor = new Vector2((float)(sumX / n), topRow);
            return true;
        }
    }
}
