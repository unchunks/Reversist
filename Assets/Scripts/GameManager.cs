using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class GameManager : MonoBehaviour
{
    [SerializeField] private Camera cam;

    [SerializeField] private LayerMask boardLayer;

    [SerializeField] private UIManager uiManager;

    [SerializeField] private StoneSelectPanel blackStoneSelectPanel = new StoneSelectPanel();
    [SerializeField] private StoneSelectPanel whiteStoneSelectPanel = new StoneSelectPanel();

    private ReversiManager reversiManager = new ReversiManager();

    private void Start()
    {
        if (uiManager == null)
        {
            Debug.LogError("UIManager is not assigned in the inspector.");
        }
        if (blackStoneSelectPanel == null)
        {
            Debug.LogError("StoneSelectPanel is not assigned in the inspector.");
        }
        if (whiteStoneSelectPanel == null)
        {
            Debug.LogError("StoneSelectPanel is not assigned in the inspector.");
        }
        if (reversiManager == null)
        {
            Debug.LogError("ReversiManager is not assigned in the inspector.");
        }

        uiManager.SetPlayerText(reversiManager.CurrentPlayer);
        reversiManager.BlackStoneCount.OnCountChanged += blackStoneSelectPanel.UpdateAvailableNums;
        reversiManager.WhiteStoneCount.OnCountChanged += whiteStoneSelectPanel.UpdateAvailableNums;
        reversiManager.Init();
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
        if (Board.GetInstance().CanPut(boardPos) && reversiManager.BlackStoneCount.GetCount(reversiManager.SelectedStoneType) > 0)
        {
            State putPlayer = reversiManager.CurrentPlayer;
            yield return Board.GetInstance().MakeMove(putPlayer, boardPos, reversiManager.SelectedStoneType);
            reversiManager.PassTurn();
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

    private IEnumerator ShowTurnOutcome(State putPlayer)
    {
        if (reversiManager.GameOver)
        {
            yield return ShowGameOver(reversiManager.Winner);
            yield break;
        }

        // 次のプレイヤーとこのターンのプレイヤーが同じなら、スキップを表示
        // OnBoardClickedでこの関数を呼び出す前にPassTurnを呼び出しているため、CurrentPlayerは次のターンのプレイヤーになっている
        if (reversiManager.CurrentPlayer == putPlayer)
        {
            yield return ShowTurnSkip(reversiManager.CurrentPlayer.Opponent());
        }

        uiManager.SetPlayerText(reversiManager.CurrentPlayer);
    }

    private IEnumerator ShowCounting()
    {
        int black = 0, white = 0;

        foreach (Position pos in Board.GetInstance().OccupiedPositions())
        {
            State state = Board.BoardState[pos.Row, pos.Col];

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

            Board.BoardStone[pos.Row, pos.Col].Twitch();
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

    public void OnBlackSelectedToggle()
    {
        for (int i = 0; i < blackStoneSelectPanel.ToggleComponents.Length; i++)
        {
            if (blackStoneSelectPanel.ToggleComponents[i].isOn)
            {
                string selectedText = blackStoneSelectPanel.Label[(StoneType)i].text;
                try
                {
                    StoneType parsedValue = Enum.Parse<StoneType>(selectedText);
                    Debug.Log($"Parsed Value: {parsedValue}");
                    reversiManager.SelectedStoneType = parsedValue;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Enum Parse Failed: {ex.Message}");
                }
                break;
            }
        }
    }
    public void OnWhiteSelectedToggle()
    {
        for (int i = 0; i < whiteStoneSelectPanel.ToggleComponents.Length; i++)
        {
            if (whiteStoneSelectPanel.ToggleComponents[i].isOn)
            {
                string selectedText = whiteStoneSelectPanel.Label[(StoneType)i].text;
                try
                {
                    StoneType parsedValue = Enum.Parse<StoneType>(selectedText);
                    Debug.Log($"Parsed Value: {parsedValue}");
                    reversiManager.SelectedStoneType = parsedValue;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Enum Parse Failed: {ex.Message}");
                }
                break;
            }
        }
    }
}
