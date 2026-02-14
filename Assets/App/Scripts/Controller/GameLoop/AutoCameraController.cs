using UnityEngine;

// ---------------------------------------------------------
// VIEW: Dynamic Camera Adjuster
// 対象サイズに合わせて、FOV内に収まる最適な距離を計算・移動する
// ---------------------------------------------------------

[RequireComponent(typeof(Camera))]
public class AutoCameraController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("画面端からの余白 (マスの数換算)")]
    [SerializeField] private float _padding = 2.0f;

    [Tooltip("カメラ移動の滑らかさ (秒)")]
    [SerializeField] private float _smoothTime = 0.5f;

    [Tooltip("盤面に対する角度 (X軸回転)")]
    [SerializeField] private float _lookAngle = 65.0f;

    [Header("References")]
    [SerializeField] private Transform _targetCenter; // 盤面の中心 (BoardRoot)

    private Camera _cam;
    private Vector3 _currentVelocity; // SmoothDamp用
    private Vector3 _targetPosition;

    // 現在の盤面の物理的な最大幅（または高さ）
    private float _currentBoardDimension = 8.0f;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _targetPosition = transform.position;

        // 初期角度の適用
        transform.rotation = Quaternion.Euler(_lookAngle, 0, 0);
    }

    private void LateUpdate()
    {
        // 常にターゲット位置へ滑らかに移動し続ける
        transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _currentVelocity, _smoothTime);
    }

    /// <summary>
    /// 盤面サイズに合わせて目標位置を再計算する
    /// </summary>
    /// <param name="boardDimension">盤面のワールド空間での一辺の長さ (CellSize * GridCount)</param>
    public void UpdateTargetPosition(float boardDimension)
    {
        if (_targetCenter == null) return;

        _currentBoardDimension = boardDimension;

        // 1. 映したい範囲の計算 (盤面サイズ + 余白)
        // 対角線をカバーできるように少し余裕を持たせるか、あるいは単に幅を見るか。
        // ここでは「幅または高さの大きい方」に余白を足したものを基準とする。
        float targetSize = boardDimension + (_padding * 2.0f); // 余白は両側に欲しいので2倍

        // 2. 必要な距離の計算 (三角関数)
        // Camera Frustumの高さ = 2 * Distance * tan(FOV / 2)
        // 逆に言えば、Distance = Height / (2 * tan(FOV / 2))

        float fovRad = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;

        // 縦方向の収まりに必要な距離
        float distanceV = (targetSize * 0.5f) / Mathf.Tan(fovRad);

        // 横方向の収まりに必要な距離 (アスペクト比考慮)
        // Camera.aspect = Width / Height
        float distanceH = (targetSize * 0.5f) / (_cam.aspect * Mathf.Tan(fovRad));

        // どちらか遠い方を採用しないと、画面比率によっては見切れる
        float requiredDistance = Mathf.Max(distanceV, distanceH);

        // 3. 角度補正
        // カメラが斜め上から見ている場合、単に距離を引くだけでなく、
        // 「盤面全体が視界の下半分〜中央に収まる」ように微調整が必要だが、
        // 単純化のため「カメラのForwardベクトルの逆方向に、計算した距離だけ引く」形にする。
        // 厳密にはView Planeへの投影が必要だが、ゲーム的にはこれで十分機能する。

        // TargetCenterから、カメラの後ろ方向へ requiredDistance だけ離れた位置
        _targetPosition = _targetCenter.position - (transform.forward * requiredDistance);
    }
}
