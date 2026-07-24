using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Meta;

namespace IdleGymBro.UI
{
    // Modal content: one runtime-built row per achievement (progress + reward) and a single
    // CLAIM ALL button that collects every ready reward at once.
    public class AchievementsPanel : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _rowsContainer;

        [SerializeField]
        private Button _claimAllButton;

        [SerializeField]
        private TMP_Text _claimAllLabel;

        [SerializeField]
        private float _rowHeight = 96f;

        [SerializeField]
        private float _rowFontSize = 30f;

        private AchievementManager _manager;
        private readonly List<TMP_Text> _rows = new List<TMP_Text>();

        private void Start()
        {
            _manager = FindAnyObjectByType<AchievementManager>();
            BuildRows();
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AchievementsChangedEvent>(HandleAchievementsChanged);

            if (_claimAllButton != null)
            {
                _claimAllButton.onClick.AddListener(OnClaimAll);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AchievementsChangedEvent>(HandleAchievementsChanged);

            if (_claimAllButton != null)
            {
                _claimAllButton.onClick.RemoveListener(OnClaimAll);
            }
        }

        private void OnClaimAll()
        {
            _manager?.ClaimAll();
        }

        private void HandleAchievementsChanged(AchievementsChangedEvent e)
        {
            Refresh();
        }

        private void BuildRows()
        {
            if (_manager == null || _rowsContainer == null)
            {
                return;
            }

            for (int i = 0; i < _manager.Count; i++)
            {
                var go = new GameObject("AchievementRow", typeof(RectTransform));
                go.transform.SetParent(_rowsContainer, false);

                var text = go.AddComponent<TextMeshProUGUI>();
                text.fontSize = _rowFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.raycastTarget = false;

                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(620f, _rowHeight - 8f);
                rt.anchoredPosition = new Vector2(0f, -20f - i * _rowHeight);

                _rows.Add(text);
            }
        }

        private void Refresh()
        {
            if (_manager == null)
            {
                return;
            }

            bool anyClaimable = false;

            for (int i = 0; i < _rows.Count && i < _manager.Count; i++)
            {
                AchievementData achievement = _manager.GetData(i);
                if (achievement == null || _rows[i] == null)
                {
                    continue;
                }

                bool claimed = _manager.IsClaimed(achievement.Id);
                bool claimable = _manager.IsClaimable(achievement);
                if (claimable)
                {
                    anyClaimable = true;
                }

                string tag = claimed ? "[DONE] " : claimable ? "[CLAIM] " : string.Empty;
                double progress = _manager.GetProgress(achievement);
                _rows[i].text = $"{tag}{achievement.DisplayName}  {NumberFormatter.Format(progress)}/{NumberFormatter.Format(achievement.Threshold)}\n+{NumberFormatter.Format(achievement.RewardGains)}";
            }

            if (_claimAllLabel != null)
            {
                _claimAllLabel.text = "CLAIM ALL";
            }

            if (_claimAllButton != null)
            {
                _claimAllButton.interactable = anyClaimable;
            }
        }
    }
}
