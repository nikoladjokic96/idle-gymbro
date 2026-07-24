using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Progression;

namespace IdleGymBro.Character
{
    // Shows the current location's background sprite on a world-space renderer behind the
    // character. Data-driven: the sprite lives on each LocationData, swapped on location change.
    public class LocationBackground : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer _renderer;

        [SerializeField]
        private LocationData[] _locations;

        private void OnEnable()
        {
            EventBus.Subscribe<LocationChangedEvent>(HandleLocationChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationChangedEvent>(HandleLocationChanged);
        }

        private void HandleLocationChanged(LocationChangedEvent e)
        {
            if (_renderer == null || _locations == null || _locations.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(e.Index, 0, _locations.Length - 1);

            if (_locations[index] != null)
            {
                _renderer.sprite = _locations[index].BackgroundSprite;
            }
        }
    }
}
