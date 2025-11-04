using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace App.Reversi
{
    public class Stone : MonoBehaviour
    {
        [Inject] private IPublisher<PlaySoundEffectMessage> _soundPublisher;

        public StoneColor Color { get; private set; }
        public StoneType Type { get; private set; }

        public Stone()
        {
            Color = StoneColor.None;
            Type = StoneType.None;
        }

        public async UniTask Put(StoneColor color, StoneType type)
        {
            Color = color;
            Type = type;

            transform.localRotation = Quaternion.Euler(GetStateRotation(Color));

            transform.localPosition = new Vector3(0, 2, 0);
            gameObject.transform.localRotation = Quaternion.Euler(GetStateRotation(Color));

            _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.PutStone));

            await transform.DOLocalMoveY(0, 0.3f)
                            .SetEase(Ease.OutQuart)
                            .ToUniTask();
            transform.localPosition = new Vector3(0, 0, 0);

            switch (type)
            {
                case StoneType.Frozen:
                    _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.Frozen));
                    break;
                case StoneType.DelayReverse:
                    _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.DelayReverse));
                    break;
            }
        }

        public async UniTask Flip()
        {
            if (Type == StoneType.Frozen)
            {
                await PlayFrozenAnim();
                return;
            }

            Color = Color.Opponent();
            var angle = GetStateRotation(Color);

            _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.Flip));

            await UniTask.WhenAll(
                transform.DOLocalMoveY(0.5f, 0.2f)
                        .SetEase(Ease.InOutQuart)
                        .SetLoops(2, LoopType.Yoyo)
                        .ToUniTask(),

                transform.DOLocalRotate(angle, 0.2f)
                        .SetEase(Ease.InOutQuart)
                        .ToUniTask()
            );
            transform.localPosition = new Vector3(0, 0, 0);
            transform.localRotation = Quaternion.Euler(angle);
        }

        private async UniTask PlayFrozenAnim()
        {
            _soundPublisher.Publish(new PlaySoundEffectMessage(SoundEffectType.FrozenFlip));

            await transform.DOLocalMoveX(0.05f, 0.05f)
                        .SetEase(Ease.InOutQuart)
                        .SetLoops(4, LoopType.Yoyo)
                        .ToUniTask();
            transform.localPosition = new Vector3(0, 0, 0);
        }

        public async UniTask PlayTwitchAnim()
        {
            await transform.DOLocalMoveY(0.3f, 0.1f)
                        .SetEase(Ease.InOutQuart)
                        .SetLoops(2, LoopType.Yoyo)
                        .ToUniTask();
            transform.localPosition = new Vector3(0, 0, 0);
        }

        private Vector3 GetStateRotation(StoneColor color)
        {
            return color switch
            {
                StoneColor.Black => new Vector3(90, 0, 0),
                StoneColor.White => new Vector3(-90, 0, 0),
                _ => Vector3.zero,
            };
        }
    }
}