using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine; // For Debug.Log

namespace App.Reversi.AI
{
	#region Bitboard Struct
	public struct Bitboard
	{
		public ulong p0, p1, p2;

		public static readonly Bitboard Empty = new Bitboard(0, 0, 0);

		public Bitboard(ulong p0, ulong p1, ulong p2)
		{
			this.p0 = p0;
			this.p1 = p1;
			this.p2 = p2;
			// This is a temporary debug log to trace a constructor bug.
			if ((p0 | p1 | p2) != 0) // Log only for non-empty bitboards to reduce spam
			{
				Debug.Log($"[Bitboard.ctor] p0={p0:X16}, p1={p1:X16}, p2={p2:X8}");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Bitboard operator &(Bitboard a, Bitboard b) => new Bitboard(a.p0 & b.p0, a.p1 & b.p1, a.p2 & b.p2);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Bitboard operator |(Bitboard a, Bitboard b) => new Bitboard(a.p0 | b.p0, a.p1 | b.p1, a.p2 | b.p2);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Bitboard operator ^(Bitboard a, Bitboard b) => new Bitboard(a.p0 ^ b.p0, a.p1 ^ b.p1, a.p2 ^ b.p2);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Bitboard operator ~(Bitboard a) => new Bitboard(~a.p0, ~a.p1, ~a.p2);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsZero() => (p0 | p1 | p2) == 0;

		public static Bitboard FromStones(ulong[] stones) => new Bitboard(stones[0], stones[1], stones[2]);
		public void ToStones(ulong[] stones) { stones[0] = p0; stones[1] = p1; stones[2] = p2; }

		public static Bitboard Shift(Bitboard b, int s)
		{
			if (s == 0) return b;
			if (s > 0)
			{
				if (s >= 64) { s -= 64; return new Bitboard(0, b.p0 << s, (b.p1 << s) | (b.p0 >> (64 - s))); }
				return new Bitboard(b.p0 << s, (b.p1 << s) | (b.p0 >> (64 - s)), (b.p2 << s) | (b.p1 >> (64 - s)));
			}
			s = -s;
			if (s >= 64) { s -= 64; return new Bitboard((b.p1 >> s) | (b.p2 << (64 - s)), b.p2 >> s, 0); }
			return new Bitboard((b.p0 >> s) | (b.p1 << (64 - s)), (b.p1 >> s) | (b.p2 << (64 - s)), b.p2 >> s);
		}

		public int Count() => CountSetBits(p0) + CountSetBits(p1) + CountSetBits(p2);
		private static int CountSetBits(ulong n) { int c = 0; while (n > 0) { n &= (n - 1); c++; } return c; }
	}
	#endregion

	public static class OptimizedSimulator
	{
		private const int Size = 12;
		private static readonly Bitboard MaskEdge;
		private static readonly Bitboard MaskA = CreateFileMask(0);
		private static readonly Bitboard MaskL = CreateFileMask(Size - 1);

		static OptimizedSimulator()
		{
			ulong p0 = 0, p1 = 0, p2 = 0;
			for (int i = 0; i < Size * Size; i++)
			{
				if (i < 64) p0 |= 1UL << i;
				else if (i < 128) p1 |= 1UL << (i - 64);
				else p2 |= 1UL << (i - 128);
			}
			MaskEdge = new Bitboard(p0, p1, p2);
		}

		private static Bitboard CreateFileMask(int file)
		{
			ulong p0 = 0, p1 = 0, p2 = 0;
			for (int r = 0; r < Size; r++)
			{
				int index = r * Size + file;
				if (index < 64) p0 |= 1UL << index;
				else if (index < 128) p1 |= 1UL << (index - 64);
				else p2 |= 1UL << (index - 128);
			}
			return new Bitboard(p0, p1, p2);
		}

		public static List<GameAction> GetValidActions(GameState state)
		{
			if (state.ValidActionsCache != null) return state.ValidActionsCache;

			var actions = new List<GameAction>(64);
			var inventory = state.CurrentPlayer == StoneColor.Black ? state.BlackInventory : state.WhiteInventory;

			var playerBoard = Bitboard.FromStones(state.CurrentPlayer == StoneColor.Black ? state.BlackStones : state.WhiteStones);
			var opponentBoard = Bitboard.FromStones(state.CurrentPlayer == StoneColor.Black ? state.WhiteStones : state.BlackStones);

			for (int typeIdx = 0; typeIdx < 5; typeIdx++)
			{
				if (inventory[typeIdx] <= 0) continue;
				var type = (StoneType)typeIdx;

				Bitboard legalMoves = type.IsReverseType() ? GetLegalMoves(opponentBoard, playerBoard) : GetLegalMoves(playerBoard, opponentBoard);

				for (int i = 0; i < Size * Size; i++)
				{
					int arrayIndex = i / 64;
					int bitIndex = i % 64;
					ulong p = arrayIndex == 0 ? legalMoves.p0 : (arrayIndex == 1 ? legalMoves.p1 : legalMoves.p2);
					if (((p >> bitIndex) & 1) == 1)
					{
						actions.Add(GameAction.Create(new Position(i / Size, i % Size), type, state.CurrentPlayer));
					}
				}
			}

			state.ValidActionsCache = actions;
			return actions;
		}

		private static Bitboard GetLegalMoves(Bitboard playerBoard, Bitboard opponentBoard)
		{
			Bitboard empty = ~(playerBoard | opponentBoard);
			Bitboard legal = Bitboard.Empty;
			legal |= CheckDirection(playerBoard, opponentBoard, empty, 1, ~MaskL);
			legal |= CheckDirection(playerBoard, opponentBoard, empty, -1, ~MaskA);
			legal |= CheckDirection(playerBoard, opponentBoard, empty, Size, Bitboard.Empty);
			legal |= CheckDirection(playerBoard, opponentBoard, empty, -Size, Bitboard.Empty);
			legal |= CheckDirection(playerBoard, opponentBoard, empty, Size + 1, ~MaskL);
			legal |= CheckDirection(playerBoard, opponentBoard, empty, Size - 1, ~MaskA);
			legal |= CheckDirection(playerBoard, opponentBoard, empty, -Size + 1, ~MaskL);
			legal |= CheckDirection(playerBoard, opponentBoard, empty, -Size - 1, ~MaskA);
			return legal & MaskEdge;
		}

		private static Bitboard CheckDirection(Bitboard player, Bitboard opp, Bitboard empty, int dir, Bitboard mask)
		{
			Bitboard maskedOpp = mask.IsZero() ? opp : opp & mask;
			Bitboard flipped = Bitboard.Shift(player, dir) & maskedOpp;
			for (int i = 0; i < Size - 2; i++) flipped |= Bitboard.Shift(flipped, dir) & maskedOpp;
			return Bitboard.Shift(flipped, dir) & empty;
		}

		public static void ExecuteActionInPlace(GameState state, GameAction action)
		{
			ProcessDelayReverse(state);

			if (action != null)
			{
				int index = action.Position.Row * Size + action.Position.Col;
				Bitboard moveBit = new Bitboard(index < 64 ? 1UL << index : 0, index >= 64 && index < 128 ? 1UL << (index - 64) : 0, index >= 128 ? 1UL << (index - 128) : 0);

				StoneColor playerColor = action.Player;
				StoneColor putColor = action.Type.IsReverseType() ? playerColor.Opponent() : playerColor;

				ref ulong[] playerStones = ref (putColor == StoneColor.Black ? ref state.BlackStones : ref state.WhiteStones);
				ref ulong[] opponentStones = ref (putColor == StoneColor.Black ? ref state.WhiteStones : ref state.BlackStones);

				Bitboard playerBoard = Bitboard.FromStones(playerStones);
				Bitboard opponentBoard = Bitboard.FromStones(opponentStones);

				Bitboard flipped = GetFlippedStones(playerBoard, opponentBoard, moveBit);

				playerBoard |= flipped | moveBit;
				opponentBoard &= ~flipped;

				playerBoard.ToStones(playerStones);
				opponentBoard.ToStones(opponentStones);

				(playerColor == StoneColor.Black ? state.BlackInventory : state.WhiteInventory)[(int)action.Type]--;
				SetStoneType(state, action.Position.Row, action.Position.Col, action.Type);

				switch (action.Type)
				{
					case StoneType.Extend:
						state.CurrentBoardSize = Math.Min(state.CurrentBoardSize + 2, Size);
						break;
					case StoneType.Reverse:
						Bitboard selfAndNeighbors = moveBit | GetNeighbors(moveBit);
						Bitboard originalPlayer = Bitboard.FromStones(playerStones);
						Bitboard originalOpponent = Bitboard.FromStones(opponentStones);

						Bitboard toPlayer = originalOpponent & selfAndNeighbors;
						Bitboard toOpponent = originalPlayer & selfAndNeighbors;

						originalPlayer = (originalPlayer & ~toOpponent) | toPlayer;
						originalOpponent = (originalOpponent & ~toPlayer) | toOpponent;

						originalPlayer.ToStones(playerStones);
						originalOpponent.ToStones(opponentStones);
						break;
					case StoneType.DelayReverse:
						state.DelayReverseStack.Add(new ReverseCountDown(action.Position, 5)); // Hardcoded turn count
						break;
				}
			}

			state.CurrentPlayer = state.CurrentPlayer.Opponent();
			state.ValidActionsCache = null;
			if (GetValidActions(state).Count == 0)
			{
				state.CurrentPlayer = state.CurrentPlayer.Opponent();
				state.ValidActionsCache = null;
				if (GetValidActions(state).Count == 0) state.IsGameOver = true;
			}

			state.BlackCount = Bitboard.FromStones(state.BlackStones).Count();
			state.WhiteCount = Bitboard.FromStones(state.WhiteStones).Count();
		}

		private static void ProcessDelayReverse(GameState state)
		{
			state.DelayReverseStack.RemoveAll(item =>
			{
				item.Delay--;
				if (item.Delay > 0) return false;

				int index = item.Pos.Row * Size + item.Pos.Col;
				Bitboard moveBit = new Bitboard(index < 64 ? 1UL << index : 0, index >= 64 && index < 128 ? 1UL << (index - 64) : 0, index >= 128 ? 1UL << (index - 128) : 0);

				bool isBlack = (Bitboard.FromStones(state.BlackStones) & moveBit).IsZero();
				ref ulong[] playerStones = ref (isBlack ? ref state.WhiteStones : ref state.BlackStones);
				ref ulong[] opponentStones = ref (isBlack ? ref state.BlackStones : ref state.WhiteStones);

				Bitboard playerBoard = Bitboard.FromStones(playerStones);
				Bitboard opponentBoard = Bitboard.FromStones(opponentStones);

				Bitboard flipped = GetFlippedStones(playerBoard, opponentBoard, moveBit);
				playerBoard |= flipped | moveBit;
				opponentBoard &= ~flipped;

				playerBoard.ToStones(playerStones);
				opponentBoard.ToStones(opponentStones);

				return true;
			});
		}

		private static Bitboard GetFlippedStones(Bitboard player, Bitboard opp, Bitboard move)
		{
			Bitboard flipped = Bitboard.Empty;
			flipped |= FlipDirection(player, opp, move, 1, ~MaskL);
			flipped |= FlipDirection(player, opp, move, -1, ~MaskA);
			flipped |= FlipDirection(player, opp, move, Size, Bitboard.Empty);
			flipped |= FlipDirection(player, opp, move, -Size, Bitboard.Empty);
			flipped |= FlipDirection(player, opp, move, Size + 1, ~MaskL);
			flipped |= FlipDirection(player, opp, move, Size - 1, ~MaskA);
			flipped |= FlipDirection(player, opp, move, -Size + 1, ~MaskL);
			flipped |= FlipDirection(player, opp, move, -Size - 1, ~MaskA);
			return flipped;
		}

		private static Bitboard FlipDirection(Bitboard player, Bitboard opp, Bitboard move, int dir, Bitboard mask)
		{
			Bitboard maskedOpp = mask.IsZero() ? opp : opp & mask;
			Bitboard line = Bitboard.Empty;
			Bitboard tempMove = Bitboard.Shift(move, dir) & maskedOpp;
			for (int i = 0; i < Size - 1; i++)
			{
				if (tempMove.IsZero()) break;
				line |= tempMove;
				tempMove = Bitboard.Shift(tempMove, dir) & maskedOpp;
			}
			if ((Bitboard.Shift(line, dir) & player).IsZero()) return Bitboard.Empty;
			return line;
		}

		private static Bitboard GetNeighbors(Bitboard b)
		{
			Bitboard n = Bitboard.Empty;
			n |= Bitboard.Shift(b, 1) & ~MaskA;
			n |= Bitboard.Shift(b, -1) & ~MaskL;
			n |= Bitboard.Shift(b, Size);
			n |= Bitboard.Shift(b, -Size);
			n |= Bitboard.Shift(b, Size + 1) & ~MaskA;
			n |= Bitboard.Shift(b, Size - 1) & ~MaskL;
			n |= Bitboard.Shift(b, -Size + 1) & ~MaskA;
			n |= Bitboard.Shift(b, -Size - 1) & ~MaskL;
			return n;
		}

		private static void SetStoneType(GameState state, int row, int col, StoneType type)
		{
			int index = row * Size + col;
			int arrayIdx = index / 64;
			int bitIdx = index % 64;
			ulong mask = 1UL << bitIdx;

			state.StoneTypeBits0[arrayIdx] &= ~mask;
			state.StoneTypeBits1[arrayIdx] &= ~mask;
			state.StoneTypeBits2[arrayIdx] &= ~mask;

			int typeInt = (int)type;
			if ((typeInt & 1) != 0) state.StoneTypeBits0[arrayIdx] |= mask;
			if ((typeInt & 2) != 0) state.StoneTypeBits1[arrayIdx] |= mask;
			if ((typeInt & 4) != 0) state.StoneTypeBits2[arrayIdx] |= mask;
		}

		public static float GetResult(GameState state)
		{
			if (!state.IsGameOver) return 0.0f;
			state.BlackCount = Bitboard.FromStones(state.BlackStones).Count();
			state.WhiteCount = Bitboard.FromStones(state.WhiteStones).Count();
			if (state.BlackCount > state.WhiteCount) return 1.0f;
			if (state.WhiteCount > state.BlackCount) return -1.0f;
			return 0.5f;
		}
	}
}