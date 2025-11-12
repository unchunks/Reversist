using App.Reversi.Messaging;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
	/// <summary>
	/// UI以外のユーザーからの入力（盤面クリックなど）を管理する
	/// </summary>
	public class InputManager : MonoBehaviour
	{
		[SerializeField] private Camera _mainCam;
		[SerializeField] private LayerMask _hitLayer;

		[Inject] private IPublisher<CellClickedMessage> _cellClickedPublisher;
		[Inject] private Board _board;

		private bool _isInputActive = true;
		public void SetInputActive(bool isActive) => _isInputActive = isActive;

		private void Update()
		{
			if (_isInputActive && Input.GetMouseButtonDown(0))
			{
				Ray ray = _mainCam.ScreenPointToRay(Input.mousePosition);

				if (Physics.Raycast(ray, out RaycastHit hitInfo, 20f, _hitLayer))
				{
					if (_board.TryGetBoardPosition(hitInfo.point, out Position boardPos))
					{
						_cellClickedPublisher.Publish(new CellClickedMessage(boardPos));
					}
				}
			}
		}
	}
}