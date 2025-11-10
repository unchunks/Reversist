using App.Reversi.Messaging;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
	/// <summary>
	/// UI以外へのユーザーの入力（盤面クリックなど）を管理する
	/// </summary>
	public class InputManager : MonoBehaviour
	{
		//[SerializeField] private Camera _mainCam;
		[SerializeField] private LayerMask _hitLayer;

		[Inject] private IPublisher<CellClickedMessage> _cellClickedPublisher;

		private Camera _mainCam;
		private bool _isInputActive = true;
		public void SetInputActive(bool isActive) => _isInputActive = isActive;

		private void Awake()
		{
			_mainCam = Camera.main;
		}

		private void Update()
		{
			if (_isInputActive && Input.GetMouseButtonDown(0) && _mainCam != null)
			{
				Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);

				// 置ける場所に入力があったら
				if (Physics.Raycast(ray, out RaycastHit hitInfo, 20f, _hitLayer))
				{
					Vector3 impact = new Vector3(hitInfo.point.z, hitInfo.point.y, hitInfo.point.x);
					Position boardPos = SceneToBoardPos(impact);
					_cellClickedPublisher.Publish(new CellClickedMessage(boardPos));
				}
			}
		}

		private Position SceneToBoardPos(Vector3 scenePos)
		{
			int col = (int)(scenePos.x + 0.5f);
			int row = (int)(scenePos.z + 0.5f);
			return new Position(col, row);
		}
	}
}