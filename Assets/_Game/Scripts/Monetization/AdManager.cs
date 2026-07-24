using System;
using System.Collections;
using UnityEngine;

namespace IdleGymBro.Monetization
{
    // MOCK rewarded-ad provider. Real LevelPlay adapter (Faza 5, end of project) replaces ONLY
    // the internals of ShowRewarded/MockAd — callers (BoosterButton, OfflineClaimPopup) depend
    // solely on this public API and never touch a mediation SDK directly.
    public class AdManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject _adOverlay;

        [SerializeField]
        private float _mockAdSeconds = 1f;

        public bool IsShowingAd { get; private set; }

        private void Awake()
        {
            if (_adOverlay != null)
            {
                _adOverlay.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Never leave a stuck fullscreen blocker if this object is disabled mid-ad.
            StopAllCoroutines();
            IsShowingAd = false;

            if (_adOverlay != null)
            {
                _adOverlay.SetActive(false);
            }
        }

        public void ShowRewarded(string placement, Action onReward)
        {
            if (IsShowingAd)
            {
                return;
            }

            StartCoroutine(MockAd(placement, onReward));
        }

        private IEnumerator MockAd(string placement, Action onReward)
        {
            IsShowingAd = true;

            if (_adOverlay != null)
            {
                _adOverlay.SetActive(true);
            }

            yield return new WaitForSecondsRealtime(_mockAdSeconds);

            if (_adOverlay != null)
            {
                _adOverlay.SetActive(false);
            }

            IsShowingAd = false;
            Debug.Log($"[AdManager] MOCK rewarded ad '{placement}' completed.");
            onReward?.Invoke();
        }
    }
}
