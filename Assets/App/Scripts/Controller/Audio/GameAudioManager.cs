using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Audio;
using System.Threading;

/// <summary>
/// ゲーム全体のオーディオ管理を担当するシングルトン
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private AudioMixerGroup _bgmGroup;
    [SerializeField] private AudioMixerGroup _seGroup;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSourceA;
    [SerializeField] private AudioSource _bgmSourceB;
    [SerializeField] private AudioSource _seSource;

    [Header("Clips Registration")]
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
    [SerializeField] private AudioClip _seInvalid;

    // パラメータ名（Exposed Parameterと一致させること）
    private const string PARAM_MASTER_VOL = "Master_Volume";
    private const string PARAM_BGM_VOL = "BGM_Volume";
    private const string PARAM_SE_VOL = "SE_Volume";

    // SE検索用
    private StoneAudioData[] _clipArray;

    // BGMクロスフェード制御用
    private bool _isUsingSourceA = true;
    private CancellationTokenSource _bgmFadeCts;

    // SEリミッター（同じフレームで大量に鳴らさない）
    private int _frameSeCount = 0;
    private const int MAX_SE_PER_FRAME = 3;

    // 現在のボリューム設定値 (0.0 ~ 1.0)
    private const float DEFAULT_VOLUME = 0.8f;
    public float CurrentMasterVolume { get; private set; } = DEFAULT_VOLUME;
    public float CurrentBGMVolume { get; private set; } = DEFAULT_VOLUME;
    public float CurrentSEVolume { get; private set; } = DEFAULT_VOLUME;

    private void Awake()
    {
        // シングルトン
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 配列の初期化 (StoneTypeの最大値+1のサイズ)
        _clipArray = new StoneAudioData[(int)StoneType.Size];
        foreach (var data in _stoneClips)
        {
            _clipArray[(int)data.Type] = data;
        }

        // AudioSourceの初期設定
        SetupSource(_bgmSourceA, true, _bgmGroup);
        SetupSource(_bgmSourceB, true, _bgmGroup);
        SetupSource(_seSource, false, _seGroup);
    }

    private void Start()
    {
        // 保存された音量設定の読み込みと適用
        LoadVolumeSettings();
    }

    private void LateUpdate()
    {
        // フレームごとの音数カウンタをリセット
        _frameSeCount = 0;
    }

    private void SetupSource(AudioSource source, bool loop, AudioMixerGroup group)
    {
        if (source == null) return;

        source.loop = loop;
        source.playOnAwake = false;
        source.volume = loop ? 0f : 1.0f;   // BGMはフェード前提、SEは基準音量(Mixerで制御)
        if (group != null) source.outputAudioMixerGroup = group;
    }

    #region Volume Control

    /// <summary>
    /// マスター音量を設定する (0.0 ～ 1.0)
    /// </summary>
    public void SetMasterVolume(float value)
    {
        CurrentMasterVolume = Mathf.Clamp01(value);
        ApplyVolumeToMixer(PARAM_MASTER_VOL, CurrentMasterVolume);
        PlayerPrefs.SetFloat("Conf_MasterVolume", CurrentMasterVolume);
    }

    /// <summary>
    /// BGMの音量を設定する (0.0 〜 1.0)
    /// </summary>
    public void SetBGMVolume(float value)
    {
        CurrentBGMVolume = Mathf.Clamp01(value);
        ApplyVolumeToMixer(PARAM_BGM_VOL, CurrentBGMVolume);
        PlayerPrefs.SetFloat("Conf_BGMVolume", CurrentBGMVolume);
    }

    /// <summary>
    /// SEの音量を設定する (0.0 〜 1.0)
    /// </summary>
    public void SetSEVolume(float value)
    {
        CurrentSEVolume = Mathf.Clamp01(value);
        ApplyVolumeToMixer(PARAM_SE_VOL, CurrentSEVolume);
        PlayerPrefs.SetFloat("Conf_SEVolume", CurrentSEVolume);
    }

    private void ApplyVolumeToMixer(string paramName, float linearValue)
    {
        if (_audioMixer == null) return;

        // 線形値(0-1)をデシベル(-80dB ~ 0dB)に変換
        // Log10(0)はマイナス無限大になるため、0.0001以下は-80dBとする
        float db = 0.0f;

        if (linearValue <= 0.0001f)
        {
            db = -80.0f;
        }
        else
        {
            db = 20.0f * Mathf.Log10(linearValue);
        }

        _audioMixer.SetFloat(paramName, db);
    }

    /// <summary>
    /// 設定を保存する（設定画面を閉じたときに保存）
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    private void LoadVolumeSettings()
    {
        CurrentMasterVolume = PlayerPrefs.GetFloat("Conf_MasterVolume", DEFAULT_VOLUME);
        CurrentBGMVolume = PlayerPrefs.GetFloat("Conf_BGMVolume", DEFAULT_VOLUME);
        CurrentSEVolume = PlayerPrefs.GetFloat("Conf_SEVolume", DEFAULT_VOLUME);

        // ミキサーに適用 (Start時に適用しないと初期値に戻ってしまう)
        ApplyVolumeToMixer(PARAM_MASTER_VOL, CurrentMasterVolume);
        ApplyVolumeToMixer(PARAM_BGM_VOL, CurrentBGMVolume);
        ApplyVolumeToMixer(PARAM_SE_VOL, CurrentSEVolume);
    }

    #endregion

    #region BGM Control

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

        AudioSource currentSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
        AudioSource nextSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;

        // 同じ曲なら何もしない
        if (currentSource.isPlaying && currentSource.clip == nextClip) return;

        // フェード処理開始
        _bgmFadeCts?.Cancel();
        _bgmFadeCts = new CancellationTokenSource();
        CrossFadeBGM(currentSource, nextSource, nextClip, fadeDuration, _bgmFadeCts.Token).Forget();

        _isUsingSourceA = !_isUsingSourceA;
    }

    private async UniTaskVoid CrossFadeBGM(AudioSource current, AudioSource next, AudioClip nextClip, float duration, CancellationToken token)
    {
        if (nextClip != null)
        {
            next.clip = nextClip;
            next.volume = 0;
            next.Play();
        }

        float time = 0;
        float startVol = current.volume;

        try
        {
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                // AudioSourceのvolumeは0.0～1.0の「フェード用係数」としてのみ扱う。絶対的な音量はMixerに任せる。
                if (current.isPlaying) current.volume = Mathf.Lerp(startVol, 0f, t);
                if (nextClip != null) next.volume = Mathf.Lerp(0f, 1.0f, t);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (System.OperationCanceledException)
        {
            // キャンセルされた場合（別の曲が要求された場合）は安全に抜ける
        }
        finally
        {
            current.Stop();
            current.volume = 0f;
            if (nextClip != null && !token.IsCancellationRequested) next.volume = 1.0f;
        }
    }

    #endregion

    #region SE Control

    /// <summary>
    /// 石の配置音を鳴らす
    /// </summary>
    public void PlayStoneSpawn(StoneType type)
    {
        if (_frameSeCount >= MAX_SE_PER_FRAME) return;

        var data = _clipArray[(int)type];
        if (data.SpawnClip != null)
        {
            PlayOneShot(data.SpawnClip);
        }
        else if (_clipArray[(int)StoneType.Normal].SpawnClip != null)   // タイプ固有のクリップがない場合はNormalの配置音を鳴らす
        {
            PlayOneShot(_clipArray[(int)StoneType.Normal].SpawnClip);
        }
    }

    /// <summary>
    /// 石の特殊効果音を鳴らす
    /// </summary>
    public void PlayStoneEffect(StoneType type)
    {
        if (_frameSeCount >= MAX_SE_PER_FRAME) return;

        var data = _clipArray[(int)type];
        if (data.EffectClip != null)
        {
            PlayOneShot(data.EffectClip, 1.2f); // エフェクトは少し大きめに鳴らす
        }
    }

    public void PlayUIHover() => PlayOneShot(_seButtonHover, 0.5f);
    public void PlayUIClick() => PlayOneShot(_seButtonClick);
    public void PlayGameStart() => PlayOneShot(_seGameStart);
    public void PlayPass() => PlayOneShot(_sePass);
    public void PlayInvalid() => PlayOneShot(_seInvalid);

    /// <summary>
    /// 内部汎用SE再生メソッド
    /// </summary>
    private void PlayOneShot(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null || _seSource == null) return;

        _seSource.PlayOneShot(clip, volumeScale);
        _frameSeCount++;
    }

    #endregion
}
