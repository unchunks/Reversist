using UnityEngine;

/// <summary>
/// 現在ホバーしているマスを強調表示する
/// </summary>
public class BoardCursor : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;

    [Header("Animation Settings")]
    [SerializeField] private float _blinkSpeed = 4.0f;
    [SerializeField] private float _minAlpha = 0.2f;
    [SerializeField] private float _maxAlpha = 0.6f;

    private MaterialPropertyBlock _propBlock;
    private int _colorId = -1; // -1: 未特定
    private Color _baseColor = Color.cyan;

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();

        Material mat = _renderer.sharedMaterial;
        if (mat != null)
        {
            int id = Shader.PropertyToID("_BaseColor");
            if (mat.HasProperty(id))
            {
                _colorId = id;
                _baseColor = mat.GetColor(_colorId);
            }
        }

        // 見つからなかった場合のフォールバック（標準的な _Color を試す）
        if (_colorId == -1)
        {
            Debug.LogWarning("[BoardCursor] Could not find valid color property. Defaulting to '_Color'.");
            _colorId = Shader.PropertyToID("_Color");
        }

        SetVisible(false);
    }

    private void Update()
    {
        if (_renderer.enabled)
        {
            // 明滅アニメーション
            float alpha = Mathf.Lerp(_minAlpha, _maxAlpha, (Mathf.Sin(Time.time * _blinkSpeed) + 1.0f) * 0.5f);

            Color c = _baseColor;
            c.a = alpha;

            // 色をセットして適用
            // GetPropertyBlockは不要（色を上書きするため）
            _propBlock.SetColor(_colorId, c);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }

    public void ShowAt(Vector3 worldPosition)
    {
        transform.localPosition = worldPosition + new Vector3(0, 0.001f, 0);
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (_renderer != null) _renderer.enabled = visible;
    }
}
