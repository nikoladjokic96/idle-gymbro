using System.Collections;
using UnityEngine;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Character;

namespace IdleGymBro.UI
{
    // Celebration banner on muscle tier-up. Lives on an always-active GameObject (see
    // CoreLoopSceneBootstrap) since it deactivates only its own _text object, not itself.
    public class TierUpBanner : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _text;

        [SerializeField]
        private float _showSeconds = 1.4f;

        [SerializeField]
        private float _fadeSeconds = 0.4f;

        // CharacterBuilder publishes MuscleTierChangedEvent once on startup for the character's
        // initial tier — that application must not flash the banner, only real tier-ups after it.
        private bool _initialTierSeen;

        private Coroutine _bannerRoutine;

        private void Awake()
        {
            if (_text != null)
            {
                _text.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<MuscleTierChangedEvent>(HandleMuscleTierChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MuscleTierChangedEvent>(HandleMuscleTierChanged);

            if (_bannerRoutine != null)
            {
                StopCoroutine(_bannerRoutine);
                _bannerRoutine = null;
            }

            if (_text != null)
            {
                _text.gameObject.SetActive(false);
                _text.transform.localScale = Vector3.one;
            }
        }

        private void HandleMuscleTierChanged(MuscleTierChangedEvent e)
        {
            if (!_initialTierSeen)
            {
                _initialTierSeen = true;
                return;
            }

            if (_text == null)
            {
                return;
            }

            _text.text = $"TIER UP!\n{e.DisplayName}";

            if (_bannerRoutine != null)
            {
                StopCoroutine(_bannerRoutine);
            }

            _bannerRoutine = StartCoroutine(ShowBanner());
        }

        private IEnumerator ShowBanner()
        {
            _text.gameObject.SetActive(true);
            _text.transform.localScale = Vector3.one * 0.6f;

            Color color = _text.color;
            color.a = 1f;
            _text.color = color;

            // Pop-in.
            const float popDuration = 0.15f;
            float elapsed = 0f;
            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                _text.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t);
                yield return null;
            }
            _text.transform.localScale = Vector3.one;

            // Hold.
            elapsed = 0f;
            while (elapsed < _showSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Fade out.
            elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeSeconds);
                color.a = 1f - t;
                _text.color = color;
                yield return null;
            }

            color.a = 1f;
            _text.color = color;
            _text.gameObject.SetActive(false);
            _bannerRoutine = null;
        }
    }
}
