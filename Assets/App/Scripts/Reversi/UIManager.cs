using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections;
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

        [SerializeField] private StoneTypePanel _blackPanel;
        [SerializeField] private StoneTypePanel _whitePanel;

        // ゲーム後に使用するUI
        [SerializeField] private Image _backOverlay;

        [SerializeField] private TextMeshProUGUI _winnerText;

        [SerializeField] private Button _restartButton;

        // どちらでも使用するUI
        [SerializeField] private TextMeshProUGUI _blackTotalStonesCountText;
        [SerializeField] private TextMeshProUGUI _whiteTotalStonesCountText;

        private void Start()
        {
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
        }

        public void SetTopText(StoneColor currentPlayer)
        {
            _topText.text = $"{currentPlayer.ToString()} のターン";
        }

        public void SetTotalStonesCountText(Dictionary<StoneColor, int> stoneCount)
        {
            _blackTotalStonesCountText.text = "<sprite name=Stone Black Up>" + stoneCount[StoneColor.Black].ToString();
            _whiteTotalStonesCountText.text = "<sprite name=Stone White Up>" + stoneCount[StoneColor.White].ToString();
        }

        public void UpdateAvailableCount(StoneColor color, StoneType type, int count)
        {
            if (color == StoneColor.Black)
            {
                _blackPanel.UpdateAvailableCount(type, count);
            }
            else if (color == StoneColor.White)
            {
                _whitePanel.UpdateAvailableCount(type, count);
            }
        }

        public void UpdateUI(BoardInfo boardInfo)
        {
            // public StoneColor PutPlayer;
            // public StoneColor PutColor;
            // public StoneType PutType;
            // public List<Position> ReversePos;
            // public Dictionary<StoneColor, int> StoneCount;
            SetTopText(boardInfo.PutPlayer.Opponent());
            SetTotalStonesCountText(boardInfo.TotalStoneCount);
        }

        public async UniTask ShowResultView()
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

            _winnerText.gameObject.SetActive(true);
            _winnerText.rectTransform.localScale = Vector3.zero;
            await _winnerText.rectTransform.DOScale(Vector3.one, 0.2f).ToUniTask();

            _restartButton.gameObject.SetActive(true);
            _restartButton.transform.localScale = Vector3.zero;
            await _restartButton.transform.DOScale(Vector3.one, 0.2f).ToUniTask();

        }

        public StoneType GetSelectedStoneType(StoneColor getColor)
        {
            return (getColor == StoneColor.Black)? _blackPanel.SelectedType : _whitePanel.SelectedType;
        }

        public void OnRestart()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private async UniTask ToAnchor(RectTransform rectTransform, Vector2 anchorPos, float duration)
        {
            await UniTask.WhenAll(
                rectTransform.DOAnchorMin(anchorPos, duration).ToUniTask(),
                rectTransform.DOAnchorMax(anchorPos, duration).ToUniTask()
            );
        }
    }
}