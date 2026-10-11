using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生成・移動時間を曲と難易度の組み合わせで管理する。
/// </summary>
[CreateAssetMenu(fileName = "EnemyDifficultyDatabase",
    menuName = "Data/Enemy Difficulty Database")]
public sealed class EnemyDifficultyDatabase : ScriptableObject
{
    [Header("すべての曲・難易度に適用する全体設定")]
    [SerializeField] private EnemyGlobalSettings globalSettings = new();
    [Header("未登録の曲・難易度に使用する共通設定")]
    [SerializeField] private EnemyDifficultySettings defaultSettings = new();
    [Header("曲ごとの難易度設定一覧")]
    [SerializeField] private List<EnemySongDifficultySettings> songs = new();

    public EnemyGlobalSettings GlobalSettings => globalSettings;

    /// <summary>
    /// 一致する設定を返す。未登録の場合は共通設定を返す。
    /// </summary>
    /// <returns>プレイ開始時に使用する設定</returns>
    public EnemyDifficultySettings GetSettings(string songName, string difficulty)
    {
        string normalizedDifficulty = difficulty == "Difficult" ? "Hard" : difficulty;
        if (!string.IsNullOrEmpty(songName) &&
            !string.IsNullOrEmpty(normalizedDifficulty) && songs != null)
        {
            foreach (EnemySongDifficultySettings song in songs)
            {
                if (song == null || song.SongName != songName)
                {
                    continue;
                }

                EnemyDifficultySettings settings = song.FindSettings(normalizedDifficulty);
                if (settings != null)
                {
                    return settings;
                }
            }
        }

        return defaultSettings;
    }
}

[Serializable]
public sealed class EnemySongDifficultySettings
{
    [Header("SongDatabaseに登録した曲名")]
    [SerializeField] private string songName;
    [Header("この曲の難易度一覧")]
    [SerializeField] private List<EnemyDifficultyEntry> difficulties = new();

    public string SongName => songName;

    /// <summary>
    /// この曲から指定した難易度の設定を取得する。
    /// </summary>
    /// <returns>一致する設定。未登録の場合はnull</returns>
    public EnemyDifficultySettings FindSettings(string difficulty)
    {
        if (difficulties == null)
        {
            return null;
        }

        foreach (EnemyDifficultyEntry entry in difficulties)
        {
            if (entry != null && entry.Difficulty == difficulty)
            {
                return entry.Settings;
            }
        }

        return null;
    }
}

[Serializable]
public sealed class EnemyDifficultyEntry
{
    [Header("譜面の難易度名（Easy / Normal / Hardなど）")]
    [SerializeField] private string difficulty;
    [Header("この曲・難易度に使用する設定一式")]
    [SerializeField] private EnemyDifficultySettings settings = new();

    public string Difficulty => difficulty;
    public EnemyDifficultySettings Settings => settings;
}

[Serializable]
public sealed class EnemyGlobalSettings
{
    [Header("追加生成先として使用する通常フェーズの段数")]
    [Tooltip("5なら経過0～4小節の5段。通常フェーズの小節数が上限です")]
    [Min(1)] public int spawnRowCount = 5;
    [Header("最初の初期グループが経過済みとする小節数")]
    [Min(0f)] public float firstGroupElapsedMeasures = 1f;
    [Header("初期グループ間の経過小節数の間隔")]
    [Min(0f)] public float initialGroupMeasureInterval = 2f;
    [Header("最大グループ数（0で上限なし）")]
    [Min(0)] public int maxActiveGroupCount = 0;
    [Header("進入フェーズの小節数")]
    [Min(1)] public int entryPhaseMeasureCount = 1;
    [Header("突入チャージの拍数")]
    [Min(1)] public int diveChargeBeatCount = 3;
    [Header("突入移動の拍数")]
    [Min(1)] public int diveMovementBeatCount = 1;

    /// <summary>
    /// 全体設定が有効な範囲か確認する。
    /// </summary>
    /// <returns>設定が有効な場合はtrue</returns>
    public bool IsValid => spawnRowCount >= 1 && maxActiveGroupCount >= 0 &&
        IsNonNegativeFinite(firstGroupElapsedMeasures) &&
        IsNonNegativeFinite(initialGroupMeasureInterval) &&
        entryPhaseMeasureCount >= 1 && diveChargeBeatCount >= 1 &&
        diveMovementBeatCount >= 1;

    /// <summary>
    /// 有限の非負値か確認する。
    /// </summary>
    private static bool IsNonNegativeFinite(float number)
    {
        return !float.IsNaN(number) && !float.IsInfinity(number) && number >= 0f;
    }
}

[Serializable]
public sealed class EnemyDifficultySettings
{
    [Header("初期配置するグループ数")]
    [Min(0)] public int initialGroupCount = 3;
    [Header("生存グループ数ごとの生成ルール（配列番号が生存数）")]
    public EnemyGroupSpawnRule[] rules =
    {
        new(1, 5), new(1, 3), new(1, 2),
        new(1, 1), new(2, 1), new(3, 1)
    };
    [Header("通常フェーズの小節数")]
    [Min(1)] public int normalPhaseMeasureCount = 14;

    /// <summary>
    /// 初期配置を含め、生成・移動設定の整合性を確認する。
    /// </summary>
    /// <returns>設定が有効な場合はtrue</returns>
    public bool TryValidate(EnemyGlobalSettings global, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (global == null || !global.IsValid ||
            initialGroupCount < 0 || normalPhaseMeasureCount < 1)
        {
            errorMessage = "敵難易度設定の数値が有効な範囲ではありません。";
            return false;
        }

        if (global.maxActiveGroupCount > 0 &&
            initialGroupCount > global.maxActiveGroupCount)
        {
            errorMessage = "初期グループ数が最大グループ数を超えています。";
            return false;
        }

        double lastElapsed = global.firstGroupElapsedMeasures +
            (double)global.initialGroupMeasureInterval * (initialGroupCount - 1);
        if (initialGroupCount > 0 && lastElapsed >= normalPhaseMeasureCount)
        {
            errorMessage = "初期グループの経過小節数は通常フェーズの長さ未満にしてください。";
            return false;
        }

        if (rules == null || rules.Length == 0 ||
            Array.Exists(rules, rule => rule == null || !rule.IsValid))
        {
            errorMessage = "生成ルールには1以上の待機小節数・生成数を設定してください。";
            return false;
        }

        return true;
    }

}
