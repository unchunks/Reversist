using App.Reversi.Messaging;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip _putStoneClip;
        [SerializeField] private AudioClip _reverseStonesClip;
        [SerializeField] private AudioClip _extendClip;
        [SerializeField] private AudioClip _frozenClip;
        [SerializeField] private AudioClip _reverseClip;
        [SerializeField] private AudioClip _delayReverseClip;
        [SerializeField] private AudioClip _gameOverClip;

        private AudioSource _audioSource;

        [Inject]
        private void Construct(
            ISubscriber<RequestPutStoneMessage> putStoneSubscriber,
            ISubscriber<BoardInfo> boardInfoSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber)
        {
            _audioSource = GetComponent<AudioSource>();

            // 各メッセージに対応するメソッドを登録
            putStoneSubscriber.Subscribe(OnPutStone);
            boardInfoSubscriber.Subscribe(OnBoardInfo);
            gameOverSubscriber.Subscribe(OnGameOver);
        }

        private void OnPutStone(RequestPutStoneMessage msg)
        {
            if (_putStoneClip != null)
            {
                _audioSource.PlayOneShot(_putStoneClip);
            }
        }

        private void OnBoardInfo(BoardInfo info)
        {
            // ひっくり返した時の音
            if (info.ReversePos.Count > 0 && _reverseStonesClip != null)
            {
                // 複数の石が反転しても音は1回だけ鳴らす
                _audioSource.PlayOneShot(_reverseStonesClip);
            }

            // 特殊石を使った時の音
            switch (info.PutType)
            {
                case StoneType.Extend:
                    if (_extendClip != null) _audioSource.PlayOneShot(_extendClip);
                    break;
                case StoneType.Frozen:
                    if (_frozenClip != null) _audioSource.PlayOneShot(_frozenClip);
                    break;
                //case StoneType.Broken:
                //case StoneType.Collapse:
                case StoneType.Reverse:
                    if (_reverseClip != null) _audioSource.PlayOneShot(_frozenClip);
                    break;
                case StoneType.DelayReverse:
                    if (_delayReverseClip != null) _audioSource.PlayOneShot(_frozenClip);
                    break;
            }
        }

        private void OnGameOver(GameOverMessage msg)
        {
            if (_gameOverClip != null)
            {
                _audioSource.PlayOneShot(_gameOverClip);
            }
        }
    }
}