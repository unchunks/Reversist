using App.Reversi.Messaging;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
    /// <summary>
    /// 各プレイヤーの石の在庫を管理する
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Inject] private IPublisher<AvailableCountChangedMessage> _countChangedPublisher;
        [Inject] private ISubscriber<RequestPutStoneMessage> _requestSubscriber;

        private Dictionary<StoneColor, AvailableStoneCount> _availableCount;

        private void Start()
        {
            InitializeStock();
            _requestSubscriber.Subscribe(OnPutRequest);
        }

        /// <summary>
        /// 在庫を初期化し、UIに通知する
        /// </summary>
        public void InitializeStock()
        {
            _availableCount = new Dictionary<StoneColor, AvailableStoneCount>();
            _availableCount[StoneColor.Black] = new AvailableStoneCount(new Dictionary<StoneType, int>
            {
                { StoneType.Normal, 61 },
                { StoneType.Extend, 1 },
                { StoneType.Frozen, 1 },
                { StoneType.Reverse, 5 },
                { StoneType.DelayReverse, 5 }
            });
            _availableCount[StoneColor.White] = new AvailableStoneCount(new Dictionary<StoneType, int>
            {
                { StoneType.Normal, 61 },
                { StoneType.Extend, 1 },
                { StoneType.Frozen, 1 },
                { StoneType.Reverse, 5 },
                { StoneType.DelayReverse, 5 }
            });

            // UIを初期状態に戻すためにメッセージを発行
            foreach (var dic in _availableCount)
            {
                foreach (var count in dic.Value)
                {
                    _countChangedPublisher.Publish(new AvailableCountChangedMessage(dic.Key, count.Key, count.Value));
                }
            }
        }

        /// <summary>
        /// 石の配置リクエストに応じて在庫を消費する
        /// </summary>
        private void OnPutRequest(RequestPutStoneMessage msg)
        {
            bool couldDecrease = _availableCount[msg.Player].Decrease(msg.Type);
            if (couldDecrease)
            {
                int count = _availableCount[msg.Player].AvailableCount[msg.Type];
                _countChangedPublisher.Publish(new AvailableCountChangedMessage(msg.Player, msg.Type, count));
            }
        }

        /// <summary>
        /// 指定した石の在庫が残っているか確認する
        /// </summary>
        public bool HasStock(StoneColor player, StoneType type)
        {
            if (_availableCount == null || !_availableCount.ContainsKey(player)) return false;
            return _availableCount[player].AvailableCount[type] > 0;
        }
    }
}