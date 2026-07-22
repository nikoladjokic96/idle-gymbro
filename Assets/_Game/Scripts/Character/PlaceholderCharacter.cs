using UnityEngine;
using System.Collections;
using IdleGymBro.Core;
using IdleGymBro.Gameplay;

namespace IdleGymBro.Character
{
    public class PlaceholderCharacter : MonoBehaviour
    {
        [SerializeField]
        private float _punchScale = 1.15f;

        [SerializeField]
        private float _punchDuration = 0.08f;

        private Vector3 _originalScale;
        private Coroutine _punchRoutine;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RepPerformedEvent>(HandleRepPerformed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RepPerformedEvent>(HandleRepPerformed);

            if (_punchRoutine != null)
            {
                StopCoroutine(_punchRoutine);
                _punchRoutine = null;
            }

            transform.localScale = _originalScale;
        }

        private void HandleRepPerformed(RepPerformedEvent e)
        {
            if (_punchRoutine != null)
            {
                StopCoroutine(_punchRoutine);
            }

            _punchRoutine = StartCoroutine(Punch());
        }

        private IEnumerator Punch()
        {
            transform.localScale = _originalScale * _punchScale;

            if (_punchDuration <= 0f)
            {
                transform.localScale = _originalScale;
                _punchRoutine = null;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < _punchDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _punchDuration);
                transform.localScale = Vector3.Lerp(_originalScale * _punchScale, _originalScale, t);
                yield return null;
            }

            transform.localScale = _originalScale;
            _punchRoutine = null;
        }
    }
}
