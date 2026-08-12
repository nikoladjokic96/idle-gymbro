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
        private readonly List<Button> _rowButtons = new List<Button>();

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
                // Each row is a BUTTON now. It used to be a bare label with raycastTarget off, so
                // tapping a location did nothing at all — which reads as a broken button rather than
                // as "this line is only a label". Tapping an already-visited location travels back
                // to it; the current one and anything not yet unlocked simply do not respond.
                var rowGo = new GameObject("Row_" + i, typeof(RectTransform), typeof(Image));
                rowGo.transform.SetParent(_rowsContainer, false);

                var rowImage = rowGo.GetComponent<Image>();
                rowImage.color = new Color(1f, 1f, 1f, 0.06f); // a target to hit, not a visible box

                var rowButton = rowGo.AddComponent<Button>();
                rowButton.targetGraphic = rowImage;
                int index = i; // capture per iteration, or every row selects the last location
                rowButton.onClick.AddListener(() => OnRowClicked(index));

                var textGo = new GameObject("Label", typeof(RectTransform));
                textGo.transform.SetParent(rowGo.transform, false);

                var rowText = textGo.AddComponent<TextMeshProUGUI>();
                rowText.fontSize = _rowFontSize;
                rowText.alignment = TextAlignmentOptions.Center;
                rowText.color = Color.white;
                rowText.raycastTarget = false; // the button underneath must receive the tap

                var textRect = textGo.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var rect = rowGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -60f - i * _rowHeight);
                rect.sizeDelta = new Vector2(600f, _rowHeight - 10f);

                _rows.Add(rowText);
                _rowButtons.Add(rowButton);
            }
        }

        private void OnRowClicked(int index)
        {
            if (_manager != null && _manager.TrySelect(index))
            {
                Refresh();
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

                // Only already-visited rows respond, so a tap that does nothing is never a mystery:
                // the row that cannot be pressed is also the row that is dimmed.
                if (i < _rowButtons.Count && _rowButtons[i] != null)
                {
                    bool travellable = i < _manager.CurrentIndex;
                    _rowButtons[i].interactable = travellable;
                    _rows[i].color = travellable ? Color.white
                        : i == _manager.CurrentIndex ? new Color(1f, 0.86f, 0.45f, 1f)
                        : new Color(0.55f, 0.58f, 0.65f, 1f);
                }
            }

            if (_moveUpButton != null)
            {
                _moveUpButton.gameObject.SetActive(_lastCanAdvance);
            }

            if (_moveUpLabel != null)
            {
                // The pixel font has no arrow glyph; a missing glyph in a bitmap font draws nothing.
                _moveUpLabel.text = "MOVE UP";
            }
        }

        private void OnMoveUp()
        {
            _manager?.TryAdvance();
        }
    }
}
