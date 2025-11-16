using UnityEngine;

namespace App.Home
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Clips (BGM)")]
        [SerializeField] private AudioClip _bgmClip;

        private AudioSource _bgmAudioSource;

        private void Start()
        {
            _bgmAudioSource = GetComponent<AudioSource>();
            PlayBGM();
        }

        private void PlayBGM()
        {
            if (_bgmAudioSource != null && _bgmClip != null)
            {
                _bgmAudioSource.clip = _bgmClip;
                _bgmAudioSource.loop = true;
                _bgmAudioSource.Play();
            }
        }
    }
}