using UnityEngine;

/// <summary>
/// タイトル画面の背景演出（盤面の生成と回転）
/// </summary>
[RequireComponent(typeof(AutoRotate))]
public class TitleBackground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardMeshGenerator _boardGenerator;

    [Header("Settings")]
    [SerializeField] private int _gridSize = 8;
    [SerializeField] private float _cellSize = 1.0f;

    private void Start()
    {
        // 盤面を生成
        if (_boardGenerator != null)
        {
            _boardGenerator.Generate(_gridSize, _cellSize);
        }
    }
}
