using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace App.Reversi.AI
{
	public class GameState
	{
		public const int MAX_BOARD_SIZE = 12;
		public const int TOTAL_CELLS = MAX_BOARD_SIZE * MAX_BOARD_SIZE; // 144
		public const int BITBOARD_UINT64_COUNT = 3; // 144 / 64 = 3

		// ビットボードで石の配置を管理
		public ulong[] BlackStones;
		public ulong[] WhiteStones;

		// StoneTypeもビットボードで管理 (5種類 = 3ビット必要)
		// Bit0-1: Type の下位2ビット, Bit2: Type の最上位ビット
		public ulong[] StoneTypeBits0; // 1ビット目
		public ulong[] StoneTypeBits1; // 2ビット目
		public ulong[] StoneTypeBits2; // 3ビット目

		public int CurrentBoardSize;
		public StoneColor CurrentPlayer;
		public bool IsGameOver;

		// 石の数をキャッシュ
		public int BlackCount;
		public int WhiteCount;

		// インベントリ(簡易版 - 配列で管理)
		public int[] BlackInventory; // [Normal, Extend, Frozen, Reverse, DelayReverse]
		public int[] WhiteInventory;

		// 遅延反転スタック
		public List<ReverseCountDown> DelayReverseStack;

		// キャッシュ
		public List<GameAction> ValidActionsCache;
		private ulong _stateHash; // Zobrist Hashing用

		private static readonly ObjectPool<GameState> _pool =
			new ObjectPool<GameState>(() => new GameState(), 10000);

		public GameState()
		{
			BlackStones = new ulong[BITBOARD_UINT64_COUNT];
			WhiteStones = new ulong[BITBOARD_UINT64_COUNT];
			StoneTypeBits0 = new ulong[BITBOARD_UINT64_COUNT];
			StoneTypeBits1 = new ulong[BITBOARD_UINT64_COUNT];
			StoneTypeBits2 = new ulong[BITBOARD_UINT64_COUNT];

			BlackInventory = new int[5];
			WhiteInventory = new int[5];
			DelayReverseStack = new List<ReverseCountDown>(8);

			InitializeDefault();
		}

		public void InitializeDefault()
		{
			Array.Clear(BlackStones, 0, BITBOARD_UINT64_COUNT);
			Array.Clear(WhiteStones, 0, BITBOARD_UINT64_COUNT);
			Array.Clear(StoneTypeBits0, 0, BITBOARD_UINT64_COUNT);
			Array.Clear(StoneTypeBits1, 0, BITBOARD_UINT64_COUNT);
			Array.Clear(StoneTypeBits2, 0, BITBOARD_UINT64_COUNT);

			CurrentBoardSize = 8;
			CurrentPlayer = StoneColor.Black;
			IsGameOver = false;
			BlackCount = 2;
			WhiteCount = 2;
			ValidActionsCache = null;

			// インベントリ初期化
			BlackInventory[0] = 61; // Normal
			BlackInventory[1] = 1;  // Extend
			BlackInventory[2] = 1;  // Frozen
			BlackInventory[3] = 5;  // Reverse
			BlackInventory[4] = 5;  // DelayReverse

			Array.Copy(BlackInventory, WhiteInventory, 5);

			DelayReverseStack.Clear();

			// 初期配置
			SetStone(5, 5, StoneColor.Black, StoneType.Normal);
			SetStone(6, 6, StoneColor.Black, StoneType.Normal);
			SetStone(6, 5, StoneColor.White, StoneType.Normal);
			SetStone(5, 6, StoneColor.White, StoneType.Normal);

			_stateHash = ComputeHash();
		}

		public void CopyFrom(GameState source)
		{
			Array.Copy(source.BlackStones, BlackStones, BITBOARD_UINT64_COUNT);
			Array.Copy(source.WhiteStones, WhiteStones, BITBOARD_UINT64_COUNT);
			Array.Copy(source.StoneTypeBits0, StoneTypeBits0, BITBOARD_UINT64_COUNT);
			Array.Copy(source.StoneTypeBits1, StoneTypeBits1, BITBOARD_UINT64_COUNT);
			Array.Copy(source.StoneTypeBits2, StoneTypeBits2, BITBOARD_UINT64_COUNT);

			CurrentBoardSize = source.CurrentBoardSize;
			CurrentPlayer = source.CurrentPlayer;
			IsGameOver = source.IsGameOver;
			BlackCount = source.BlackCount;
			WhiteCount = source.WhiteCount;

			Array.Copy(source.BlackInventory, BlackInventory, 5);
			Array.Copy(source.WhiteInventory, WhiteInventory, 5);

			DelayReverseStack.Clear();
			DelayReverseStack.AddRange(source.DelayReverseStack);

			ValidActionsCache = null;
			_stateHash = source._stateHash;
		}

		public static GameState GetFromPool()
		{
			var state = _pool.Get();
			state.InitializeDefault();
			return state;
		}

		public void ReturnToPool()
		{
			ValidActionsCache?.Clear();
			_pool.Return(this);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SetStone(int row, int col, StoneColor color, StoneType type)
		{
			int index = row * MAX_BOARD_SIZE + col;
			int arrayIdx = index / 64;
			int bitIdx = index % 64;
			ulong mask = 1UL << bitIdx;

			if (color == StoneColor.Black)
			{
				BlackStones[arrayIdx] |= mask;
			}
			else
			{
				WhiteStones[arrayIdx] |= mask;
			}

			// StoneTypeを3ビットで格納
			int typeInt = (int)type;
			if ((typeInt & 1) != 0) StoneTypeBits0[arrayIdx] |= mask;
			if ((typeInt & 2) != 0) StoneTypeBits1[arrayIdx] |= mask;
			if ((typeInt & 4) != 0) StoneTypeBits2[arrayIdx] |= mask;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public StoneColor GetStone(int row, int col)
		{
			int index = row * MAX_BOARD_SIZE + col;
			int arrayIdx = index / 64;
			int bitIdx = index % 64;
			ulong mask = 1UL << bitIdx;

			if ((BlackStones[arrayIdx] & mask) != 0) return StoneColor.Black;
			if ((WhiteStones[arrayIdx] & mask) != 0) return StoneColor.White;
			return StoneColor.None;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public StoneType GetStoneType(int row, int col)
		{
			int index = row * MAX_BOARD_SIZE + col;
			int arrayIdx = index / 64;
			int bitIdx = index % 64;
			ulong mask = 1UL << bitIdx;

			int typeInt = 0;
			if ((StoneTypeBits0[arrayIdx] & mask) != 0) typeInt |= 1;
			if ((StoneTypeBits1[arrayIdx] & mask) != 0) typeInt |= 2;
			if ((StoneTypeBits2[arrayIdx] & mask) != 0) typeInt |= 4;

			return (StoneType)typeInt;
		}

		private ulong ComputeHash()
		{
			// 簡易的なZobrist Hashing
			ulong hash = 0;
			for (int i = 0; i < BITBOARD_UINT64_COUNT; i++)
			{
				hash ^= BlackStones[i] * 31UL;
				hash ^= WhiteStones[i] * 37UL;
			}
			return hash;
		}

		public ulong GetStateHash() => _stateHash;
	}
}
