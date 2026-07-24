using UnityEditor;
using UnityEngine;
using System.IO;
using System;

namespace IdleGymBro.EditorTools
{
    // Generates tiny placeholder WAV clips with correct PCM headers, so audio hookup can be
    // verified end-to-end before real SFX exist. Deterministic (fixed noise seed) so re-running
    // the generator never produces a diff in the committed bytes.
    public static class PlaceholderSfxGenerator
    {
        private const string OutputFolder = "Assets/_Game/Audio/Placeholders";
        private const int SampleRate = 44100;
        private const int NoiseSeed = 42;

        [MenuItem("IdleGymBro/Generate Placeholder SFX")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            WriteWav($"{OutputFolder}/tap.wav", BuildTap());
            WriteWav($"{OutputFolder}/buy.wav", BuildBuy());
            WriteWav($"{OutputFolder}/tier_up.wav", BuildTierUp());
            WriteWav($"{OutputFolder}/booster.wav", BuildBooster());

            AssetDatabase.SaveAssets();
            Debug.Log("[PlaceholderSfxGenerator] 4 clips generated.");
        }

        // 50ms 880Hz square wave, linear fade-out over the whole clip.
        private static float[] BuildTap()
        {
            int sampleCount = SecondsToSamples(0.05f);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float fade = 1f - (float)i / sampleCount;
                samples[i] = Square(t, 880f) * 0.25f * fade;
            }

            return samples;
        }

        // Two 80ms square tones (523Hz then 784Hz), each with a small fade at its end.
        private static float[] BuildBuy()
        {
            float[] toneA = BuildTone(523f, 0.08f, 0.3f);
            float[] toneB = BuildTone(784f, 0.08f, 0.3f);
            return Concat(toneA, toneB);
        }

        // Arpeggio: three 100ms square tones (523/659/784Hz), each faded.
        private static float[] BuildTierUp()
        {
            float[] toneA = BuildTone(523f, 0.10f, 0.3f);
            float[] toneB = BuildTone(659f, 0.10f, 0.3f);
            float[] toneC = BuildTone(784f, 0.10f, 0.3f);
            return Concat(Concat(toneA, toneB), toneC);
        }

        // 200ms white-noise "whoosh" with a triangular amplitude envelope (0 -> 0.35 -> 0).
        // Fixed-seed Random keeps the output deterministic/idempotent across regenerations.
        private static float[] BuildBooster()
        {
            int sampleCount = SecondsToSamples(0.2f);
            var samples = new float[sampleCount];
            // System-qualified: this file also has 'using UnityEngine;', whose Random collides.
            var random = new System.Random(NoiseSeed);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = t < 0.5f ? t * 2f : (1f - t) * 2f;
                float noise = (float)(random.NextDouble() * 2d - 1d);
                samples[i] = noise * 0.35f * envelope;
            }

            return samples;
        }

        // Single square tone with a short fade-out at its tail so tone-to-tone edges don't click.
        private static float[] BuildTone(float frequency, float durationSeconds, float amplitude)
        {
            int sampleCount = SecondsToSamples(durationSeconds);
            var samples = new float[sampleCount];

            int fadeSamples = Mathf.Max(1, sampleCount / 8);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float fade = i >= sampleCount - fadeSamples
                    ? (float)(sampleCount - i) / fadeSamples
                    : 1f;
                samples[i] = Square(t, frequency) * amplitude * fade;
            }

            return samples;
        }

        private static float Square(float timeSeconds, float frequency)
        {
            float phase = (timeSeconds * frequency) % 1f;
            return phase < 0.5f ? 1f : -1f;
        }

        private static int SecondsToSamples(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));
        }

        private static float[] Concat(float[] a, float[] b)
        {
            var result = new float[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }

        private static void WriteWav(string path, float[] samples)
        {
            const int channels = 1;
            const int bitsPerSample = 16;
            const int blockAlign = channels * bitsPerSample / 8;
            int byteRate = SampleRate * blockAlign;
            int dataSize = samples.Length * blockAlign;

            string absolutePath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));

            using (var stream = new FileStream(absolutePath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                // RIFF header.
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });

                // "fmt " chunk.
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)channels);
                writer.Write(SampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);

                // "data" chunk.
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);

                foreach (float sample in samples)
                {
                    float clamped = Mathf.Clamp(sample, -1f, 1f);
                    writer.Write((short)(clamped * 32767));
                }
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
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
