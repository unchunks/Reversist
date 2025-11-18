using UnityEngine;

namespace App.Reversi.View
{
    public class CameraOrbit : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Transform _target; // 回転の中心（盤面の親オブジェクトなど。なければVector3.zeroを使います）
        [SerializeField] private float _rotateSpeed = 100f; // 回転速度
        [SerializeField] private float _zoomSpeed = 0.1f;    // ズーム速度
        [SerializeField] private float _minDistance = 2f;   // ズームの最小距離
        [SerializeField] private float _maxDistance = 20f;  // ズームの最大距離

        // 上下回転の制限（角度）
        [SerializeField] private float _minVerticalAngle = 10f;
        [SerializeField] private float _maxVerticalAngle = 85f;

        private float _currentDistance;
        private Vector3 _currentRotation; // X, Yのオイラー角を保持

        private void Start()
        {
            // 初期化：現在のカメラ位置から距離と角度を計算
            Vector3 targetPos = _target != null ? _target.position : Vector3.zero;
            Vector3 direction = transform.position - targetPos;

            _currentDistance = direction.magnitude;
            _currentRotation = transform.eulerAngles;

            // 角度を扱いやすい範囲(-180~180)に正規化
            if (_currentRotation.x > 180) _currentRotation.x -= 360;
            if (_currentRotation.y > 180) _currentRotation.y -= 360;
        }

        private void LateUpdate()
        {
            HandleRotation();
            HandleZoom();
            UpdateCameraTransform();
        }

        private void HandleRotation()
        {
            // WASD (Horizontal/Vertical) 入力を取得
            float h = Input.GetAxis("Horizontal"); // A, D
            float v = Input.GetAxis("Vertical");   // W, S

            // Y軸（左右）回転
            if (Mathf.Abs(h) > 0.01f)
            {
                _currentRotation.y += h * _rotateSpeed * Time.deltaTime;
            }

            // X軸（上下）回転
            if (Mathf.Abs(v) > 0.01f)
            {
                // Wキーで上に上がり（見下ろし）、Sキーで下がる逆の挙動にしたい場合は符号を反転させてください
                _currentRotation.x -= v * _rotateSpeed * Time.deltaTime;

                // 上下の角度制限
                _currentRotation.x = Mathf.Clamp(_currentRotation.x, _minVerticalAngle, _maxVerticalAngle);
            }
        }

        private void HandleZoom()
        {
            // マウススクロール入力を取得
            float scroll = Input.mouseScrollDelta.y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                // スクロールで距離を増減
                _currentDistance -= scroll * _zoomSpeed;
                _currentDistance = Mathf.Clamp(_currentDistance, _minDistance, _maxDistance);
            }
        }

        private void UpdateCameraTransform()
        {
            Vector3 targetPos = _target != null ? _target.position : Vector3.zero;

            // 回転と距離から新しい位置を算出
            Quaternion rotation = Quaternion.Euler(_currentRotation.x, _currentRotation.y, 0);
            Vector3 position = targetPos - (rotation * Vector3.forward * _currentDistance);

            // 反映
            transform.rotation = rotation;
            transform.position = position;
        }
    }
}
