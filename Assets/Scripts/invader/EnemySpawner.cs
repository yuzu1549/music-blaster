using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyGridPlacement))]
[RequireComponent(typeof(EnemySpawnScheduler))]
[RequireComponent(typeof(EnemyChargeEffectController))]
[RequireComponent(typeof(EnemyGroupMovementSettings))]
[RequireComponent(typeof(EnemyGroupScoreSettings))]
public class EnemySpawner : MonoBehaviour
{
    [Header("敵のプールのキー")]
    [SerializeField] private string enemyPoolKey = "Invader";

    private readonly List<EnemyGroupController> activeGroups = new();
    private int nextGroupNumber;
    private EnemyGridPlacement gridPlacement;
    private EnemySpawnScheduler spawnScheduler;
    private EnemyChargeEffectController chargeEffectController;
    private EnemyGroupMovementSettings movementSettings;
    private EnemyGroupScoreSettings scoreSettings;

    /// <summary>
    /// 現在生存している敵グループ数を返す。
    /// </summary>
    public int ActiveGroupCount
    {
        get
        {
            RemoveDestroyedGroups();
            return activeGroups.Count;
        }
    }

    private void Awake()
    {
        gridPlacement = GetComponent<EnemyGridPlacement>();
        spawnScheduler = GetComponent<EnemySpawnScheduler>();
        chargeEffectController = GetComponent<EnemyChargeEffectController>();
        movementSettings = GetComponent<EnemyGroupMovementSettings>();
        scoreSettings = GetComponent<EnemyGroupScoreSettings>();
    }

