using System;
using System.Collections.Concurrent;

namespace App.Reversi.AI
{
	/// <summary>
	/// メモリアロケーション削減のため
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ObjectPool<T> where T : class
	{
		private readonly ConcurrentBag<T> _objects;
		private readonly Func<T> _factory;
		private readonly int _maxSize;

		public ObjectPool(Func<T> factory, int maxSize = 1000)
		{
			_factory = factory;
			_maxSize = maxSize;
			_objects = new ConcurrentBag<T>();
		}

		public T Get()
		{
			return _objects.TryTake(out T item) ? item : _factory();
		}

		public void Return(T item)
		{
			if (_objects.Count < _maxSize)
			{
				_objects.Add(item);
			}
		}
	}
}
