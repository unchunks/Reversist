using UnityEngine;

// ---------------------------------------------------------
// VIEW: Title Screen Background
// タイトル画面の背景演出（盤面の生成と回転）を担当する
// ---------------------------------------------------------

public class TitleBackground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardMeshGenerator _boardGenerator;

    [Header("Settings")]
    [SerializeField] private int _gridSize = 8;
    [SerializeField] private float _cellSize = 1.0f;
    [SerializeField] private float _rotateSpeed = 5.0f; // 回転速度

    private void Start()
    {
        // 1. 盤面を生成する命令を出す
        if (_boardGenerator != null)
        {
            _boardGenerator.Generate(_gridSize, _cellSize);
        }
    }

    private void Update()
    {
        // 2. ゆっくり回転させる演出
        // Y軸を中心に回す
        transform.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime);
    }
}
