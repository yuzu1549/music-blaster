using UnityEngine;

public class EnemyGroupMovementSettings : MonoBehaviour
{
    [Header("進入フェーズ開始位置")]
    [SerializeField] private Transform entryStartPoint;
    [Header("通常フェーズ開始位置")]
    [SerializeField] private Transform normalStartPoint;
    [Header("突入フェーズ開始位置")]
    [SerializeField] private Transform diveStartPoint;
    [Header("突入フェーズ終了位置")]
    [SerializeField] private Transform diveEndPoint;

    [Space(15)]
    [Header("進入フェーズの小節数")]
    [Min(1)]
    [SerializeField] private int entryPhaseMeasureCount = 1;
    [Header("通常フェーズの小節数")]
    [Min(1)]
    [SerializeField] private int normalPhaseMeasureCount = 14;

    private int spawnRowCount = 1;
    [Header("突入フェーズでチャージする拍数")]
    [Min(1)]
    [SerializeField] private int diveChargeBeatCount = 3;
    [Header("突入フェーズで等速移動する拍数")]
    [Min(1)]
    [SerializeField] private int diveMovementBeatCount = 1;
    [Header("進入フェーズの減速指数")]
    [Min(1f)]
    [SerializeField] private float entryDecelerationPower = 5f;

    [Space(15)]
    [Header("突入チャージ完了時の横方向倍率")]
    [Min(0.01f)]
    [SerializeField] private float diveChargeScaleX = 1.1f;
    [Header("突入チャージ完了時の縦方向倍率")]
    [Min(0.01f)]
    [SerializeField] private float diveChargeScaleY = 0.7f;

    [Space(15)]
    [Header("拍先頭で適用する敵の横方向倍率")]
    [Min(0.01f)]
    [SerializeField] private float beatSquashScaleX = 1.05f;
    [Header("拍先頭で適用する敵の縦方向倍率")]
    [Min(0.01f)]
    [SerializeField] private float beatSquashScaleY = 0.9f;
    [Header("元の大きさへ戻り終える拍内進行度")]
    [Range(0.01f, 1f)]
    [SerializeField] private float beatScaleRestoreProgress = 0.5f;

    public Transform EntryStartPoint => entryStartPoint;
    public Transform NormalStartPoint => normalStartPoint;
    public Transform DiveStartPoint => diveStartPoint;
    public Transform DiveEndPoint => diveEndPoint;
    public int SpawnRowCount => Mathf.Min(spawnRowCount, normalPhaseMeasureCount);
    public int EntryPhaseMeasureCount => entryPhaseMeasureCount;

    /// <summary>
    /// プレイ開始時に曲別の移動時間だけを反映する。
    /// 基準位置・伸縮・減速演出はシーンの設定を維持する。
    /// </summary>
    public void ApplyDifficultySettings(
        EnemyGlobalSettings global, EnemyDifficultySettings settings)
    {
        entryPhaseMeasureCount = global.entryPhaseMeasureCount;
        spawnRowCount = global.spawnRowCount;
        normalPhaseMeasureCount = settings.normalPhaseMeasureCount;
        diveChargeBeatCount = global.diveChargeBeatCount;
        diveMovementBeatCount = global.diveMovementBeatCount;
    }

    /// <summary>
    /// 移動設定と基準位置が使用可能か確認する。
    /// </summary>
    /// <param name="errorMessage">設定に問題がある場合の説明</param>
    /// <returns>敵グループを移動させられる設定の場合はtrue</returns>
    public bool TryValidate(out string errorMessage)
    {
        if (entryStartPoint == null || normalStartPoint == null ||
            diveStartPoint == null || diveEndPoint == null)
        {
            errorMessage = "敵移動用の基準位置が設定されていません。";
            return false;
        }

        if (entryPhaseMeasureCount < 1 || normalPhaseMeasureCount < 1 ||
            diveChargeBeatCount < 1 || diveMovementBeatCount < 1)
        {
            errorMessage = "各移動フェーズの長さは1以上にしてください。";
            return false;
        }

        if (entryDecelerationPower < 1f || diveChargeScaleX <= 0f ||
            diveChargeScaleY <= 0f || beatSquashScaleX <= 0f ||
            beatSquashScaleY <= 0f || beatScaleRestoreProgress <= 0f)
        {
            errorMessage = "敵移動の倍率設定が有効な範囲ではありません。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 通常フェーズの経過小節数に対応するY座標を返す。
    /// </summary>
    /// <param name="elapsedMeasures">通常フェーズの経過小節数</param>
    /// <returns>通常フェーズ内のY座標</returns>
    public float GetNormalPhaseY(float elapsedMeasures)
    {
        float normalPhaseProgress = Mathf.Clamp01(
            elapsedMeasures / normalPhaseMeasureCount
        );

        return Mathf.Lerp(
            normalStartPoint.position.y,
            diveStartPoint.position.y,
            normalPhaseProgress
        );
    }

    /// <summary>
    /// 指定段の進入完了位置を、通常移動と同じ拍補正で返す。
    /// </summary>
    /// <returns>段に対応するワールドY座標</returns>
    public float GetSpawnRowY(int rowIndex, int beatsPerMeasure)
    {
        return EnemyGroupMovement.GetNormalPositionY(
            normalStartPoint.position.y, diveStartPoint.position.y,
            rowIndex * beatsPerMeasure,
            normalPhaseMeasureCount * beatsPerMeasure);
    }

    /// <summary>
    /// 初期配置か追加生成かに応じてグループ移動を初期化する。
    /// </summary>
    /// <param name="groupMovement">初期化するグループ移動</param>
    /// <param name="musicBeatClock">移動の拍同期に使用するクロック</param>
    /// <param name="elapsedMeasures">通常フェーズの経過済み小節数</param>
    /// <param name="startsInEntryPhase">進入フェーズから開始する場合はtrue</param>
    public void InitializeGroupMovement(
        EnemyGroupMovement groupMovement,
        MusicBeatClock musicBeatClock,
        float elapsedMeasures,
        bool startsInEntryPhase)
    {
        if (startsInEntryPhase)
        {
            groupMovement.InitializeEntryPhase(
                musicBeatClock,
                entryStartPoint.position.y,
                normalStartPoint.position.y,
                diveStartPoint.position.y,
                diveEndPoint.position.y,
                entryPhaseMeasureCount,
                normalPhaseMeasureCount,
                diveChargeBeatCount,
                diveMovementBeatCount,
                entryDecelerationPower,
                diveChargeScaleX,
                diveChargeScaleY,
                beatSquashScaleX,
                beatSquashScaleY,
                beatScaleRestoreProgress,
                elapsedMeasures
            );
            return;
        }

        groupMovement.InitializeNormalPhase(
            musicBeatClock,
            entryStartPoint.position.y,
            normalStartPoint.position.y,
            diveStartPoint.position.y,
            diveEndPoint.position.y,
            entryPhaseMeasureCount,
            normalPhaseMeasureCount,
            elapsedMeasures,
            diveChargeBeatCount,
            diveMovementBeatCount,
            entryDecelerationPower,
            diveChargeScaleX,
            diveChargeScaleY,
            beatSquashScaleX,
            beatSquashScaleY,
            beatScaleRestoreProgress
        );
    }
}
