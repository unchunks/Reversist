using App.Reversi;
using App.Reversi.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Home
{
    public class ButtonSceneLoader : MonoBehaviour
    {
        [Tooltip("AIの思考時間（ms）")]
        public int aiThinkTime;

        private string targetSceneName = "ReversiScene";

        public void PVEOnClick()
        {
            // AIの思考時間を設定する
            ToReversiValues.GameMode = GameMode.PVE;
            ToReversiValues.AiThinkTime = aiThinkTime;

            // シーンを移動する
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
