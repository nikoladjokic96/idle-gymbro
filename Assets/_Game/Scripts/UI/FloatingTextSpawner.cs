using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;

namespace IdleGymBro.UI
{
    // Pooled "+X" floating text, one per tap rep. Pooled (not instantiate/destroy per tap) since
    // taps can fire multiple times a second; pool never grows past _poolSize, extra taps skip.
    public class FloatingTextSpawner : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _spawnArea;

        [SerializeField]
        private float _floatDistance = 140f;

        [SerializeField]
        private float _duration = 0.6f;

        [SerializeField]
        private int _poolSize = 12;

        [SerializeField]
        private float _fontSize = 44f;

        private readonly Queue<TMP_Text> _pool = new Queue<TMP_Text>();
        private readonly List<TMP_Text> _active = new List<TMP_Text>();
        private bool _missingSpawnAreaLogged;

        private void Awake()
        {
            if (_spawnArea == null)
            {
                return;
            }

            for (int i = 0; i < _poolSize; i++)
            {
                _pool.Enqueue(CreatePooledText());
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TapGainsEvent>(HandleTapGains);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TapGainsEvent>(HandleTapGains);

            // Coroutines were killed mid-flight, so their own return-to-pool cleanup never ran;
            // reclaim every still-showing text by hand so the pool is intact on re-enable.
            StopAllCoroutines();

            for (int i = 0; i < _active.Count; i++)
            {
                var text = _active[i];
                if (text != null)
                {
                    text.gameObject.SetActive(false);
                    _pool.Enqueue(text);
                }
            }

            _active.Clear();
        }

        private void HandleTapGains(TapGainsEvent e)
        {
            if (_spawnArea == null)
            {
                if (!_missingSpawnAreaLogged)
                {
                    Debug.LogError("FloatingTextSpawner: _spawnArea is not assigned. Floating text is disabled.");
                    _missingSpawnAreaLogged = true;
                }

                return;
            }

            if (_pool.Count == 0)
            {
                // Pool exhausted by rapid taps: skip this one rather than growing unbounded.
                return;
            }

            TMP_Text text = _pool.Dequeue();
            text.text = "+" + NumberFormatter.Format(e.Amount);

            var rt = (RectTransform)text.transform;
            rt.anchoredPosition = new Vector2(Random.Range(-120f, 120f), Random.Range(-60f, 60f));

            Color color = text.color;
            color.a = 1f;
            text.color = color;

            text.gameObject.SetActive(true);
            _active.Add(text);
            StartCoroutine(Animate(text));
        }

        private IEnumerator Animate(TMP_Text text)
        {
            var rt = (RectTransform)text.transform;
            Vector2 start = rt.anchoredPosition;
            Vector2 end = start + new Vector2(0f, _floatDistance);

            if (_duration <= 0f)
            {
                rt.anchoredPosition = end;
                ReturnToPool(text);
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, t);

                Color color = text.color;
                color.a = 1f - t;
                text.color = color;

                yield return null;
            }

            rt.anchoredPosition = end;
            ReturnToPool(text);
        }

        private void ReturnToPool(TMP_Text text)
        {
            text.gameObject.SetActive(false);
            _active.Remove(text);
            _pool.Enqueue(text);
        }

        private TMP_Text CreatePooledText()
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(_spawnArea, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = _fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false; // must never intercept taps meant for the game beneath it

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 100f);

            go.SetActive(false);
            return text;
        }
    }
}
