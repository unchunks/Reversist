using App.Reversi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace App.Home
{
    public class PlayerColor : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown dropdown;

        void Update()
        {
            switch (dropdown.value)
            {
                case 0:
                    ToReversiValues.AiColor = Random.Range(0, 2) == 0 ? StoneColor.Black : StoneColor.White;
                    break;
                case 1: // プレイヤーが黒を選択した場合
                    ToReversiValues.AiColor = StoneColor.White;
                    break;
                case 2: // プレイヤーが白を選択した場合
                    ToReversiValues.AiColor = StoneColor.Black;
                    break;
            }
        }
    }
}
