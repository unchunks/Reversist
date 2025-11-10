using App.Reversi.Messaging;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace App.Reversi
{
	public class UIManager : MonoBehaviour
	{
		// ゲーム中に使用するUI
		[SerializeField] private TextMeshProUGUI _topText;
		[SerializeField] private TextMeshProUGUI _aiText;

		[SerializeField] private StoneTypePanel _blackPanel;
		[SerializeField] private StoneTypePanel _whitePanel;

		// ゲーム後に使用するUI
		[SerializeField] private Image _backOverlay;

		[SerializeField] private TextMeshProUGUI _winnerText;

		[SerializeField] private Button _restartButton;

		// どちらでも使用するUI
		[SerializeField] private TextMeshProUGUI _blackTotalStonesCountText;
		[SerializeField] private TextMeshProUGUI _whiteTotalStonesCountText;

		[Inject] private ISubscriber<BoardInfo> _boardInfoSubscriber;
		[Inject] private ISubscriber<TurnChangedMessage> _turnChangedSubscriber;
		[Inject] private ISubscriber<GameOverMessage> _gameOverSubscriber;
		[Inject] private ISubscriber<AvailableCountChangedMessage> _countSubscriber;
		[Inject] private ISubscriber<AIThinkingMessage> _aiThinkingSubscriber;

		private void Awake()
		{
			// パネルの初期化
			_blackPanel.SetObserveColor(StoneColor.Black);
			_whitePanel.SetObserveColor(StoneColor.White);

			_ = ToAnchor(_blackTotalStonesCountText.rectTransform, new Vector2(0, 0.5f), 0);
			_ = ToAnchor(_whiteTotalStonesCountText.rectTransform, new Vector2(0, 0.5f), 0);

			_blackTotalStonesCountText.rectTransform.anchoredPosition = new Vector2(150, 0);
			_whiteTotalStonesCountText.rectTransform.anchoredPosition = new Vector2(450, 0);

			_blackTotalStonesCountText.rectTransform.DOScale(Vector3.one, 0);
			_whiteTotalStonesCountText.rectTransform.DOScale(Vector3.one, 0);

			_topText.gameObject.SetActive(true);
			_blackTotalStonesCountText.gameObject.SetActive(true);
			_whiteTotalStonesCountText.gameObject.SetActive(true);
			_blackPanel.gameObject.SetActive(true);
			_whitePanel.gameObject.SetActive(true);

			_backOverlay.gameObject.SetActive(false);
			_winnerText.gameObject.SetActive(false);
			_restartButton.gameObject.SetActive(false);

			// メッセージの処理登録
			_boardInfoSubscriber.Subscribe(UpdateUI);
			_turnChangedSubscriber.Subscribe(msg => SetTopText(msg.CurrentPlayer));
			_gameOverSubscriber.Subscribe(msg => ShowResultView(msg).Forget());
			_countSubscriber.Subscribe(msg => UpdateAvailableCount(msg.Color, msg.Type, msg.Count));
			_aiThinkingSubscriber.Subscribe(OnAIThinking);
		}

		public void SetTopText(StoneColor currentPlayer)
		{
			_topText.text = $"{currentPlayer.ToString()} のターン";
		}

		private void OnAIThinking(AIThinkingMessage msg)
		{
			Debug.Log("AIの思考状態が変化: " + msg.AiColor.ToString() + " IsThinking=" + msg.IsThinking.ToString());
			if (msg.IsThinking)
			{
				Debug.Log("AIが思考中です");
				_aiText.text = $"AI({msg.AiColor.ToString()})が思考中...";
				_aiText.gameObject.SetActive(true);
			}
			else
			{
				Debug.Log("AIの思考が終了しました");
				_aiText.gameObject.SetActive(false);
			}
		}

		public void SetTotalStonesCountText(Dictionary<StoneColor, int> stoneCount)
		{
			_blackTotalStonesCountText.text = "<sprite name=Stone Black Up>" + stoneCount[StoneColor.Black].ToString();
			_whiteTotalStonesCountText.text = "<sprite name=Stone White Up>" + stoneCount[StoneColor.White].ToString();
		}

		public void UpdateAvailableCount(StoneColor color, StoneType type, int count)
		{
			//Debug.Log("石の個数を更新: " + color.ToString() + " " + type.ToString() + " " + count.ToString());
			if (color == StoneColor.Black)
			{
				_blackPanel.UpdateAvailableCount(type, count);
			}
			else if (color == StoneColor.White)
			{
				_whitePanel.UpdateAvailableCount(type, count);
			}
		}

		/// <summary>
		/// 盤面の更新情報（主に石の総数）をUIに反映する
		/// </summary>
		public void UpdateUI(BoardInfo boardInfo)
		{
			// ターン表示は TurnChangedMessage で行うため、ここでは石の総数のみ更新
			SetTotalStonesCountText(boardInfo.TotalStoneCount);
		}//SetTopText(boardInfo.PutPlayer.Opponent());

		public async UniTask ShowResultView(GameOverMessage msg)
		{
			_topText.gameObject.SetActive(false);
			_blackPanel.gameObject.SetActive(false);
			_whitePanel.gameObject.SetActive(false);

			_backOverlay.gameObject.SetActive(true);

			await UniTask.WhenAll(
				ToAnchor(_blackTotalStonesCountText.rectTransform, new Vector2(0.5f, 0.5f), 1),
				_blackTotalStonesCountText.rectTransform.DOAnchorPos(new Vector2(-250, 100), 1).SetEase(Ease.OutQuad).ToUniTask(),
				_blackTotalStonesCountText.rectTransform.DOScale(new Vector3(1.5f, 1.5f), 1).ToUniTask(),

				ToAnchor(_whiteTotalStonesCountText.rectTransform, new Vector2(0.5f, 0.5f), 1),
				_whiteTotalStonesCountText.rectTransform.DOAnchorPos(new Vector2(250, 100), 1).SetEase(Ease.OutQuad).ToUniTask(),
				_whiteTotalStonesCountText.rectTransform.DOScale(new Vector3(1.5f, 1.5f), 1).ToUniTask()
			);

			string WinnerString = (msg.Winner == StoneColor.None)
				? "引き分け"
				: msg.Winner.ToString() + "の勝ち";
			_winnerText.text = $"{WinnerString} の勝利！";
			_winnerText.gameObject.SetActive(true);
			_winnerText.rectTransform.localScale = Vector3.zero;
			await _winnerText.rectTransform.DOScale(Vector3.one, 0.2f).ToUniTask();

			_restartButton.gameObject.SetActive(true);
			_restartButton.transform.localScale = Vector3.zero;
			await _restartButton.transform.DOScale(Vector3.one, 0.2f).ToUniTask();

		}

		public void OnRestart()
		{
			Scene activeScene = SceneManager.GetActiveScene();
			SceneManager.LoadScene(activeScene.name);
		}

		/// <summary>
		/// UIの位置調整を行う
		/// </summary>
		/// <param name="rectTransform"></param>
		/// <param name="anchorPos"></param>
		/// <param name="duration"></param>
		/// <returns></returns>
		private async UniTask ToAnchor(RectTransform rectTransform, Vector2 anchorPos, float duration)
		{
			await UniTask.WhenAll(
				rectTransform.DOAnchorMin(anchorPos, duration).ToUniTask(),
				rectTransform.DOAnchorMax(anchorPos, duration).ToUniTask()
			);
		}
	}
}