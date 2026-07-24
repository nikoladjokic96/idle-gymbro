using System.Collections;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Economy;

namespace IdleGymBro.UI
{
    // Scale-pop of the gains counter on each tap rep only (not on passive trickle, which fires
    // via GainsChangedEvent ~10x/sec through TickSystem and would jitter the counter constantly).
    public class GainsCounterJuice : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _target;

        [SerializeField]
        private float _punchScale = 1.08f;

        [SerializeField]
        private float _duration = 0.08f;

        private Coroutine _punchRoutine;

        private void OnEnable()
        {
            EventBus.Subscribe<TapGainsEvent>(HandleTapGains);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TapGainsEvent>(HandleTapGains);

            if (_punchRoutine != null)
            {
                StopCoroutine(_punchRoutine);
                _punchRoutine = null;
            }

            if (_target != null)
            {
                _target.localScale = Vector3.one;
            }
        }

        private void HandleTapGains(TapGainsEvent e)
        {
            if (_target == null)
            {
                return;
            }

            if (_punchRoutine != null)
            {
                StopCoroutine(_punchRoutine);
            }

            _punchRoutine = StartCoroutine(Punch());
        }

        private IEnumerator Punch()
        {
            _target.localScale = Vector3.one * _punchScale;

            if (_duration <= 0f)
            {
                _target.localScale = Vector3.one;
                _punchRoutine = null;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
                _target.localScale = Vector3.Lerp(Vector3.one * _punchScale, Vector3.one, t);
                yield return null;
            }

            _target.localScale = Vector3.one;
            _punchRoutine = null;
        }
    }
}
