using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask boardLayer;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ReversiManager reversiManager;

    private void Start()
    {
        uiManager.SetPlayerText(reversiManager.CurrentPlayer);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, 20f, boardLayer))
            {
                Vector3 impact = new Vector3(hitInfo.point.z, hitInfo.point.y, hitInfo.point.x);
                Position boardPos = SceneToBoardPos(impact);
                StartCoroutine(OnBoardClicked(boardPos));
            }
        }
    }

    /// <summary>
    /// 引数の位置に石が配置可能なら置く
    /// </summary>
    /// <param name="boardPos"></param>
    private IEnumerator OnBoardClicked(Position boardPos)
    {
        bool canUseStoneType = reversiManager.IsAvailableStoneType();

        if (!canUseStoneType)
        {
            Debug.Log("石がありません");
        }
        if (reversiManager.CanPut(boardPos) && canUseStoneType)
        {
            State putPlayer = reversiManager.CurrentPlayer;
            yield return reversiManager.MakeMove(putPlayer, boardPos, reversiManager.SelectedStoneType[putPlayer]);

            bool showPassButton = reversiManager.PassTurn();
            if (showPassButton)
            {
                Debug.Log("Show button");
                yield return uiManager.ShowPassButton();
            }
            else
            {
                Debug.Log("Hide button");
                yield return uiManager.HidePassButton();
            }

            yield return ShowTurnOutcome(putPlayer);
        }
    }

    private Position SceneToBoardPos(Vector3 scenePos)
    {
        int col = (int)(scenePos.x + 0.5f);
        int row = (int)(scenePos.z + 0.5f);
        return new Position(col, row);
    }

    private Vector3 BoardToScenePos(Position pos)
    {
        return new Vector3(pos.Col, 0, pos.Row);
    }

    private IEnumerator ShowTurnSkip(State skippedPlayer)
    {
        uiManager.SetSkippedText(skippedPlayer);
        yield return uiManager.AnimateTopText();
    }

    private IEnumerator ShowGameOver(State winner)
    {
        uiManager.SetTopText("石を置けるプレイヤーがいません！");
        yield return uiManager.AnimateTopText();

        yield return uiManager.ShowScoreText();
        yield return new WaitForSeconds(0.5f);

        yield return ShowCounting();

        uiManager.SetWinnerText(winner);
        StartCoroutine(uiManager.ShowEndScreen());
    }

    private IEnumerator ShowTurnOutcome(State putPlayer, bool animate = true)
    {
        if (reversiManager.GameOver)
        {
            yield return ShowGameOver(reversiManager.Winner);
            yield break;
        }

        // 次のプレイヤーとこのターンのプレイヤーが同じなら、スキップを表示
        // OnBoardClickedでこの関数を呼び出す前にPassTurnを呼び出しているため、CurrentPlayerは次のターンのプレイヤーになっている
        if (reversiManager.CurrentPlayer == putPlayer && animate)
        {
            yield return ShowTurnSkip(reversiManager.CurrentPlayer.Opponent());
        }

        uiManager.SetPlayerText(reversiManager.CurrentPlayer);
    }

    private IEnumerator ShowCounting()
    {
        int black = 0, white = 0;

        var occupiedPositions = reversiManager.OccupiedPositions();
        while (occupiedPositions.MoveNext())
        {
            Position pos = occupiedPositions.Current;
            State state = reversiManager.GetBoardState(pos);

            if (state == State.Black)
            {
                black++;
                uiManager.SetBlackScoreText(black);
            }
            else if (state == State.White)
            {
                white++;
                uiManager.SetWhiteScoreText(white);
            }

            reversiManager.TwitchStone(pos);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator RestartGame()
    {
        yield return uiManager.HideEndScreen();
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }


    /*   UI用関数   */
    public void OnPlayAgainClicked()
    {
        StartCoroutine(RestartGame());
    }

    // 呼び出しの順序がおかしいからボタンが表示されないのかも？　UniTaskへの置き換えを検討
    public void OnPassClicked()
    {
        Debug.Log("Pass clicked");
        StartCoroutine(passButtonProcess());
        Debug.Log("Pass clicked");
    }

    private IEnumerator passButtonProcess()
    {
        Debug.Log("Pass clicked");
        bool showPassButton = reversiManager.PassTurn();
        if (showPassButton)
        {
            yield return uiManager.ShowPassButton();
        }
        else
        {
            yield return uiManager.HidePassButton();
        }
        yield return ShowTurnOutcome(reversiManager.CurrentPlayer, false);
        Debug.Log("Pass clicked");
    }
}
