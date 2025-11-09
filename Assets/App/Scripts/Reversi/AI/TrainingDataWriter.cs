using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace App.Reversi.AI
{
	[System.Serializable]
	public class TrainingSample
	{
		public float[] inputTensor;
		public float[] policy;
		public float value;
	}

	[System.Serializable]
	public class GameRecord
	{
		public List<TrainingSample> samples = new List<TrainingSample>();

		/// <summary>
		/// ClassicMCTSモード（ランダム・プレイアウト）で棋譜を生成する際、
		/// JSONにシリアライズしないMCTSノード（訪問回数など）を一時的に保持します。
		/// </summary>
		[System.NonSerialized]
		public List<object> tempRoots = new List<object>();
	}

	public static class TrainingDataWriter
	{
		public static void Write(GameRecord record, string filePath)
		{
			try
			{
				// tempRootsは[NonSerialized]なので、JSONには含まれません
				string json = JsonUtility.ToJson(record);
				File.WriteAllText(filePath, json);
			}
			catch (Exception e)
			{
				Debug.LogError($"棋譜の書き出しに失敗: {filePath} \n{e.Message}");
			}
		}
	}
}
