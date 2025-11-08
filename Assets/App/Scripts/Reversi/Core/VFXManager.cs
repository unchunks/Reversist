using App.Reversi.Messaging;
using MessagePipe;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
    public class VFXManager : MonoBehaviour
    {
        [Header("VFX Prefabs")]
        [SerializeField] private ParticleSystem _putEffectPrefab;
        [SerializeField] private ParticleSystem _flipEffectPrefab;
        [SerializeField] private ParticleSystem _frozenFlipEffectPrefab;
        [SerializeField] private ParticleSystem _extendEffectPrefab;
        [SerializeField] private ParticleSystem _frozenEffectPrefab;
        [SerializeField] private ParticleSystem _reverseEffectPrefab;
        [SerializeField] private ParticleSystem _delayReverseEffectPrefab;


        // VContainerによるDI
        [Inject]
        private void Construct(ISubscriber<PlayVFXMessage> vfxSubscriber)
        {
            vfxSubscriber.Subscribe(OnPlayVFX);
        }

        /// <summary>
        /// VFX再生メッセージを受け取った時の処理
        /// </summary>
        private void OnPlayVFX(PlayVFXMessage msg)
        {
            ParticleSystem prefabToSpawn = null;
            switch (msg.Type)
            {
                case VFXType.PutStone:
                    prefabToSpawn = _putEffectPrefab;
                    break;
                case VFXType.Flip:
                    prefabToSpawn = _flipEffectPrefab;
                    break;
                case VFXType.FrozenFlip:
                    prefabToSpawn = _frozenFlipEffectPrefab;
                    break;
                case VFXType.Extend:
                    prefabToSpawn = _extendEffectPrefab;
                    break;
                case VFXType.Frozen:
                    prefabToSpawn = _frozenEffectPrefab;
                    break;
                //case Broken:
                //case Collapse:
                case VFXType.Reverse:
                    prefabToSpawn = _reverseEffectPrefab;
                    break;
                case VFXType.DelayReverse:
                    prefabToSpawn = _delayReverseEffectPrefab;
                    break;
            }

            if (prefabToSpawn != null)
            {
                Quaternion rotation = prefabToSpawn.transform.rotation;
                // プレハブをメッセージで指定された位置に生成
                Instantiate(prefabToSpawn, msg.Position, rotation);
            }
        }
    }
}
