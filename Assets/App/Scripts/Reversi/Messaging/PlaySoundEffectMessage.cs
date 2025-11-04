namespace App.Reversi.Messaging
{
    /// <summary>
    /// サウンドエフェクトの再生をリクエストするメッセージ
    /// </summary>
    public class PlaySoundEffectMessage
    {
        public readonly SoundEffectType Type;

        public PlaySoundEffectMessage(SoundEffectType type)
        {
            Type = type;
        }
    }
}
