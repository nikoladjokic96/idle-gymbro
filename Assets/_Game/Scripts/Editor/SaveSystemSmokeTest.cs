using System;
using UnityEditor;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.EditorTools
{
    // Headless verification of the save pipeline without needing Play mode:
    // Serialize -> Encrypt -> Decrypt -> Deserialize must round-trip losslessly,
    // and a garbage blob must be rejected (not crash). Run via menu or
    // -executeMethod IdleGymBro.EditorTools.SaveSystemSmokeTest.RunSaveRoundTrip
    public static class SaveSystemSmokeTest
    {
        [MenuItem("IdleGymBro/Test Save RoundTrip")]
        public static void RunSaveRoundTrip()
        {
            var original = new SaveData
            {
                Version = 1,
                TotalGains = 12345.6789d,
                CurrentEnergy = 42.5f,
                LastSaveTimeTicks = 638000000000000000L
            };

            byte[] blob = SaveSystem.Encrypt(SaveSystem.Serialize(original));
            SaveData back = SaveSystem.Deserialize(SaveSystem.Decrypt(blob));

            bool roundTripOk =
                back != null &&
                back.Version == original.Version &&
                back.TotalGains == original.TotalGains &&
                Mathf.Approximately(back.CurrentEnergy, original.CurrentEnergy) &&
                back.LastSaveTimeTicks == original.LastSaveTimeTicks;

            // A tampered/garbage blob must throw (caller catches and falls back to fresh),
            // never return silently-wrong data.
            bool rejectsGarbage = false;
            try
            {
                SaveSystem.Deserialize(SaveSystem.Decrypt(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            }
            catch
            {
                rejectsGarbage = true;
            }

            if (roundTripOk && rejectsGarbage)
            {
                Debug.Log("[SaveSystemSmokeTest] PASS — round-trip lossless, garbage rejected.");
            }
            else
            {
                Debug.LogError($"[SaveSystemSmokeTest] FAIL — roundTripOk={roundTripOk} rejectsGarbage={rejectsGarbage}");
            }
        }
    }
}
