using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Progression;

namespace IdleGymBro.UI
{
    // Body / Equipment / Macros tabs over one shared list of rows.
    //
    // The rows are all built once and simply shown or hidden. Rebuilding the list per tab would
    // mean re-binding every UpgradeButton on each switch, and the buttons cache manager lookups and
    // subscribe to the EventBus in OnEnable — churn with nothing gained, since the whole catalogue
    // is a few dozen rows.
    //
    // Equipment is additionally filtered to the CURRENT location: gear from a gym the player has
    // not reached is hidden rather than greyed out. A list of things you cannot buy is not
    // information, and the tab exists to answer "what do I still need here".
    public class UpgradeTabBar : MonoBehaviour
    {
        [SerializeField]
        private Transform _content;

        [SerializeField]
        private Button[] _tabButtons;

        [SerializeField]
        private Image[] _tabSurfaces;

        [SerializeField]
        private TMP_Text[] _tabLabels;

        [SerializeField]
        private Color _selectedColor = new Color(0.16f, 0.51f, 0.96f, 1f);

        [SerializeField]
        private Color _unselectedColor = Color.white;

        private readonly UpgradeCategory[] _categories =
        {
            UpgradeCategory.Body,
            UpgradeCategory.Equipment,
            UpgradeCategory.Macros
        };

        private int _selected;
        private string _currentLocationId = string.Empty;

        private void OnEnable()
        {
            EventBus.Subscribe<LocationChangedEvent>(HandleLocationChanged);

            for (int i = 0; i < (_tabButtons?.Length ?? 0); i++)
            {
                int index = i; // capture per iteration, or every tab selects the last one
                _tabButtons[i].onClick.AddListener(() => Select(index));
            }

            // The modal is disabled while closed, so state is re-read on every open.
            var locations = FindAnyObjectByType<LocationManager>();

            if (locations != null && locations.Current != null)
            {
                _currentLocationId = locations.Current.Id;
            }

            Select(_selected);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationChangedEvent>(HandleLocationChanged);

            for (int i = 0; i < (_tabButtons?.Length ?? 0); i++)
            {
                _tabButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void HandleLocationChanged(LocationChangedEvent e)
        {
            _currentLocationId = e.LocationId;
            ApplyFilter();
        }

        public void Select(int index)
        {
            _selected = Mathf.Clamp(index, 0, _categories.Length - 1);
            ApplyFilter();
            RefreshTabVisuals();
        }

        private void ApplyFilter()
        {
            if (_content == null)
            {
                return;
            }

            UpgradeCategory category = _categories[_selected];

            foreach (UpgradeButton row in CollectRows())
            {
                UpgradeData data = row.Upgrade;
                bool visible = data != null && data.Category == category;

                if (visible && category == UpgradeCategory.Equipment)
                {
                    visible = string.IsNullOrEmpty(data.LocationId) || data.LocationId == _currentLocationId;
                }

                row.gameObject.SetActive(visible);
            }
        }

        private List<UpgradeButton> CollectRows()
        {
            var rows = new List<UpgradeButton>();

            for (int i = 0; i < _content.childCount; i++)
            {
                // GetComponent, not GetComponentInChildren: an inactive row must still be found, and
                // the search must not reach into a row's own children.
                var row = _content.GetChild(i).GetComponent<UpgradeButton>();

                if (row != null)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private void RefreshTabVisuals()
        {
            for (int i = 0; i < (_tabSurfaces?.Length ?? 0); i++)
            {
                if (_tabSurfaces[i] != null)
                {
                    _tabSurfaces[i].color = i == _selected ? _selectedColor : _unselectedColor;
                }
            }

            for (int i = 0; i < (_tabLabels?.Length ?? 0); i++)
            {
                if (_tabLabels[i] != null)
                {
                    _tabLabels[i].color = i == _selected ? Color.white : new Color(0.75f, 0.78f, 0.85f, 1f);
                }
            }
        }
    }
}
