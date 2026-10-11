using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyGridPlacement : MonoBehaviour
{
    public const int EnemiesPerGroup = 3;

    [Header("グリッド左端の基準位置")]
    [SerializeField] private Transform gridLeftPoint;
    [Header("グリッド右端の基準位置")]
    [SerializeField] private Transform gridRightPoint;
    [Header("横方向のグリッド列数")]
    [Min(EnemiesPerGroup)]
    [SerializeField] private int gridColumnCount = 24;
    [Header("グループ内の敵同士の列間隔")]
    [Tooltip("1で隣接、2で敵の間に1列の空きを作る")]
    [Min(1)]
    [SerializeField] private int groupEnemyColumnStep = 2;
    [Header("グループ間に確保する最低空き列数")]
    [Min(0)]
    [SerializeField] private int minimumGroupGapColumns = 1;
    [Header("離れた生成位置を優先する強さ（0で均等）")]
    [Range(0f, 8f)]
    [SerializeField] private float spawnDistanceBiasPower = 2f;
    [Header("同じ列へ配置するグループ間の最低Y距離")]
    [Tooltip("敵の高さの半分程度を目安に調整します")]
    [Min(0f)]
    [SerializeField] private float minimumGroupVerticalSpacing = 0.5f;

    public Transform GridLeftPoint => gridLeftPoint;
    public Transform GridRightPoint => gridRightPoint;
    public int GridColumnCount => gridColumnCount;
    public int GroupEnemyColumnStep => groupEnemyColumnStep;
    public int GroupColumnSpan =>
        (EnemiesPerGroup - 1) * groupEnemyColumnStep + 1;

    /// <summary>
    /// 初期グループを配置できるグリッド設定か確認する。
    /// </summary>
    /// <param name="initialGroupCount">初期配置するグループ数</param>
    /// <param name="errorMessage">設定に問題がある場合の説明</param>
    /// <returns>配置可能な設定の場合はtrue</returns>
    public bool TryValidate(
        int initialGroupCount,
        out string errorMessage)
    {
        if (gridLeftPoint == null || gridRightPoint == null)
        {
            errorMessage = "グリッド左右端の基準位置が設定されていません。";
            return false;
        }

        if (groupEnemyColumnStep < 1)
        {
            errorMessage = "グループ内の敵の列間隔は1以上にしてください。";
            return false;
        }

        if (minimumGroupGapColumns < 0)
        {
            errorMessage = "グループ間の最低空き列数は0以上にしてください。";
            return false;
        }

        if (minimumGroupVerticalSpacing < 0f)
        {
            errorMessage = "グループ間の最低Y距離は0以上にしてください。";
            return false;
        }

        if (gridColumnCount < GroupColumnSpan)
        {
            errorMessage = "1グループを配置できるグリッド列数がありません。";
            return false;
        }

        int requiredColumnCount = initialGroupCount == 0
            ? 0
            : initialGroupCount * GroupColumnSpan +
                (initialGroupCount - 1) * minimumGroupGapColumns;
        if (requiredColumnCount > gridColumnCount)
        {
            errorMessage = "初期グループを重なりなく配置できる列数がありません。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 初期グループ同士が横方向に重ならない開始列を選ぶ。
    /// </summary>
    /// <param name="groupCount">配置するグループ数</param>
    /// <param name="activeGroups">現在生存しているグループ</param>
    /// <param name="initialGroupYProvider">初期グループのY座標取得処理</param>
    /// <param name="entryY">進入開始位置のY座標</param>
    /// <param name="diveY">突入開始位置のY座標</param>
    /// <param name="selectedColumns">選択した開始列</param>
    /// <returns>必要数分の開始列を選択できた場合はtrue</returns>
    public bool TryChooseInitialStartColumns(
        int groupCount,
        IReadOnlyList<EnemyGroupController> activeGroups,
        Func<int, float> initialGroupYProvider,
        float entryY,
        float diveY,
        out List<int> selectedColumns)
    {
        selectedColumns = new List<int>();
        bool[] occupiedColumns = new bool[gridColumnCount];

        return TryChooseStartColumns(
            occupiedColumns,
            selectedColumns,
            groupCount,
            activeGroups,
            initialGroupYProvider,
            entryY,
            diveY
        );
    }

    /// <summary>
    /// 離れた位置を優先した重み付きランダム順で開始列を返す。
    /// </summary>
    /// <param name="activeGroups">現在生存しているグループ</param>
    /// <param name="entryY">進入開始位置のY座標</param>
    /// <param name="diveY">突入開始位置のY座標</param>
    /// <returns>重み付きランダム順の開始列</returns>
    public List<int> CreateOrderedStartColumnCandidates(
        IReadOnlyList<EnemyGroupController> activeGroups,
        float entryY,
        float diveY)
    {
        return CreateOrderedStartColumnCandidates(
            activeGroups,
            null,
            null,
            entryY,
            diveY
        );
    }

    /// <summary>
    /// 進入開始位置の指定列にグループを配置できるか確認する。
    /// </summary>
    /// <param name="startColumn">候補とする開始列</param>
    /// <param name="activeGroups">現在生存しているグループ</param>
    /// <param name="entryY">進入開始位置のY座標</param>
    /// <returns>配置可能な場合はtrue</returns>
    public bool CanSpawnAtEntry(
        int startColumn,
        IReadOnlyList<EnemyGroupController> activeGroups,
        float entryY)
    {
        int candidateFirstColumn = startColumn - minimumGroupGapColumns;
        int candidateLastColumn = startColumn + GroupColumnSpan - 1 +
            minimumGroupGapColumns;

        foreach (EnemyGroupController activeGroup in activeGroups)
        {
            if (activeGroup == null)
            {
                continue;
            }

            float verticalDistance = Mathf.Abs(
                activeGroup.transform.position.y - entryY
            );
            if (verticalDistance >= minimumGroupVerticalSpacing)
            {
                continue;
            }

            int activeFirstColumn = activeGroup.StartColumn;
            int activeLastColumn = activeFirstColumn +
                activeGroup.ColumnSpan - 1;
            bool overlapsHorizontally =
                candidateFirstColumn <= activeLastColumn &&
                candidateLastColumn >= activeFirstColumn;
            if (overlapsHorizontally)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 進入完了時の予測位置に対し、指定段の列が空いているか確認する。
    /// </summary>
    /// <returns>既存・進入中のグループと重ならない場合はtrue</returns>
    public bool CanSpawnAtArrival(
        int startColumn,
        IReadOnlyList<EnemyGroupController> activeGroups,
        float arrivalY,
        float arrivalBeat)
    {
        int firstColumn = startColumn - minimumGroupGapColumns;
        int lastColumn = startColumn + GroupColumnSpan - 1 + minimumGroupGapColumns;
        foreach (EnemyGroupController group in activeGroups)
        {
            if (group == null)
            {
                continue;
            }

            float predictedY = group.transform.position.y;
            if (group.TryGetComponent(out EnemyGroupMovement movement) &&
                !movement.TryPredictPositionY(arrivalBeat, out predictedY))
            {
                continue;
            }

            // 最低距離が0でも、同一段・同一列への重複は許可しない。
            float distance = Mathf.Abs(predictedY - arrivalY);
            if (distance > 0.0001f && distance >= minimumGroupVerticalSpacing)
            {
                continue;
            }

            int groupLastColumn = group.StartColumn + group.ColumnSpan - 1;
            if (firstColumn <= groupLastColumn && lastColumn >= group.StartColumn)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 指定したグリッド列に対応するワールドX座標を返す。
    /// </summary>
    /// <param name="column">左から0始まりの列番号</param>
    /// <returns>指定列のワールドX座標</returns>
    public float GetColumnX(int column)
    {
        float normalizedColumn = (float)column / (gridColumnCount - 1);
        return Mathf.Lerp(
            gridLeftPoint.position.x,
            gridRightPoint.position.x,
            normalizedColumn
        );
    }

    /// <summary>
    /// 必要な列幅とグループ間隔を確保できる配置を組み立てる。
    /// </summary>
    private bool TryChooseStartColumns(
        bool[] occupiedColumns,
        List<int> selectedColumns,
        int remainingGroupCount,
        IReadOnlyList<EnemyGroupController> activeGroups,
        Func<int, float> initialGroupYProvider,
        float entryY,
        float diveY)
    {
        if (remainingGroupCount == 0)
        {
            return true;
        }

        List<int> candidates = CreateOrderedStartColumnCandidates(
            activeGroups,
            selectedColumns,
            initialGroupYProvider,
            entryY,
            diveY
        );
        foreach (int startColumn in candidates)
        {
            if (!CanUseColumns(occupiedColumns, startColumn))
            {
                continue;
            }

            SetColumnsOccupied(occupiedColumns, startColumn, true);
            selectedColumns.Add(startColumn);

            if (TryChooseStartColumns(
                occupiedColumns,
                selectedColumns,
                remainingGroupCount - 1,
                activeGroups,
                initialGroupYProvider,
                entryY,
                diveY))
            {
                return true;
            }

            selectedColumns.RemoveAt(selectedColumns.Count - 1);
            SetColumnsOccupied(occupiedColumns, startColumn, false);
        }

        return false;
    }

    /// <summary>
    /// 離れた位置を優先した重み付きランダム順で開始列を作成する。
    /// </summary>
    private List<int> CreateOrderedStartColumnCandidates(
        IReadOnlyList<EnemyGroupController> activeGroups,
        List<int> selectedStartColumns,
        Func<int, float> selectedGroupYProvider,
        float entryY,
        float diveY)
    {
        int candidateCount = gridColumnCount - GroupColumnSpan + 1;
        List<int> remainingCandidates = new List<int>(candidateCount);
        List<int> orderedCandidates = new List<int>(candidateCount);

        for (int column = 0; column < candidateCount; column++)
        {
            remainingCandidates.Add(column);
        }

        while (remainingCandidates.Count > 0)
        {
            int selectedIndex = ChooseWeightedCandidateIndex(
                remainingCandidates,
                activeGroups,
                selectedStartColumns,
                selectedGroupYProvider,
                entryY,
                diveY
            );
            orderedCandidates.Add(remainingCandidates[selectedIndex]);
            remainingCandidates.RemoveAt(selectedIndex);
        }

        return orderedCandidates;
    }

    /// <summary>
    /// 生存グループから離れた候補ほど高い重みで1列を選択する。
    /// </summary>
    private int ChooseWeightedCandidateIndex(
        List<int> candidates,
        IReadOnlyList<EnemyGroupController> activeGroups,
        List<int> selectedStartColumns,
        Func<int, float> selectedGroupYProvider,
        float entryY,
        float diveY)
    {
        float totalWeight = 0f;
        float[] candidateWeights = new float[candidates.Count];

        for (int index = 0; index < candidates.Count; index++)
        {
            float weight = GetSpawnDistanceWeight(
                candidates[index],
                activeGroups,
                selectedStartColumns,
                selectedGroupYProvider,
                entryY,
                diveY
            );
            candidateWeights[index] = weight;
            totalWeight += weight;
        }

        float selectedWeight = UnityEngine.Random.value * totalWeight;
        for (int index = 0; index < candidateWeights.Length; index++)
        {
            selectedWeight -= candidateWeights[index];
            if (selectedWeight <= 0f)
            {
                return index;
            }
        }

        return candidates.Count - 1;
    }

    /// <summary>
    /// Y位置の影響を反映した横距離から生成候補の重みを計算する。
    /// </summary>
    private float GetSpawnDistanceWeight(
        int startColumn,
        IReadOnlyList<EnemyGroupController> activeGroups,
        List<int> selectedStartColumns,
        Func<int, float> selectedGroupYProvider,
        float entryY,
        float diveY)
    {
        float closestAdjustedDistance = float.PositiveInfinity;
        float candidateCenter = GetGroupCenterColumn(
            startColumn,
            GroupColumnSpan
        );

        foreach (EnemyGroupController activeGroup in activeGroups)
        {
            if (activeGroup == null)
            {
                continue;
            }

            float activeGroupCenter = GetGroupCenterColumn(
                activeGroup.StartColumn,
                activeGroup.ColumnSpan
            );
            float adjustedDistance = GetInfluenceAdjustedHorizontalDistance(
                candidateCenter,
                activeGroupCenter,
                activeGroup.transform.position.y,
                entryY,
                diveY
            );
            closestAdjustedDistance = Mathf.Min(
                closestAdjustedDistance,
                adjustedDistance
            );
        }

        if (selectedStartColumns != null && selectedGroupYProvider != null)
        {
            for (int groupIndex = 0;
                groupIndex < selectedStartColumns.Count;
                groupIndex++)
            {
                float selectedGroupCenter = GetGroupCenterColumn(
                    selectedStartColumns[groupIndex],
                    GroupColumnSpan
                );
                float adjustedDistance =
                    GetInfluenceAdjustedHorizontalDistance(
                        candidateCenter,
                        selectedGroupCenter,
                        selectedGroupYProvider(groupIndex),
                        entryY,
                        diveY
                    );
                closestAdjustedDistance = Mathf.Min(
                    closestAdjustedDistance,
                    adjustedDistance
                );
            }
        }

        if (float.IsPositiveInfinity(closestAdjustedDistance))
        {
            return 1f;
        }

        float maximumRelevantDistance = gridColumnCount + 1f;
        float weightedDistance = Mathf.Min(
            closestAdjustedDistance,
            maximumRelevantDistance
        );
        return Mathf.Pow(weightedDistance, spawnDistanceBiasPower);
    }

    /// <summary>
    /// Y位置による影響倍率を反映した候補との横距離を返す。
    /// </summary>
    private float GetInfluenceAdjustedHorizontalDistance(
        float candidateCenter,
        float groupCenter,
        float groupY,
        float entryY,
        float diveY)
    {
        float influence = GetSpawnPositionInfluence(groupY, entryY, diveY);
        if (influence <= 0f)
        {
            return float.PositiveInfinity;
        }

        float horizontalDistance = Mathf.Abs(candidateCenter - groupCenter);
        return (horizontalDistance + 1f) / influence;
    }

    /// <summary>
    /// 生成位置を1、突入開始位置を0とする配置への影響倍率を返す。
    /// </summary>
    private float GetSpawnPositionInfluence(
        float groupY,
        float entryY,
        float diveY)
    {
        float verticalRange = Mathf.Abs(entryY - diveY);
        if (Mathf.Approximately(verticalRange, 0f))
        {
            return 1f;
        }

        float distanceFromEntry = Mathf.Abs(groupY - entryY);
        return 1f - Mathf.Clamp01(distanceFromEntry / verticalRange);
    }

    /// <summary>
    /// グループの開始列と列幅から中心列を返す。
    /// </summary>
    private float GetGroupCenterColumn(int startColumn, int columnSpan)
    {
        return startColumn + (columnSpan - 1) * 0.5f;
    }

    /// <summary>
    /// 開始列からグループの列幅と最低間隔を確保できるか確認する。
    /// </summary>
    private bool CanUseColumns(bool[] occupiedColumns, int startColumn)
    {
        int firstCheckedColumn = Mathf.Max(
            0,
            startColumn - minimumGroupGapColumns
        );
        int lastCheckedColumn = Mathf.Min(
            gridColumnCount - 1,
            startColumn + GroupColumnSpan - 1 + minimumGroupGapColumns
        );

        for (int column = firstCheckedColumn;
            column <= lastCheckedColumn;
            column++)
        {
            if (occupiedColumns[column])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 開始列からグループが占有する列幅の使用状態を更新する。
    /// </summary>
    private void SetColumnsOccupied(
        bool[] occupiedColumns,
        int startColumn,
        bool isOccupied)
    {
        for (int offset = 0; offset < GroupColumnSpan; offset++)
        {
            occupiedColumns[startColumn + offset] = isOccupied;
        }
    }
}
