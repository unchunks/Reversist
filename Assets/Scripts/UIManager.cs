using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI topText;
    [SerializeField] private TextMeshProUGUI blackScoreText;
    [SerializeField] private TextMeshProUGUI whiteScoreText;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Image filterOverlay;
    [SerializeField] private RectTransform playAgainButton;

    public void SetPlayerText(State currentPlayer)
    {
        if (currentPlayer == State.Black)
        {
            topText.text = "黒のターン <sprite name=Stone Black Up>";
        }
        else if (currentPlayer == State.White)
        {
            topText.text = "白のターン <sprite name=Stone White Up>";
        }
    }

    public void SetSkippedText(State skippedPlayer)
    {
        if (skippedPlayer == State.Black)
        {
            topText.text = "黒は置ける場所がありません！ <sprite name=Stone Black Up>";
        }
        else if (skippedPlayer == State.White)
        {
            topText.text = "白は置ける場所がありません！ <sprite name=Stone White Up>";
        }
    }

    public void SetTopText(string message)
    {
        topText.text = message;
        AnimateTopText();
    }

    public IEnumerator AnimateTopText()
    {
        topText.transform.LeanScale(Vector3.one * 1.2f, 0.25f).setLoopPingPong(4);
        yield return new WaitForSeconds(2);
    }

    private IEnumerator ScaleDown(RectTransform rect)
    {
        rect.LeanScale(Vector3.zero, 0.2f);
        yield return new WaitForSeconds(0.2f);
        rect.gameObject.SetActive(false);
    }

    private IEnumerator ScaleUp(RectTransform rect)
    {
        rect.gameObject.SetActive(true);
        rect.localScale = Vector3.zero;
        rect.LeanScale(Vector3.one, 0.2f);
        yield return new WaitForSeconds(0.2f);
    }

    public IEnumerator ShowScoreText()
    {
        yield return ScaleDown(topText.rectTransform);
        yield return ScaleUp(blackScoreText.rectTransform);
        yield return ScaleUp(whiteScoreText.rectTransform);
    }

    public void SetBlackScoreText(int score)
    {
        blackScoreText.text = $"<sprite name=Stone Black Up> {score}";
    }
    
    public void SetWhiteScoreText(int score)
    {
        whiteScoreText.text = $"<sprite name=Stone White Up> {score}";
    }

    private IEnumerator ShowOverlay()
    {
        filterOverlay.gameObject.SetActive(true);
        filterOverlay.color = Color.clear;
        filterOverlay.rectTransform.LeanAlpha(0.8f, 1);
        yield return new WaitForSeconds(1);
    }

    private IEnumerator HideOverlay()
    {
        filterOverlay.rectTransform.LeanAlpha(0, 1);
        yield return new WaitForSeconds(1);
        filterOverlay.gameObject.SetActive(false);
    }

    private IEnumerator MoveScoreDown()
    {
        blackScoreText.rectTransform.LeanMoveY(-600, 0.5f);
        whiteScoreText.rectTransform.LeanMoveY(-600, 0.5f);
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator MoveScoreUp()
    {
        blackScoreText.rectTransform.LeanMoveY(-100, 0.5f);
        whiteScoreText.rectTransform.LeanMoveY(-100, 0.5f);
        yield return new WaitForSeconds(0.5f);
    }

    public void SetWinnerText(State winner)
    {
        Debug.Log($"winner is {winner}");
        switch (winner)
        {
            case State.Black:
                winnerText.text = "黒の勝ち！";
                break;
            case State.White:
                winnerText.text = "白の勝ち！";
                break;
            case State.None:
                winnerText.text = "引き分け";
                break;
        }
    }

    public IEnumerator ShowEndScreen()
    {
        yield return ShowOverlay();
        yield return MoveScoreDown();
        yield return ScaleUp(winnerText.rectTransform);
        yield return ScaleUp(playAgainButton);
    }

    public IEnumerator HideEndScreen()
    {
        StartCoroutine(ScaleDown(winnerText.rectTransform));
        StartCoroutine(ScaleDown(blackScoreText.rectTransform));
        StartCoroutine(ScaleDown(whiteScoreText.rectTransform));
        StartCoroutine(ScaleDown(playAgainButton));

        yield return new WaitForSeconds(0.5f);
        yield return HideOverlay();
    }
}
