using UnityEngine;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Meta;

namespace IdleGymBro.UI
{
    // Bottom-left HUD button that opens the achievements modal; its label shows a "(N)" badge
    // when N achievements are ready to claim. Opening is handled by ModalToggle on the same button.
    public class AchievementsButton : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private string _baseText = "GOALS";

        private void Awake()
        {
            if (_label != null)
            {
                _label.text = _baseText;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AchievementsChangedEvent>(HandleAchievementsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AchievementsChangedEvent>(HandleAchievementsChanged);
        }

        private void HandleAchievementsChanged(AchievementsChangedEvent e)
        {
            if (_label != null)
            {
                _label.text = e.ClaimableCount > 0 ? $"{_baseText}\n({e.ClaimableCount})" : _baseText;
            }
        }
    }
}
