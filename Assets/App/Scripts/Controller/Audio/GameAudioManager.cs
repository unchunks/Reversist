using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Audio;
using System.Threading;

// ---------------------------------------------------------
// CORE: Audio Manager
// BGMのクロスフェード、SEのピッチランダム化、同時発音制御を備えた統合サウンドシステム
// ---------------------------------------------------------

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Mixer Group (Optional)")]
    [SerializeField] private AudioMixerGroup _bgmGroup;
    [SerializeField] private AudioMixerGroup _seGroup;

    [Header("Audio Sources")]
    [Tooltip("BGM用 (2つ用意してクロスフェードさせる)")]
    [SerializeField] private AudioSource _bgmSourceA;
    [SerializeField] private AudioSource _bgmSourceB;
    [Tooltip("SE用")]
    [SerializeField] private AudioSource _seSource;

    [Header("Clips Registration")]
    // インスペクターで設定しやすいようにリスト化
    [SerializeField] private AudioClip _bgmTitle;
    [SerializeField] private AudioClip _bgmMainGame;
    [SerializeField] private AudioClip _bgmWin;
    [SerializeField] private AudioClip _bgmLose;

    // 石ごとのSEデータ
    [System.Serializable]
    public struct StoneAudioData
    {
        public StoneType Type;
        public AudioClip SpawnClip;
        public AudioClip EffectClip;
    }
    [SerializeField] private List<StoneAudioData> _stoneClips;

    // UI等の汎用SE
    [Header("UI / Common SE")]
    [SerializeField] private AudioClip _seButtonHover;
    [SerializeField] private AudioClip _seButtonClick;
    [SerializeField] private AudioClip _seGameStart;
    [SerializeField] private AudioClip _sePass;

    // 高速検索用辞書
    private Dictionary<StoneType, StoneAudioData> _clipMap = new Dictionary<StoneType, StoneAudioData>();

    // BGMクロスフェード制御用
    private bool _isUsingSourceA = true;
    private float _bgmVolume = 0.5f;
    private CancellationTokenSource _bgmFadeCts;

    // SEリミッター（同じフレームで大量に鳴らさない）
    private int _frameSeCount = 0;
    private const int MAX_SE_PER_FRAME = 3;

    private void Awake()
    {
        // シングルトンパターン（重複したら自爆）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 辞書構築
        foreach (var data in _stoneClips)
        {
            if (!_clipMap.ContainsKey(data.Type)) _clipMap.Add(data.Type, data);
        }

        // AudioSourceの初期設定
        SetupSource(_bgmSourceA, true);
        SetupSource(_bgmSourceB, true);
        SetupSource(_seSource, false);
    }

    private void SetupSource(AudioSource source, bool loop)
    {
        if (source == null) return;
        source.loop = loop;
        source.playOnAwake = false;
        if (loop) source.volume = 0; // BGMはフェードイン前提で0

        // Mixer設定があれば適用
        if (loop && _bgmGroup != null) source.outputAudioMixerGroup = _bgmGroup;
        else if (!loop && _seGroup != null) source.outputAudioMixerGroup = _seGroup;
    }

    private void LateUpdate()
    {
        // フレームごとの発音数カウンタをリセット
        _frameSeCount = 0;
    }

    // ========================================================================
    // BGM Control (Cross-fade)
    // ========================================================================

    public enum BgmType { Title, MainGame, Win, Lose, Silence }

    /// <summary>
    /// BGMを滑らかに切り替える
    /// </summary>
    public void PlayBGM(BgmType type, float fadeDuration = 1.0f)
    {
        AudioClip nextClip = null;
        switch (type)
        {
            case BgmType.Title: nextClip = _bgmTitle; break;
            case BgmType.MainGame: nextClip = _bgmMainGame; break;
            case BgmType.Win: nextClip = _bgmWin; break;
            case BgmType.Lose: nextClip = _bgmLose; break;
            case BgmType.Silence: nextClip = null; break;
        }

        // 現在鳴っているSourceと、次に鳴らすSourceを特定
        AudioSource currentSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
        AudioSource nextSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;

        // 同じ曲なら何もしない
        if (currentSource.isPlaying && currentSource.clip == nextClip) return;

        // フェード処理開始
        _bgmFadeCts?.Cancel();
        _bgmFadeCts = new CancellationTokenSource();
        CrossFadeBGM(currentSource, nextSource, nextClip, fadeDuration, _bgmFadeCts.Token).Forget();

        // フラグ反転
        _isUsingSourceA = !_isUsingSourceA;
    }

    private async UniTaskVoid CrossFadeBGM(AudioSource current, AudioSource next, AudioClip nextClip, float duration, CancellationToken token)
    {
        // 次の曲の準備
        if (nextClip != null)
        {
            next.clip = nextClip;
            next.volume = 0;
            next.Play();
        }

        float time = 0;
        float startVol = current.volume;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            // フェードアウト
            if (current.isPlaying)
                current.volume = Mathf.Lerp(startVol, 0, t);

            // フェードイン
            if (nextClip != null)
                next.volume = Mathf.Lerp(0, _bgmVolume, t);

            await UniTask.Yield(token);
        }

        current.Stop();
        current.volume = 0;

        if (nextClip != null) next.volume = _bgmVolume;
        else next.Stop(); // Silenceの場合
    }

    // ========================================================================
    // SE Control
    // ========================================================================

    /// <summary>
    /// 石の配置音を鳴らす（ピッチランダム化付き）
    /// </summary>
    public void PlayStoneSpawn(StoneType type)
    {
        if (_frameSeCount >= MAX_SE_PER_FRAME) return; // 同時発音制限

        if (_clipMap.TryGetValue(type, out var data) && data.SpawnClip != null)
        {
            PlayOneShot(data.SpawnClip, 0.9f, 1.1f);
        }
        else
        {
            // なければNormalで代用
            if (_clipMap.TryGetValue(StoneType.Normal, out var normalData))
            {
                PlayOneShot(normalData.SpawnClip, 0.9f, 1.1f);
            }
        }
    }

    /// <summary>
    /// 石の特殊効果音を鳴らす
    /// </summary>
    public void PlayStoneEffect(StoneType type)
    {
        if (_frameSeCount >= MAX_SE_PER_FRAME) return;

        if (_clipMap.TryGetValue(type, out var data) && data.EffectClip != null)
        {
            // エフェクト音はあまりピッチを変えない方が重厚感が出る
            PlayOneShot(data.EffectClip, 1.0f, 1.0f, 1.2f); // 少し大きめに
        }
    }

    public void PlayUIHover() => PlayOneShot(_seButtonHover, 0.95f, 1.05f, 0.5f);
    public void PlayUIClick() => PlayOneShot(_seButtonClick, 0.9f, 1.1f);
    public void PlayGameStart() => PlayOneShot(_seGameStart);
    public void PlayPass() => PlayOneShot(_sePass);

    /// <summary>
    /// 内部汎用SE再生メソッド
    /// </summary>
    private void PlayOneShot(AudioClip clip, float minPitch = 1.0f, float maxPitch = 1.0f, float volumeScale = 1.0f)
    {
        if (clip == null || _seSource == null) return;

        _seSource.pitch = Random.Range(minPitch, maxPitch);
        _seSource.PlayOneShot(clip, volumeScale);
        _frameSeCount++;
    }
}