    /// <summary>
    /// 初期グループの生成に必要な設定を確認する。
    /// </summary>
    /// <returns>初期グループを生成できる場合は true</returns>
    private bool CanSpawnInitialGroups(int initialGroupCount)
    {
        if (movementSettings == null)
        {
            Debug.LogError(
                $"{name}: EnemyGroupMovementSettingsが設定されていません。"
            );
            return false;
        }

        if (!movementSettings.TryValidate(out string movementErrorMessage))
        {
            Debug.LogError($"{name}: {movementErrorMessage}");
            return false;
        }

        if (gridPlacement == null)
        {
            Debug.LogError($"{name}: EnemyGridPlacementが設定されていません。");
            return false;
        }

        if (!gridPlacement.TryValidate(
            initialGroupCount,
            out string gridErrorMessage))
        {
            Debug.LogError($"{name}: {gridErrorMessage}");
            return false;
        }

        if (spawnScheduler == null)
        {
            Debug.LogError($"{name}: EnemySpawnSchedulerが設定されていません。");
            return false;
        }

        if (chargeEffectController == null)
        {
            Debug.LogError(
                $"{name}: EnemyChargeEffectControllerが設定されていません。"
            );
            return false;
        }

        if (scoreSettings == null)
        {
            Debug.LogError(
                $"{name}: EnemyGroupScoreSettingsが設定されていません。"
            );
            return false;
        }

        if (!scoreSettings.TryValidate(out string scoreErrorMessage))
        {
            Debug.LogError($"{name}: {scoreErrorMessage}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 通常フェーズ序盤の異なる進行位置に初期グループを配置する。
    /// </summary>
    /// <param name="initialGroupCount">初期配置するグループ数</param>
    /// <param name="firstGroupElapsedMeasures">
    /// 最初のグループが経過済みとする小節数
    /// </param>
    /// <param name="initialGroupMeasureInterval">
    /// 初期グループ間の小節間隔
    /// </param>
    /// <returns>初期グループの配置を開始できた場合はtrue</returns>
    public bool TrySpawnInitialGroups(
        int initialGroupCount,
        float firstGroupElapsedMeasures,
        float initialGroupMeasureInterval)
    {
        if (!CanSpawnInitialGroups(initialGroupCount))
        {
            return false;
        }

        if (!gridPlacement.TryChooseInitialStartColumns(
            initialGroupCount,
            activeGroups,
            groupIndex => GetInitialGroupY(
                groupIndex,
                firstGroupElapsedMeasures,
                initialGroupMeasureInterval
            ),
            movementSettings.EntryStartPoint.position.y,
            movementSettings.DiveStartPoint.position.y,
            out List<int> startColumns))
        {
            Debug.LogError($"{name}: 初期グループの配置を決定できませんでした。");
            return false;
        }

        for (int groupIndex = 0; groupIndex < startColumns.Count; groupIndex++)
        {
            float elapsedMeasures = GetInitialGroupElapsedMeasures(
                groupIndex,
                firstGroupElapsedMeasures,
                initialGroupMeasureInterval
            );
            float groupY = GetInitialGroupY(
                groupIndex,
                firstGroupElapsedMeasures,
                initialGroupMeasureInterval
            );

            SpawnGroup(
                startColumns[groupIndex],
                groupY,
                elapsedMeasures,
                false
            );
        }

        return true;
    }

    /// <summary>
    /// 初期グループが通常フェーズで経過済みとする小節数を返す。
    /// </summary>
    /// <param name="groupIndex">0始まりの初期グループ番号</param>
    /// <param name="firstGroupElapsedMeasures">
    /// 最初のグループが経過済みとする小節数
    /// </param>
    /// <param name="initialGroupMeasureInterval">
    /// 初期グループ間の小節間隔
    /// </param>
    /// <returns>通常フェーズで経過済みとする小節数</returns>
    private float GetInitialGroupElapsedMeasures(
        int groupIndex,
        float firstGroupElapsedMeasures,
        float initialGroupMeasureInterval)
    {
        return firstGroupElapsedMeasures +
            initialGroupMeasureInterval * groupIndex;
    }

    /// <summary>
    /// 初期グループを配置する予定Y座標を返す。
    /// </summary>
    /// <param name="groupIndex">0始まりの初期グループ番号</param>
    /// <param name="firstGroupElapsedMeasures">
    /// 最初のグループが経過済みとする小節数
    /// </param>
    /// <param name="initialGroupMeasureInterval">
    /// 初期グループ間の小節間隔
    /// </param>
    /// <returns>初期グループの予定Y座標</returns>
    private float GetInitialGroupY(
        int groupIndex,
        float firstGroupElapsedMeasures,
        float initialGroupMeasureInterval)
    {
        float elapsedMeasures = GetInitialGroupElapsedMeasures(
            groupIndex,
            firstGroupElapsedMeasures,
            initialGroupMeasureInterval
        );
        return movementSettings.GetNormalPhaseY(elapsedMeasures);
    }

    /// <summary>
    /// 設定数まで、空いている列へ追加グループを生成する。
    /// </summary>
    /// <param name="requestedGroupCount">生成を試みるグループ数</param>
    /// <param name="maxActiveGroupCount">
    /// 盤面上の最大グループ数。0の場合は上限なし
    /// </param>
    /// <returns>実際に生成できたグループ数</returns>
    public int TrySpawnAdditionalGroups(
        int requestedGroupCount,
        int maxActiveGroupCount)
    {
        RemoveDestroyedGroups();

        int availableGroupCount = requestedGroupCount;
        if (maxActiveGroupCount > 0)
        {
            availableGroupCount = Mathf.Min(
                availableGroupCount,
                Mathf.Max(0, maxActiveGroupCount - activeGroups.Count)
            );
        }

        int spawnedGroupCount = 0;
        for (int groupIndex = 0;
            groupIndex < availableGroupCount;
            groupIndex++)
        {
            if (!TrySpawnAdditionalGroup())
            {
                break;
            }

            spawnedGroupCount++;
        }

        return spawnedGroupCount;
    }

    /// <summary>
    /// 到着時に空いている段と列を選び、最上部からグループを進入させる。
    /// </summary>
    /// <returns>生成できた場合は true</returns>
    private bool TrySpawnAdditionalGroup()
    {
        int beatsPerMeasure = spawnScheduler.MusicBeatClock.BeatsPerMeasure;
        float arrivalBeat = spawnScheduler.MusicBeatClock.CurrentBeatIndex +
            movementSettings.EntryPhaseMeasureCount * beatsPerMeasure;
        List<int> rows = new List<int>(movementSettings.SpawnRowCount);
        for (int row = 0; row < movementSettings.SpawnRowCount; row++)
        {
            rows.Add(row);
        }

        // 段の順序を均等にシャッフルし、空きのある段をランダムに選ぶ。
        for (int index = rows.Count - 1; index > 0; index--)
        {
            int other = Random.Range(0, index + 1);
            (rows[index], rows[other]) = (rows[other], rows[index]);
        }

        List<int> candidates =
            gridPlacement.CreateOrderedStartColumnCandidates(
                activeGroups,
                movementSettings.EntryStartPoint.position.y,
                movementSettings.DiveStartPoint.position.y
            );
        foreach (int row in rows)
        {
            float arrivalY = movementSettings.GetSpawnRowY(row, beatsPerMeasure);
            foreach (int startColumn in candidates)
            {
                if (!gridPlacement.CanSpawnAtArrival(
                    startColumn, activeGroups, arrivalY, arrivalBeat))
                {
                    continue;
                }

                return SpawnGroup(
                    startColumn,
                    movementSettings.EntryStartPoint.position.y,
                    row,
                    true
                );
            }
        }

        return false;
    }

    /// <summary>
    /// 破棄済みのグループを生存リストから取り除く。
    /// </summary>
    private void RemoveDestroyedGroups()
    {
        activeGroups.RemoveAll(group => group == null);
    }

    /// <summary>
    /// 指定した列とY座標に3体の敵グループを生成する。
    /// </summary>
    /// <param name="startColumn">グループの開始列</param>
    /// <param name="groupY">グループのY座標</param>
    /// <param name="elapsedMeasures">通常フェーズで経過済みとする小節数</param>
    /// <param name="startsInEntryPhase">進入フェーズから開始する場合は true</param>
    /// <returns>敵1体以上を生成できた場合は true</returns>
    private bool SpawnGroup(
        int startColumn,
        float groupY,
        float elapsedMeasures,
        bool startsInEntryPhase)
    {
        float firstEnemyX = gridPlacement.GetColumnX(startColumn);
        float lastEnemyX = gridPlacement.GetColumnX(
            startColumn +
                (EnemyGridPlacement.EnemiesPerGroup - 1) *
                gridPlacement.GroupEnemyColumnStep
        );
        float groupX = (firstEnemyX + lastEnemyX) * 0.5f;

        nextGroupNumber++;
        GameObject groupObject = new GameObject(
            $"EnemyGroup_{nextGroupNumber:00}"
        );
        groupObject.transform.SetParent(transform, false);
        groupObject.transform.position = new Vector3(
            groupX,
            groupY,
            transform.position.z
        );

        EnemyGroupController enemyGroup =
            groupObject.AddComponent<EnemyGroupController>();
        scoreSettings.ApplyTo(enemyGroup);
        enemyGroup.SetGridPlacement(
            startColumn,
            gridPlacement.GroupColumnSpan
        );

        EnemyGroupMovement groupMovement =
            groupObject.AddComponent<EnemyGroupMovement>();
        movementSettings.InitializeGroupMovement(
            groupMovement,
            spawnScheduler.MusicBeatClock,
            elapsedMeasures,
            startsInEntryPhase
        );

        EnemyGroupChargeEffect groupChargeEffect =
            chargeEffectController.AddToGroup(groupObject);

        int registeredEnemyCount = 0;
        for (int offset = 0;
            offset < EnemyGridPlacement.EnemiesPerGroup;
            offset++)
        {
            Vector3 spawnPosition = new Vector3(
                gridPlacement.GetColumnX(
                    startColumn +
                        offset * gridPlacement.GroupEnemyColumnStep
                ),
                groupY,
                transform.position.z
            );
            EnemyMove enemy = GetEnemy(spawnPosition);
            if (enemy == null)
            {
                continue;
            }

            if (!enemy.TryGetComponent(out EnemyHealth enemyHealth))
            {
                Debug.LogError($"{enemy.name}: EnemyHealthが見つかりません。");
                PoolManager.Instance.Return(enemyPoolKey, enemy.gameObject);
                continue;
            }

            enemy.enabled = false;
            enemyGroup.RegisterEnemy(enemyHealth);
            groupMovement.RegisterEnemy(enemy.transform);
            groupChargeEffect.RegisterEnemy(enemy.transform);
            registeredEnemyCount++;
        }

        if (registeredEnemyCount == 0)
        {
            Destroy(groupObject);
            return false;
        }

        activeGroups.Add(enemyGroup);
        return true;
    }

    /// <summary>
    /// プールから敵を1体取得し、指定位置に配置する。
    /// </summary>
    /// <param name="spawnPosition">敵の生成位置</param>
    /// <returns>取得した敵。取得できない場合は null</returns>
    private EnemyMove GetEnemy(Vector3 spawnPosition)
    {
        if (PoolManager.Instance == null || string.IsNullOrEmpty(enemyPoolKey))
        {
            Debug.LogError($"{name}: PoolManagerまたは敵のプールキーが設定されていません。");
            return null;
        }

        GameObject enemyObject = PoolManager.Instance.Get(enemyPoolKey);
        if (enemyObject == null)
        {
            Debug.LogError($"{name}: プールから{enemyPoolKey}を取得できませんでした。");
            return null;
        }

        if (!enemyObject.TryGetComponent(out EnemyMove enemy))
        {
            Debug.LogError($"{name}: {enemyPoolKey}にEnemyMoveがありません。");
            PoolManager.Instance.Return(enemyPoolKey, enemyObject);
            return null;
        }

        enemy.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
        return enemy;
    }
}
