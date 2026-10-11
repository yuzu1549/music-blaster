using UnityEngine;
using MusicInvaders.Data;

public class ScoreRankCalculator
{
    private const int ScorePerNoteForRank = 200;

    /// <summary>
    /// 最大スコアを計算
    /// </summary>
    /// <param name="songDatabase"></param>
    /// <param name="songName"></param>
    /// <param name="difficulty"></param>
    /// <returns></returns>
    public int CalculateMaxScore(
        SongDatabaseSO songDatabase,
        string songName,
        string difficulty)
    {
        if (songDatabase == null)
        {
            Debug.LogWarning("SongDatabaseが設定されていません。");
            return 0;
        }

        MusicInvaders.Data.ChartData chart = songDatabase.FindChart(
            songName,
            difficulty
        );

        if (chart == null || chart.chartFile == null)
        {
            Debug.LogWarning(
                $"譜面が見つかりません: {songName} / {difficulty}"
            );
            return 0;
        }

        int noteCount = CountNotes(chart.chartFile);
        return noteCount * ScorePerNoteForRank;
    }

    /// <summary>
    /// ランク判定
    /// </summary>
    /// <param name="score"></param>
    /// <param name="maxScore"></param>
    /// <param name="settings">共通設定。未指定の場合は初期ボーダーを使用する。</param>
    /// <returns></returns>
    public string Calculate(
        int score, int maxScore, ScoreRankSettings settings = null)
    {
        if (maxScore <= 0)
        {
            return "-";
        }

        float scoreRate = (float)score / maxScore;

        float sRate = (settings != null
            ? settings.SPercent : ScoreRankSettings.DefaultSPercent) / 100f;
        float aRate = (settings != null
            ? settings.APercent : ScoreRankSettings.DefaultAPercent) / 100f;
        float bRate = (settings != null
            ? settings.BPercent : ScoreRankSettings.DefaultBPercent) / 100f;
        float cRate = (settings != null
            ? settings.CPercent : ScoreRankSettings.DefaultCPercent) / 100f;

        if (scoreRate >= sRate)
        {
            return "S";
        }

        if (scoreRate >= aRate)
        {
            return "A";
        }

        if (scoreRate >= bRate)
        {
            return "B";
        }

        if (scoreRate >= cRate)
        {
            return "C";
        }

        return "D";
    }

    /// <summary>
    /// ノーツの数を数える
    /// </summary>
    /// <param name="chartFile"></param>
    /// <returns></returns>
    private int CountNotes(TextAsset chartFile)
    {
        int noteCount = 0;
        string[] lines = chartFile.text.Split('\n');

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] values = line.Trim().Split(',');

            // NoteManager.LoadChart()と同じ条件
            if (values.Length == 2)
            {
                noteCount++;
            }
        }

        return noteCount;
    }
}
