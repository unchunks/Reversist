using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace App.Reversi.Core
{
	public enum GameMode
	{
		PVP,
		PVE
	}

	/// <summary>
	/// �Q�[���̐i�s�A�^�[���Ǘ��A���s������s��
	/// </summary>
	public class GameController : MonoBehaviour
	{
		[Header("Game Settings")]
		[SerializeField] private GameMode _gameMode = GameMode.PVE;
		[SerializeField] private StoneColor _aiColor = StoneColor.White;

		[Header("References")]
		[SerializeField] private Camera _mainCam;

		[Inject] private Board _board;
		[Inject] private PlayerInventory _playerInventory;
		[Inject] private InputManager _inputManager;
		[Inject] private AIAgent _aiAgent;

		[Inject] private IPublisher<RequestPutStoneMessage> _requestPutStonePublisher;
		[Inject] private IPublisher<TurnChangedMessage> _turnChangedPublisher;
		[Inject] private IPublisher<GameOverMessage> _gameOverPublisher;
		[Inject] private ISubscriber<CellClickedMessage> _cellClickedSubscriber;
		[Inject] private ISubscriber<BoardInfo> _boardInfoSubscriber;
		[Inject] private ISubscriber<SelectedStoneTypeInfo> _selectedStoneTypeSubscriber;

		private bool _isGameOver;
		private StoneColor _currentPlayer;
		private Dictionary<StoneColor, StoneType> _currentSelectedType;

		private async UniTask Start()
		{
			await _board.InitializeAsync();

			_isGameOver = false;
			_currentPlayer = StoneColor.Black;

			_currentSelectedType = new Dictionary<StoneColor, StoneType>
			{
				{ StoneColor.Black, StoneType.Normal },
				{ StoneColor.White, StoneType.Normal }
			};

			// ���b�Z�[�W�ւ̓o�^
			_cellClickedSubscriber.Subscribe(OnCellClicked);
			_boardInfoSubscriber.Subscribe(info => OnBoardUpdated(info).Forget());
			_selectedStoneTypeSubscriber.Subscribe(OnSelectedStoneTypeChanged);

			// �J�����A���̓o�^
			_board.OnBoardSizeChanged = async (size) =>
			{
				await _mainCam.transform.DOMoveY(size, 1).SetEase(Ease.OutBounce).ToUniTask();
			};

			// AI�̐F��������
			if (_gameMode == GameMode.PVE)
			{
				_aiAgent.Initialize(_aiColor);
			}

			// UI�ƃn�C���C�g�̏�����
			_board.UpdateHighlight(_currentPlayer, _currentSelectedType[_currentPlayer]);
			_turnChangedPublisher.Publish(new TurnChangedMessage(_currentPlayer));
		}

		/// <summary>
		/// �v���C���[���g�p����΂̎�ނ�ύX����
		/// </summary>
		private void OnSelectedStoneTypeChanged(SelectedStoneTypeInfo info)
		{
			_currentSelectedType[info.Color] = info.Type;
			if (info.Color == _currentPlayer && !_isGameOver)
			{
				_board.UpdateHighlight(_currentPlayer, info.Type);
			}
		}

		/// <summary>
		/// ���[�U�[���Ֆʂ��N���b�N����
		/// </summary>
		private void OnCellClicked(CellClickedMessage msg)
		{
			if (_isGameOver) return;

			StoneType selectedType = _currentSelectedType[_currentPlayer];

			// �΂̍݌ɂ��`�F�b�N
			if (!_playerInventory.HasStock(_currentPlayer, selectedType))
			{
				Debug.Log($"�΂̐�������܂���: {_currentPlayer}, {selectedType}");
				return;
			}

			// �Ֆʂɒu���邩�`�F�b�N
			if (!_board.CanPut(msg.Position))
			{
				Debug.Log($"���̏ꏊ�ɂ͒u���܂���: {msg.Position}");
				return;
			}

			// �z�u���N�G�X�g�𔭍s
			_board.HideHighlight();
			_requestPutStonePublisher.Publish(new RequestPutStoneMessage(_currentPlayer, selectedType, msg.Position));
		}

		/// <summary>
		/// �Ֆʂ̍X�V�����������iBoard���΂�u�����j
		/// </summary>
		private async UniTask OnBoardUpdated(BoardInfo info)
		{
			if (_isGameOver) return;

			// �΂̃A�j���[�V�����i����Extend�j�ƃJ�������[�N���������Ȃ��悤�A
			// �����ҋ@���Ă���^�[���`�F�b�N���s��
			await UniTask.DelayFrame(1);

			CheckNextTurn();
		}

		/// <summary>
		/// ���̃^�[���ɐi�݁A�p�X��Q�[���I�[�o�[�𔻒肷��
		/// </summary>
		private void CheckNextTurn()
		{
			_currentPlayer = _currentPlayer.Opponent();
			StoneType nextType = _currentSelectedType[_currentPlayer];

			if (_board.UpdateHighlight(_currentPlayer, nextType) == 0)
			{
				// �p�X
				_currentPlayer = _currentPlayer.Opponent();
				nextType = _currentSelectedType[_currentPlayer];
				Debug.Log(_currentPlayer.Opponent() + " ���p�X���܂���");

				if (_board.UpdateHighlight(_currentPlayer, nextType) == 0)
				{
					// ���Ғu���Ȃ� = �Q�[���I�[�o�[
					_isGameOver = true;
					_board.HideHighlight();
					_inputManager.SetInputActive(false);

					StoneColor winColor = _board.GetWinColor();
					int blackCount = _board.StoneCount[StoneColor.Black];
					int whiteCount = _board.StoneCount[StoneColor.White];
					_gameOverPublisher.Publish(new GameOverMessage(winColor, blackCount, whiteCount));
					return;
				}
			}

			// �^�[���̌���ʒm
			Debug.Log("���̃^�[��: " + _currentPlayer);
			_turnChangedPublisher.Publish(new TurnChangedMessage(_currentPlayer));

			if (_gameMode == GameMode.PVE && _currentPlayer == _aiColor)
			{
				// AI�̃^�[���F�v���C���[�̓��͂𖳌���
				_inputManager.SetInputActive(false);
			}
			else
			{
				// �l�Ԃ̃^�[���F�v���C���[�̓��͂�L����
				_inputManager.SetInputActive(true);
			}
		}
	}
}