using UnityEngine;

/// <summary>
/// 対象サイズに合わせて、FOV内に収まる最適な距離を計算・移動する
/// </summary>
[RequireComponent(typeof(Camera))]
public class AutoCameraController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("画面端からの余白 (マスの数換算)")]
    [SerializeField] private float _paddingUnits = 0.1f;

    [Tooltip("カメラ移動の滑らかさ (秒)")]
    [SerializeField] private float _smoothTime = 0.5f;

    [Tooltip("盤面に対する角度 (X軸回転)")]
    [SerializeField] private float _lookAngle = 65.0f;

    [Header("References")]
    [SerializeField] private Transform _targetCenter;   // 盤面の中心

    private Camera _cam;
    private Vector3 _currentVelocity;
    private Vector3 _targetPosition;

    // 状態監視用キャッシュ
    private float _currentBoardDimension = 8.0f;    // 現在の盤面の物理的な最大幅（または高さ）
    private float _lastAspect = -1f;

    // 目標との距離が閾値（0.001）以下になったらカメラの移動をやめる
    private const float STOP_THRESHOLD_SQR = 0.000001f; // 計算を軽くするため2乗で比較
    private bool _isMoving = false;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _targetPosition = transform.position;
        transform.rotation = Quaternion.Euler(_lookAngle, 0, 0);
    }

    private void LateUpdate()
    {
        // 画面リサイズを検知して自動再計算
        if (!Mathf.Approximately(_cam.aspect, _lastAspect))
        {
            _lastAspect = _cam.aspect;
            RecalculateTargetPosition();
        }

        // 移動が必要な場合のみSmoothDampを実行
        if (_isMoving)
        {
            // 目標との距離の2乗を計算 (SquareMagnitudeは処理が軽い)
            Vector3 diff = _targetPosition - transform.position;
            if (diff.sqrMagnitude < STOP_THRESHOLD_SQR)
            {
                // 到達とみなして停止
                transform.position = _targetPosition;
                _isMoving = false;
                _currentVelocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _currentVelocity, _smoothTime);
            }
        }
    }

    /// <summary>
    /// 盤面サイズに合わせて目標位置を再計算する（外部からの更新トリガー）
    /// </summary>
    public void UpdateTargetPosition(float boardDimension)
    {
        _currentBoardDimension = boardDimension;
        RecalculateTargetPosition();
    }

    /// <summary>
    /// 内部の再計算ロジック（アスペクト比変更時にも呼ばれる）
    /// </summary>
    private void RecalculateTargetPosition()
    {
        if (_targetCenter == null || _cam == null) return;

        // 映したい範囲の計算
        float targetSize = _currentBoardDimension + (_paddingUnits * 2.0f);

        // 必要な距離の計算
        // 縦方向距離 = Height / (2 * tan(FOV / 2))
        float fovRad = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float distanceV = (targetSize * 0.5f) / Mathf.Tan(fovRad);

        // 横方向距離 = Width / (2 * tan(FOV / 2) * aspect)
        float distanceH = (targetSize * 0.5f) / (_cam.aspect * Mathf.Tan(fovRad));

        float requiredDistance = Mathf.Max(distanceV, distanceH);

        // 角度補正付き目標ワールド座標の算出
        _targetPosition = _targetCenter.position - (transform.forward * requiredDistance);

        // 移動フラグを立ててLateUpdateで移動実行
        _isMoving = true;
    }
}
