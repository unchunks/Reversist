using Cysharp.Threading.Tasks;
using UnityEngine;

public class StoneView : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;

    [SerializeField] private Color _blackMainColor = new Color(0.0f, 0.0f, 0.0f);
    [SerializeField] private Color _blackSubColor = new Color(0.8f, 0.8f, 1.0f);
    [SerializeField] private Color _blackRimColor = new Color(0.8f, 0.9f, 1.0f) * 1.5f;
    [SerializeField] private Color _whiteMainColor = new Color(0.8f, 0.8f, 1.0f);
    [SerializeField] private Color _whiteSubColor = new Color(0.0f, 0.0f, 0.0f);
    [SerializeField] private Color _whiteRimColor = new Color(1.0f, 0.0f, 0.5f) * 2.5f;

    private static readonly int _ColorId = Shader.PropertyToID("_Color");
    private static readonly int _SubColorId = Shader.PropertyToID("_SubColor");
    private static readonly int _PatternModeId = Shader.PropertyToID("_PatternMode");
    private static readonly int _PatternScaleId = Shader.PropertyToID("_PatternScale");
    private static readonly int _AnimSpeedId = Shader.PropertyToID("_AnimSpeed");
    private static readonly int _RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int _RimColorId = Shader.PropertyToID("_RimColor");

    private static MaterialPropertyBlock _propBlock;

    private StoneColor _currentColor;
    private StoneType _currentType;

    private void Awake()
    {
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
        if (_renderer == null) _renderer = GetComponent<Renderer>();
    }

    public void SetAppearance(StoneColor color, StoneType type)
    {
        _currentColor = color;
        _currentType = type;

        _renderer.GetPropertyBlock(_propBlock);
        // 通常適用（上書きなし）
        ApplyColorSettings(color, type, null);
        _renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// 設定値をプロパティブロックにセットする。
    /// overrideRimColor が指定されていれば、リムカラーだけその値を使う。
    /// </summary>
    private void ApplyColorSettings(StoneColor color, StoneType type, Color? overrideRimColor)
    {
        Color mainColor, subColor, rimColor;
        float rimPower = 4.0f;

        if (color == StoneColor.Black)
        {
            mainColor = _blackMainColor;
            subColor = _blackSubColor;
            rimColor = _blackRimColor;
        }
        else
        {
            mainColor = _whiteMainColor;
            subColor = _whiteSubColor;
            rimColor = _whiteRimColor;
        }

        float mode = 0.0f;
        float speed = 1.0f;
        float scale = 5.0f;

        switch (type)
        {
            case StoneType.Normal: mode = 0.0f; break;
            case StoneType.Expander: mode = 1.0f; speed = 2.0f; subColor = (color == StoneColor.White) ? new Color(0, 0.8f, 0.8f) : new Color(0.8f, 0, 0.8f); break;
            case StoneType.Fixed: mode = 2.0f; rimColor = (color == StoneColor.White) ? new Color(1.0f, 0.8f, 0.0f) : new Color(0.8f, 0.5f, 0.0f); break;
            case StoneType.Phantom: mode = 3.0f; speed = 8.0f; rimColor = Color.gray; break;
            case StoneType.Bomb: mode = 4.0f; speed = 5.0f; subColor = new Color(0.8f, 0.0f, 0.0f); break;
            case StoneType.Spy: mode = 5.0f; speed = 3.0f; subColor = new Color(0.0f, 0.8f, 0.2f); break;
        }

        // リムカラーの上書き指定があれば適用
        if (overrideRimColor.HasValue)
        {
            rimColor = overrideRimColor.Value;
        }

        _propBlock.SetColor(_ColorId, mainColor);
        _propBlock.SetColor(_SubColorId, subColor);
        _propBlock.SetFloat(_PatternModeId, mode);
        _propBlock.SetFloat(_PatternScaleId, scale);
        _propBlock.SetFloat(_AnimSpeedId, speed);
        _propBlock.SetFloat(_RimPowerId, rimPower);
        _propBlock.SetColor(_RimColorId, rimColor);
    }

    // --- Animations ---

    public async UniTask AnimateSpawnAsync(Vector3 targetScale)
    {
        float duration = 0.25f;
        float time = 0;

        Vector3 endPos = transform.localPosition;
        Vector3 startPos = endPos + Vector3.up * 10.0f;

        transform.localPosition = startPos;
        transform.localScale = Vector3.zero;
        gameObject.SetActive(true);

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float ease = (t >= 1.0f) ? 1.0f : 1.0f - Mathf.Pow(2.0f, -10.0f * t);

            transform.localPosition = Vector3.Lerp(startPos, endPos, ease);
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, ease);

            await UniTask.Yield();
        }
        transform.localPosition = endPos;
        transform.localScale = targetScale;
    }

    public async UniTask AnimateFlipAsync(StoneColor targetColor, StoneType type)
    {
        float duration = 0.4f;
        float time = 0;
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(180, 0, 0);

        bool colorChanged = false;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float ease = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            transform.localRotation = Quaternion.Slerp(startRot, endRot, ease);

            if (t >= 0.5f && !colorChanged)
            {
                SetAppearance(targetColor, type);
                colorChanged = true;
            }

            await UniTask.Yield();
        }
        transform.localRotation = Quaternion.identity;
        SetAppearance(targetColor, type);
    }

    /// <summary>
    /// 固定石の拒絶アニメーション（修正版）
    /// </summary>
    public async UniTask AnimateLockedAsync()
    {
        float duration = 0.5f; // 少し長めに
        float time = 0;
        Vector3 originalPos = transform.localPosition;

        // エラー色定義
        Color errorColor = new Color(1.0f, 0.0f, 0.0f) * 8.0f; // 強烈な赤（Bloom用）

        // 本来のリムカラーを計算しておく（基準値）
        // 一旦ダミーブロックにセットして値を取り出す手もあるが、
        // ApplyColorSettingsのロジックを知っているので再計算させる方が早い
        // ここでは「点滅していない状態の色」をApplyColorSettings内で再計算させる

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // 減衰する激しい振動
            float strength = (1.0f - t) * 0.01f;
            float offsetX = Mathf.Sin(time * 60.0f) * strength;
            float offsetZ = Mathf.Cos(time * 55.0f) * strength;

            transform.localPosition = originalPos + new Vector3(offsetX, 0, offsetZ);

            // リムの点滅計算 (Sine波で赤と本来の色を行き来する)
            float flash = Mathf.PingPong(time * 6.0f, 1.0f); // 0 -> 1 -> 0

            // 現在の _currentColor, _currentType に基づく設定をセットしつつ、
            // RimColorだけは errorColor を混ぜたものにする
            // ※LerpはShader側ではなくCPU側で計算して渡す
            Color currentTargetRim = errorColor; // ベースは赤

            // 赤く光らせる値を渡して全プロパティ更新
            // 点滅させるために、後半は元の色に戻していくなどの調整も可能だが、
            // ここではシンプルに「アニメーション中は赤いリムを適用し続ける（強弱をつける）」形にする

            Color flashRim = Color.Lerp(Color.black, errorColor, flash);
            // ※本来の色とのLerpはApplyColorSettingsの構造上難しいので、
            // 「エラー色が上乗せされる」イメージで、強烈な赤を渡す。

            // ブロックをクリア（念のため）
            _propBlock.Clear();

            // 全パラメータセット（リムカラーだけ上書き）
            ApplyColorSettings(_currentColor, _currentType, flashRim);

            // 適用
            _renderer.SetPropertyBlock(_propBlock);

            await UniTask.Yield();
        }

        // 終了処理
        transform.localPosition = originalPos;
        SetAppearance(_currentColor, _currentType); // 完全に元の状態に戻す
    }

    public async UniTask AnimateDestructionAsync()
    {
        float duration = 0.3f;
        float time = 0;
        Vector3 startScale = transform.localScale;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            float shake = Mathf.Sin(time * 50.0f) * 0.1f;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t) + Vector3.one * shake;

            await UniTask.Yield();
        }
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }
}

