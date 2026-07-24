using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;

namespace IdleGymBro.UI
{
    // Sound on/off toggle for the settings modal. Reads/writes mute state through AudioManager
    // (found at runtime; the modal itself is built by CoreLoopSceneBootstrap).
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField]
        private Button _soundToggleButton;

        [SerializeField]
        private TMP_Text _soundToggleLabel;

        private AudioManager _audio;

        private void Start()
        {
            _audio = FindAnyObjectByType<AudioManager>();
            RefreshLabel();
        }

        private void OnEnable()
        {
            if (_soundToggleButton != null)
            {
                _soundToggleButton.onClick.AddListener(OnSoundToggle);
            }
        }

        private void OnDisable()
        {
            if (_soundToggleButton != null)
            {
                _soundToggleButton.onClick.RemoveListener(OnSoundToggle);
            }
        }

        private void OnSoundToggle()
        {
            if (_audio != null)
            {
                _audio.SetMuted(!_audio.IsMuted);
                RefreshLabel();
            }
        }

        private void RefreshLabel()
        {
            if (_soundToggleLabel != null)
            {
                _soundToggleLabel.text = _audio != null && _audio.IsMuted ? "SOUND: OFF" : "SOUND: ON";
            }
        }
    }
}
