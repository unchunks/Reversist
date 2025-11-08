using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace App.Reversi.AI
{
    /// <summary>
    /// 1つの盤面の訓練データを定義
    /// </summary>
    [System.Serializable]
    public class TrainingSample
    {
        // 入力：盤面状態（12x12xNチャンネル）
        public float[] inputTensor;

        // 教師データ（Policy）：各行動の有望度（MCTSの訪問回数比率）
        public float[] policy;

        // 教師データ（Value）：この局面からの最終的な勝敗 (1=勝ち, -1=負け, 0=引き分け)
        public float value;
    }

    /// <summary>
    /// 1ゲーム分の訓練データをリストとして保持
    /// </summary>
    [System.Serializable]
    public class GameRecord
    {
        public List<TrainingSample> samples = new List<TrainingSample>();
    }

    /// <summary>
    /// 棋譜データをJSONファイルとして書き出すクラス
    /// </summary>
    public static class TrainingDataWriter
    {
        public static void Write(GameRecord record, string filePath)
        {
            string json = JsonUtility.ToJson(record);
            File.WriteAllText(filePath, json);
        }
    }
}
