namespace App.Reversi.AI
{
	/// <summary>
	/// AIが実行可能な1つの行動（手）を定義する
	/// </summary>
	public class GameAction
	{
		public readonly Position Position;
		public readonly StoneType Type;
		public readonly StoneColor Player;

		private static readonly ObjectPool<GameAction> _pool = new ObjectPool<GameAction>(
			() => new GameAction(default, default, default),
			1000);

		public GameAction(Position position, StoneType type, StoneColor player)
		{
			Position = position;
			Type = type;
			Player = player;
		}

		public static GameAction Create(Position position, StoneType type, StoneColor player)
		{
			var action = _pool.Get();
			// 読み取り専用フィールドを回避するため、新しいインスタンスを返す
			return new GameAction(position, type, player);
		}

		public void Release()
		{
			_pool.Return(this);
		}
	}
}
