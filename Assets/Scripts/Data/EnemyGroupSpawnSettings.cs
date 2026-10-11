using System;
using UnityEngine;

[Serializable]
public sealed class EnemyGroupSpawnRule
{
    [Header("生成まで待つ小節数")]
    [Min(1)]
    [SerializeField] private int spawnIntervalMeasures = 1;

    [Header("一度に生成するグループ数")]
    [Min(1)]
    [SerializeField] private int spawnGroupCount = 1;

    public int SpawnIntervalMeasures => Mathf.Max(
        1,
        spawnIntervalMeasures
    );

    public int SpawnGroupCount => Mathf.Max(1, spawnGroupCount);

    public bool IsValid => spawnIntervalMeasures >= 1 && spawnGroupCount >= 1;

    public EnemyGroupSpawnRule()
    {
    }

    public EnemyGroupSpawnRule(
        int intervalMeasures,
        int groupCount)
    {
        spawnIntervalMeasures = intervalMeasures;
        spawnGroupCount = groupCount;
    }
}

[CreateAssetMenu(
    fileName = "EnemyGroupSpawnSettings",
    menuName = "Data/Enemy Group Spawn Settings"
)]
public class EnemyGroupSpawnSettings : ScriptableObject
{
    [Header("生存グループ数ごとの追加生成ルール")]
    [Tooltip("配列番号を現在の生存グループ数として使用します")]
    [SerializeField] private EnemyGroupSpawnRule[] rules =
    {
        new EnemyGroupSpawnRule(1, 2),
        new EnemyGroupSpawnRule(1, 2),
        new EnemyGroupSpawnRule(2, 2),
        new EnemyGroupSpawnRule(2, 1),
        new EnemyGroupSpawnRule(3, 1),
        new EnemyGroupSpawnRule(4, 1)
    };

    /// <summary>
    /// 現在の生存グループ数に対応する追加生成ルールを取得する。
    /// </summary>
    /// <param name="activeGroupCount">現在の生存グループ数</param>
    /// <param name="rule">取得した追加生成ルール</param>
    /// <returns>有効なルールを取得できた場合は true</returns>
    public bool TryGetRule(
        int activeGroupCount,
        out EnemyGroupSpawnRule rule)
    {
        rule = null;

        if (rules == null || rules.Length == 0)
        {
            return false;
        }

        int ruleIndex = Mathf.Clamp(
            activeGroupCount,
            0,
            rules.Length - 1
        );
        rule = rules[ruleIndex];
        return rule != null;
    }
}
