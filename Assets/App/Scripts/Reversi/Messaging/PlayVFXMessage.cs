using UnityEngine;

namespace App.Reversi.Messaging
{
    /// <summary>
    /// VFXの再生をリクエストするメッセージ
    /// </summary>
    public class PlayVFXMessage
    {
        public readonly VFXType Type;
        public readonly Vector3 Position; // どこで再生するか

        public PlayVFXMessage(VFXType type, Vector3 position)
        {
            Type = type;
            Position = position;
        }
    }
}
