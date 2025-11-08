using App.Reversi.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace App.Reversi.AI
{
    /// <summary>
    /// Unityに依存しない、純粋なゲーム状態を保持するクラス
    /// ほかのクラスと重複しているコードが多いため、のちに統一する
    /// </summary>
    public class GameState
    {
        public const int MAX_BOARD_SIZE = 12;
        public const int DEF_BOARD_SIZE = 8;
        public const int DELAY_COUNT = 1;

        // 盤面の状態
        public StoneColor[,] Board { get; set; }
        public StoneType[,] StoneTypes { get; set; }
        public int CurrentBoardSize { get; set; }
        public List<ReverseCountDown> DelayReverseStack { get; set; }

        // プレイヤーの状態
        public StoneColor CurrentPlayer { get; set; }
        public Dictionary<StoneColor, AvailableStoneCount> Inventories { get; set; }

        // 石の数（評価関数用）
        public Dictionary<StoneColor, int> StoneCount { get; set; }

        // ゲーム終了判定用
        public bool IsGameOver { get; set; }

        public GameState()
        {
            // 盤面の初期化
            Board = new StoneColor[MAX_BOARD_SIZE, MAX_BOARD_SIZE];
            StoneTypes = new StoneType[MAX_BOARD_SIZE, MAX_BOARD_SIZE];
            for (int r = 0; r < MAX_BOARD_SIZE; r++)
            {
                for (int c = 0; c < MAX_BOARD_SIZE; c++)
                {
                    Board[r, c] = StoneColor.None;
                    StoneTypes[r, c] = StoneType.None;
                }
            }
            CurrentBoardSize = DEF_BOARD_SIZE;
            DelayReverseStack = new List<ReverseCountDown>();

            // プレイヤーの初期化
            CurrentPlayer = StoneColor.Black;
            IsGameOver = false;

            // 在庫の初期化 
            Inventories = new Dictionary<StoneColor, AvailableStoneCount>
            {
                [StoneColor.Black] = new AvailableStoneCount(new Dictionary<StoneType, int>
                {
                    { StoneType.Normal, 61 },
                    { StoneType.Extend, 1 },
                    { StoneType.Frozen, 1 },
                    { StoneType.Reverse, 5 },
                    { StoneType.DelayReverse, 5 }
                }),
                [StoneColor.White] = new AvailableStoneCount(new Dictionary<StoneType, int>
                {
                    { StoneType.Normal, 61 },
                    { StoneType.Extend, 1 },
                    { StoneType.Frozen, 1 },
                    { StoneType.Reverse, 5 },
                    { StoneType.DelayReverse, 5 }
                })
            };

            // 石の数の初期化
            StoneCount = new Dictionary<StoneColor, int>
            {
                { StoneColor.Black, 2 }, { StoneColor.White, 2 }
            };

            // 初期配置 (Board.cs より)
            Board[5, 5] = StoneColor.Black;
            Board[6, 6] = StoneColor.Black;
            Board[6, 5] = StoneColor.White;
            Board[5, 6] = StoneColor.White;
            StoneTypes[5, 5] = StoneType.Normal;
            StoneTypes[6, 6] = StoneType.Normal;
            StoneTypes[6, 5] = StoneType.Normal;
            StoneTypes[5, 6] = StoneType.Normal;
        }

        /// <summary>
        /// MCTS用のディープコピーコンストラクタ
        /// </summary>
        public GameState(GameState source)
        {
            // プリミティブ型のコピー
            CurrentBoardSize = source.CurrentBoardSize;
            CurrentPlayer = source.CurrentPlayer;
            IsGameOver = source.IsGameOver;

            // 配列のディープコピー
            Board = (StoneColor[,])source.Board.Clone();
            StoneTypes = (StoneType[,])source.StoneTypes.Clone();

            // 辞書のディープコピー
            StoneCount = new Dictionary<StoneColor, int>(source.StoneCount);

            // AvailableStoneCount (カスタムクラス) のディープコピー
            Inventories = new Dictionary<StoneColor, AvailableStoneCount>();
            foreach (var inv in source.Inventories)
            {
                Inventories.Add(inv.Key, new AvailableStoneCount(inv.Value));
            }

            // リストのディープコピー
            DelayReverseStack = new List<ReverseCountDown>();
            foreach (var reverseCountDown in source.DelayReverseStack)
            {
                // 各要素を新しいオブジェクトとしてコピー
                DelayReverseStack.Add(new ReverseCountDown(reverseCountDown));
            }
        }
    }
}