using App.Reversi.Messaging;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [Tooltip("効果音(SE)再生用のAudioSource")]
        [SerializeField] private AudioSource _seAudioSource;
        [Tooltip("BGM再生用のAudioSource。LoopをTrueに設定してください。")]
        [SerializeField] private AudioSource _bgmAudioSource;

        [Header("Audio Clips (SE)")]
        [SerializeField] private AudioClip _putStoneClip;
        [SerializeField] private AudioClip _flipStonesClip;
        [SerializeField] private AudioClip _frozenFlipStonesClip;
        [SerializeField] private AudioClip _extendClip;
        [SerializeField] private AudioClip _frozenClip;
        [SerializeField] private AudioClip _reverseClip;
        [SerializeField] private AudioClip _delayReverseClip;
        [SerializeField] private AudioClip _gameOverClip;

        [Header("Audio Clips (BGM)")]
        [SerializeField] private AudioClip _bgmClip;

        [Inject]
        private void Construct(
            ISubscriber<PlaySoundEffectMessage> soundSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber)
        {
            soundSubscriber.Subscribe(OnPlaySound);
            gameOverSubscriber.Subscribe(OnGameOver);

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

        private void OnPlaySound(PlaySoundEffectMessage msg)
        {
            if (_seAudioSource == null) return;

            switch (msg.Type)
            {
                case SoundEffectType.PutStone:
                    if (_putStoneClip != null) _seAudioSource.PlayOneShot(_putStoneClip);
                    break;
                case SoundEffectType.Flip:
                    if (_flipStonesClip != null) _seAudioSource.PlayOneShot(_flipStonesClip);
                    break;
                case SoundEffectType.FrozenFlip:
                    if (_frozenFlipStonesClip != null) _seAudioSource.PlayOneShot(_frozenFlipStonesClip);
                    break;
                case SoundEffectType.Frozen:
                    if (_frozenClip != null) _seAudioSource.PlayOneShot(_frozenClip);
                    break;
                //case StoneType.Broken:
                //case StoneType.Collapse:
                case SoundEffectType.Extend:
                    if (_extendClip != null) _seAudioSource.PlayOneShot(_extendClip);
                    break;
                case SoundEffectType.Reverse:
                    if (_reverseClip != null) _seAudioSource.PlayOneShot(_reverseClip);
                    break;
                case SoundEffectType.DelayReverse:
                    if (_delayReverseClip != null) _seAudioSource.PlayOneShot(_delayReverseClip);
                    break;
            }
        }

        private void OnGameOver(GameOverMessage msg)
        {
            if (_seAudioSource == null) return;

            if (_gameOverClip != null)
            {
                _seAudioSource.PlayOneShot(_gameOverClip);
            }
        }
    }
}