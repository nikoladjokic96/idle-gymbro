using System.Collections.Generic;
using UnityEngine;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Progression;
using UnityEngine.UI;

namespace IdleGymBro.UI
{
    // Locations modal content: runtime-built rows (one per location) + a MOVE UP action.
    // Progress/CanAdvance are cached from the last LocationProgressChangedEvent rather than
    // recomputed here — LocationManager keeps that math private, event-driven per §16.
    public class LocationsPanel : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _rowsContainer;

        [SerializeField]
        private Button _moveUpButton;

        [SerializeField]
        private TMP_Text _moveUpLabel;

        [SerializeField]
        private float _rowHeight = 90f;

        [SerializeField]
        private float _rowFontSize = 40f;

        private LocationManager _manager;
        private readonly List<TMP_Text> _rows = new List<TMP_Text>();

        private float _lastProgress;
        private bool _lastCanAdvance;

        private void Start()
        {
            _manager = FindAnyObjectByType<LocationManager>();
            BuildRows();
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LocationProgressChangedEvent>(HandleProgressChanged);
            EventBus.Subscribe<LocationChangedEvent>(HandleLocationChanged);

            if (_moveUpButton != null)
            {
                _moveUpButton.onClick.AddListener(OnMoveUp);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationProgressChangedEvent>(HandleProgressChanged);
            EventBus.Unsubscribe<LocationChangedEvent>(HandleLocationChanged);

            if (_moveUpButton != null)
            {
                _moveUpButton.onClick.RemoveListener(OnMoveUp);
            }
        }

        private void BuildRows()
        {
            if (_manager == null || _rowsContainer == null)
            {
                return;
            }

            for (int i = 0; i < _manager.Count; i++)
            {
                var rowGo = new GameObject("Row_" + i, typeof(RectTransform));
                rowGo.transform.SetParent(_rowsContainer, false);

                var rowText = rowGo.AddComponent<TextMeshProUGUI>();
                rowText.fontSize = _rowFontSize;
                rowText.alignment = TextAlignmentOptions.Center;
                rowText.color = Color.white;
                rowText.raycastTarget = false;

                var rect = rowGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -60f - i * _rowHeight);
                rect.sizeDelta = new Vector2(600f, _rowHeight - 10f);

                _rows.Add(rowText);
            }
        }

        private void HandleProgressChanged(LocationProgressChangedEvent e)
        {
            _lastProgress = e.Progress01;
            _lastCanAdvance = e.CanAdvance;
            Refresh();
        }

        private void HandleLocationChanged(LocationChangedEvent e)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_manager == null)
            {
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var loc = _manager.GetLocation(i);

                if (loc == null)
                {
                    continue;
                }

                string prefix = i < _manager.CurrentIndex ? "[DONE] " : i == _manager.CurrentIndex ? "> " : "[LOCKED] ";
                string percent = i < _manager.CurrentIndex ? "100%" : i == _manager.CurrentIndex ? $"{Mathf.FloorToInt(_lastProgress * 100f)}%" : string.Empty;

                _rows[i].text = $"{prefix}{loc.DisplayName}  {percent}";
            }

            if (_moveUpButton != null)
            {
                _moveUpButton.gameObject.SetActive(_lastCanAdvance);
            }

            if (_moveUpLabel != null)
            {
                _moveUpLabel.text = "MOVE UP ▲";
            }
        }

        private void OnMoveUp()
        {
            _manager?.TryAdvance();
        }
    }
}
