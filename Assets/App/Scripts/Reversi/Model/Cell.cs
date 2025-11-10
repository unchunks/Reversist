using UnityEngine;
using TMPro;
using static UnityEngine.Rendering.DebugUI.Table;
using Cysharp.Threading.Tasks;

namespace App.Reversi
{
	public class Cell : MonoBehaviour
	{
		[SerializeField] private Stone _stone;
		[SerializeField] private Nail _nail;
		[SerializeField] private Highlight _highlight;
		[SerializeField] private TextMeshPro _countText;

		public StoneColor Color => _stone.Color;
		public StoneType Type => _stone.Type;
		public bool isPlased { get; private set; }
		public int Row { get; private set; }
		public int Col { get; private set; }

		private void Awake()
		{
			_stone.gameObject.SetActive(false);
			_nail.gameObject.SetActive(false);
			_countText.gameObject.SetActive(false);
			_highlight.gameObject.SetActive(false);

			// 誤差防止のために+0.1
			Row = (int)(transform.position.z + 0.1);
			Col = (int)(transform.position.x + 0.1);
			gameObject.name = $"{Row}-{Col}";
		}

		public void SetHighlight(bool isOn)
		{
			if (isPlased && isOn)
			{
				Debug.LogError($"{Row}-{Col}: 石がある場所にハイライトを設置しようとしています");
				return;
			}
			_highlight.gameObject.SetActive(isOn);
		}

		public async UniTask Put(StoneColor color, StoneType type)
		{
			var token = this.GetCancellationTokenOnDestroy();
			token.ThrowIfCancellationRequested();

			isPlased = true;

			_highlight.gameObject.SetActive(false);
			_stone.gameObject.SetActive(true);
			await _stone.Put(color, type);

			if (token.IsCancellationRequested) return;

			switch (type)
			{
				case StoneType.Frozen:
					_nail.gameObject.SetActive(true);
					_nail.Pin();
					break;
				case StoneType.DelayReverse:
					_countText.gameObject.SetActive(true);
					break;
			}
		}

		public async UniTask Flip()
		{
			_countText.gameObject.SetActive(false);
			await _stone.Flip();
		}
	}
}