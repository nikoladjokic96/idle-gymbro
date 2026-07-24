using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "IdleGymBro/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        [SerializeField]
        private AudioClip _tapClip;

        [SerializeField]
        private AudioClip _buyClip;

        [SerializeField]
        private AudioClip _tierUpClip;

        [SerializeField]
        private AudioClip _boosterClip;

        [SerializeField]
        [Range(0f, 1f)]
        private float _masterVolume = 0.8f;

        public AudioClip TapClip => _tapClip;
        public AudioClip BuyClip => _buyClip;
        public AudioClip TierUpClip => _tierUpClip;
        public AudioClip BoosterClip => _boosterClip;
        public float MasterVolume => _masterVolume;
    }
}
