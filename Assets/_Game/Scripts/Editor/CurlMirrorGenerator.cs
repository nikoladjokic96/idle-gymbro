using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleGymBro.EditorTools
{
    // Builds the second half of the curl by MIRRORING the first, so both arms actually take turns.
    //
    // The generated clip only ever lifts one arm: the other just holds its dumbbell for the whole
    // loop. Two re-rolls with progressively more explicit prompts made it worse rather than better —
    // one produced both arms curling together, the other stopped lifting anything at all. The model
    // does not reliably express "now the OTHER arm".
    //
    // It does not need to. The character is a bald, front-facing, near-symmetric sprite, so flipping
    // the authored frames horizontally IS the other arm's rep — including the head turn, which flips
    // to follow the dumbbell that is now up. Concatenating the two gives a true alternating cycle
    // for zero generations and with no risk of the art drifting.
    //
    // Writes work<N+1>..work<2N> next to the authored work1..workN.
    public static class CurlMirrorGenerator
    {
        private const string ArtFolder = "Assets/_Game/Art/Character/Placeholders";
        private const int TierCount = 6;
        private const int SourceFrames = 8;

        [MenuItem("IdleGymBro/Mirror Curl Frames")]
        public static void MirrorMenu()
        {
            Debug.Log($"[CurlMirrorGenerator] {Mirror()} mirrored frames written.");
        }

        public static int Mirror()
        {
            int written = 0;

            for (int tier = 1; tier <= TierCount; tier++)
            {
                for (int f = 1; f <= SourceFrames; f++)
                {
                    Texture2D src = Load($"{ArtFolder}/body_tier{tier}_work{f}.png");

                    if (src == null)
                    {
                        continue;
                    }

                    int w = src.width;
                    int h = src.height;
                    Color[] px = src.GetPixels();
                    Object.DestroyImmediate(src);

                    var flipped = new Color[w * h];

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            flipped[y * w + (w - 1 - x)] = px[y * w + x];
                        }
                    }

                    Write($"{ArtFolder}/body_tier{tier}_work{SourceFrames + f}.png", flipped, w, h);
                    written++;
                }
            }

            AssetDatabase.Refresh();
            return written;
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
