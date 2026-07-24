using UnityEngine;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Progression;

namespace IdleGymBro.UI
{
    // Renders the current location's name/progress on the top-left HUD button (docs/ui-layout.md).
    // Open behavior is wired separately through ModalToggle — this component only renders.
    public class StoryProgressButton : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _label;

        private void OnEnable()
        {
            EventBus.Subscribe<LocationProgressChangedEvent>(HandleProgressChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationProgressChangedEvent>(HandleProgressChanged);
        }

        private void HandleProgressChanged(LocationProgressChangedEvent e)
        {
            if (_label == null)
            {
                return;
            }

            _label.text = $"{e.DisplayName}\n{Mathf.FloorToInt(e.Progress01 * 100f)}%" + (e.CanAdvance ? " ▲" : string.Empty);
        }
    }
}
