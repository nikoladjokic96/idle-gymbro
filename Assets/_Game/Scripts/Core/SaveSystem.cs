using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using IdleGymBro.Data;

namespace IdleGymBro.Core
{
    // Runs last so it restores saved state only after every other system has already
    // published its fresh-default state (e.g. EnergySystem.Start), overwriting it once.
    [DefaultExecutionOrder(1000)]
    public class SaveSystem : MonoBehaviour
    {
        private const string SaveFileName = "gymbro.sav";

        // Tamper-obfuscation, not real security: this key ships inside the built binary
        // and can be extracted from it. Acceptable for local single-player saves.
        private const string Passphrase = "IdleGymBro.save.v1";

        [SerializeField]
        private GameConfig _gameConfig;

        private float _autoSaveTimer;

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        // SHA256.Create().ComputeHash (not the .NET 5+ static SHA256.HashData) — the project
        // targets .NET Standard 2.1, where the one-shot HashData API does not exist.
        private static byte[] Key
        {
            get
            {
                using var sha = SHA256.Create();
                return sha.ComputeHash(Encoding.UTF8.GetBytes(Passphrase));
            }
        }

        private void Start()
        {
            bool hadSave = TryLoad(out SaveData data);

            if (hadSave)
            {
                foreach (ISaveable saveable in FindSaveables())
                {
                    saveable.RestoreState(data);
                }
            }

            // Always published (even with no/corrupt save) so downstream systems (e.g. offline
            // earnings) can react to load state instead of depending on SaveSystem directly.
            EventBus.Publish(new GameLoadedEvent(hadSave, hadSave ? data.LastSaveTimeTicks : 0L));
        }

        private void Update()
        {
            if (_gameConfig == null)
            {
                return;
            }

            _autoSaveTimer += Time.deltaTime;

            if (_autoSaveTimer >= _gameConfig.AutoSaveIntervalSeconds)
            {
                _autoSaveTimer = 0f;
                Save();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public void Save()
        {
            try
            {
                var data = new SaveData();

                foreach (ISaveable saveable in FindSaveables())
                {
                    saveable.CaptureState(data);
                }

                data.LastSaveTimeTicks = DateTime.UtcNow.Ticks;

                byte[] bytes = Encrypt(Serialize(data));
                File.WriteAllBytes(SavePath, bytes);
            }
            catch (Exception e)
            {
                // A failed save must never crash/stall the game; the player simply keeps playing unsaved.
                Debug.LogError("SaveSystem: save failed. " + e.Message);
            }
        }

        private bool TryLoad(out SaveData data)
        {
            data = null;

            if (!File.Exists(SavePath))
            {
                return false;
            }

            try
            {
                data = Deserialize(Decrypt(File.ReadAllBytes(SavePath)));
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: save unreadable (corrupt/tampered), starting fresh. " + e.Message);
                return false;
            }
        }

        private static IEnumerable<ISaveable> FindSaveables()
        {
            return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>();
        }

        public static string Serialize(SaveData data)
        {
            return JsonConvert.SerializeObject(data);
        }

        public static SaveData Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<SaveData>(json);
        }

        public static byte[] Encrypt(string plaintext)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.GenerateIV();

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Prepend the IV (16 bytes) so Decrypt can recover it without a separate channel.
            byte[] result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
            return result;
        }

        public static string Decrypt(byte[] data)
        {
            const int ivLength = 16;

            byte[] iv = new byte[ivLength];
            byte[] cipherBytes = new byte[data.Length - ivLength];
            Buffer.BlockCopy(data, 0, iv, 0, ivLength);
            Buffer.BlockCopy(data, ivLength, cipherBytes, 0, cipherBytes.Length);

            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = iv;

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
