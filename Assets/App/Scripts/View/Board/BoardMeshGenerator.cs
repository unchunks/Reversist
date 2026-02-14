using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))] // Colliderも必須
public class BoardMeshGenerator : MonoBehaviour
{
    [SerializeField] private Material _gridMaterial;

    private Mesh _mesh;

    /// <summary>
    /// 指定されたグリッド数に合わせてメッシュを生成・更新する
    /// </summary>
    /// <param name="gridCount">縦横のマス数 (例: 8, 10, 12...)</param>
    /// <param name="cellSize">1マスのワールドサイズ (例: 1.0f)</param>
    public void Generate(int gridCount, float cellSize)
    {
        // 1. 物理サイズの計算
        // マス数が増えれば、板自体も大きくなる
        float totalSize = gridCount * cellSize;
        float halfSize = totalSize / 2.0f;

        // 2. メッシュ生成 (頂点座標の更新)
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "ProceduralBoard" };
        }
        else
        {
            _mesh.Clear();
        }

        Vector3[] vertices = {
            new Vector3(-halfSize, 0, -halfSize), // 左下
            new Vector3( halfSize, 0, -halfSize), // 右下
            new Vector3(-halfSize, 0,  halfSize), // 左上
            new Vector3( halfSize, 0,  halfSize)  // 右上
        };

        Vector2[] uvs = {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 1), new Vector2(1, 1)
        };

        int[] triangles = { 0, 2, 1, 2, 3, 1 };

        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        // 3. コンポーネント適用
        GetComponent<MeshFilter>().mesh = _mesh;
        var renderer = GetComponent<MeshRenderer>();

        // マテリアルが未設定なら適用、あればプロパティ更新
        if (renderer.sharedMaterial != _gridMaterial)
        {
            renderer.material = _gridMaterial;
        }

        // ★シェーダーのグリッド数を更新（ここが重要）
        renderer.material.SetFloat("_GridCount", (float)gridCount);

        // 4. コライダーのサイズ更新 (Raycast用)
        var collider = GetComponent<BoxCollider>();
        collider.size = new Vector3(totalSize, 0.1f, totalSize);
        collider.center = Vector3.zero;
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }
}
