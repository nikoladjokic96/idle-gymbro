using UnityEngine;
using UnityEngine.UI;

namespace IdleGymBro.UI
{
    // Generic open/close for a modal panel driven by an open button and a close button.
    // The panel starts hidden; opening it shows a full-screen dimmer that (having a
    // raycast target) also blocks taps to the game behind it.
    public class ModalToggle : MonoBehaviour
    {
        [SerializeField]
        private GameObject _panel;

        [SerializeField]
        private Button _openButton;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Button _backdropButton;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_openButton != null)
            {
                _openButton.onClick.AddListener(Open);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_backdropButton != null)
            {
                _backdropButton.onClick.AddListener(Close);
            }
        }

        private void OnDisable()
        {
            if (_openButton != null)
            {
                _openButton.onClick.RemoveListener(Open);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
            }

            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(Close);
            }
        }

        public void Open()
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
            }
        }

        public void Close()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }
    }
}
