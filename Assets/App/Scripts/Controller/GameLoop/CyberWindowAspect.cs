using UnityEngine;
using UnityEngine.UI;

// ---------------------------------------------------------
// VIEW: UI Aspect Ratio Sync (Fixed via IMaterialModifier)
// IMaterialModifierを使用し、Inspectorのマテリアルを上書きせずに補正を行う
// ---------------------------------------------------------

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class CyberWindowAspect : MonoBehaviour, IMaterialModifier
{
    private Image _image;
    private RectTransform _rectTransform;

    // 生成したマテリアルインスタンスを保持する
    private Material _instancedMaterial;
    // 変更検知用キャッシュ
    private Material _baseMaterialCache;

    private static readonly int _AspectId = Shader.PropertyToID("_Aspect");
    private float _currentAspect = 1.0f;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (_image == null || _rectTransform == null) return;

        // 現在のアスペクト比を計算
        // lossyScaleを含めることで、親のScaleアニメーションにも対応
        float width = _rectTransform.rect.width * transform.lossyScale.x;
        float height = _rectTransform.rect.height * transform.lossyScale.y;

        if (height <= 0.001f) height = 0.001f; // ゼロ除算防止

        float newAspect = width / height;

        // 変化があった場合のみマテリアルの更新をリクエスト
        if (Mathf.Abs(newAspect - _currentAspect) > 0.001f)
        {
            _currentAspect = newAspect;
            _image.SetMaterialDirty(); // これを呼ぶと GetModifiedMaterial が走る
        }
    }

    /// <summary>
    /// IMaterialModifierの実装: UI描画直前に呼ばれる。
    /// ここでマテリアルを一時的に差し替えることで、Inspectorの設定を汚さずに値を変更できる。
    /// </summary>
    public Material GetModifiedMaterial(Material baseMaterial)
    {
        // マテリアルが設定されていない場合は何もしない
        if (baseMaterial == null) return null;

        // ベースマテリアルが変更された、またはインスタンス未生成の場合に再生成
        if (_instancedMaterial == null || _baseMaterialCache != baseMaterial)
        {
            // 古いインスタンスがあれば破棄
            if (_instancedMaterial != null)
            {
                DestroyImmediateWrapper(_instancedMaterial);
            }

            // 元のマテリアルをベースに複製
            _instancedMaterial = new Material(baseMaterial);
            _instancedMaterial.name = baseMaterial.name + " (Aspect Corrected)";
            _instancedMaterial.hideFlags = HideFlags.HideAndDontSave; // シーンに保存させない
            _baseMaterialCache = baseMaterial;
        }

        // アスペクト比を適用
        _instancedMaterial.SetFloat(_AspectId, _currentAspect);

        return _instancedMaterial;
    }

    // アニメーションなどでプロパティが変わった際のコールバック
    private void OnDidApplyAnimationProperties()
    {
        if (_image != null) _image.SetMaterialDirty();
    }

    private void OnDisable()
    {
        // 無効化されたら標準マテリアルに戻すようリクエスト
        if (_image != null) _image.SetMaterialDirty();
    }

    private void OnDestroy()
    {
        if (_instancedMaterial != null)
        {
            DestroyImmediateWrapper(_instancedMaterial);
            _instancedMaterial = null;
        }
    }

    private void DestroyImmediateWrapper(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
