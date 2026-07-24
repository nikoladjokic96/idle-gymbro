using System.Collections.Generic;
using UnityEngine;
using IdleGymBro.Data;
using IdleGymBro.Economy;
using IdleGymBro.Character;

namespace IdleGymBro.Core
{
    // Central SFX player: listens for gameplay events and fires the matching placeholder clip.
    // Mute state persists across sessions via PlayerPrefs (no save-file involvement — audio
    // preference isn't game progress).
    public class AudioManager : MonoBehaviour
    {
        [SerializeField]
        private AudioLibrary _library;

        [SerializeField]
        private AudioSource _source;

        private const string MutedPrefKey = "audio_muted";

        private bool _muted;

        // CharacterBuilder publishes MuscleTierChangedEvent once on startup for the character's
        // initial tier (see UI/TierUpBanner.cs) — that application must not play a sound.
        private bool _initialTierSeen;

        private readonly HashSet<string> _activeBoosters = new HashSet<string>();

        public bool IsMuted => _muted;

        private void Awake()
        {
            _muted = PlayerPrefs.GetInt(MutedPrefKey, 0) == 1;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TapGainsEvent>(HandleTapGains);
            EventBus.Subscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Subscribe<MuscleTierChangedEvent>(HandleMuscleTierChanged);
            EventBus.Subscribe<BoosterStateChangedEvent>(HandleBoosterStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TapGainsEvent>(HandleTapGains);
            EventBus.Unsubscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Unsubscribe<MuscleTierChangedEvent>(HandleMuscleTierChanged);
            EventBus.Unsubscribe<BoosterStateChangedEvent>(HandleBoosterStateChanged);
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            PlayerPrefs.SetInt(MutedPrefKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void HandleTapGains(TapGainsEvent e)
        {
            Play(_library != null ? _library.TapClip : null);
        }

        private void HandleUpgradePurchased(UpgradePurchasedEvent e)
        {
            Play(_library != null ? _library.BuyClip : null);
        }

        private void HandleMuscleTierChanged(MuscleTierChangedEvent e)
        {
            if (!_initialTierSeen)
            {
                _initialTierSeen = true;
                return;
            }

            Play(_library != null ? _library.TierUpClip : null);
        }

        private void HandleBoosterStateChanged(BoosterStateChangedEvent e)
        {
            if (e.IsActive)
            {
                if (_activeBoosters.Add(e.BoosterId))
                {
                    Play(_library != null ? _library.BoosterClip : null);
                }
            }
            else
            {
                _activeBoosters.Remove(e.BoosterId);
            }
        }

        private void Play(AudioClip clip)
        {
            if (_muted || clip == null || _source == null)
            {
                return;
            }

            _source.PlayOneShot(clip, _library != null ? _library.MasterVolume : 1f);
        }
    }
}
