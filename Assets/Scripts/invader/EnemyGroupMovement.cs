using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyGroupMovement : MonoBehaviour
{
    private enum MovementPhase
    {
        Entry,
        Normal,
        Dive,
        Completed
    }

    private sealed class EnemyVisualState
    {
        public Transform EnemyTransform { get; }
        public SpriteRenderer SpriteRenderer { get; }
        public Vector3 BaseLocalPosition { get; }
        public Vector3 BaseLocalScale { get; }

        public EnemyVisualState(
            Transform enemyTransform,
            SpriteRenderer spriteRenderer)
        {
            EnemyTransform = enemyTransform;
            SpriteRenderer = spriteRenderer;
            BaseLocalPosition = enemyTransform.localPosition;
            BaseLocalScale = enemyTransform.localScale;
        }
    }

    private readonly List<EnemyVisualState> enemyVisualStates = new();

    private MusicBeatClock musicBeatClock;
    private EnemyGroupController enemyGroupController;
    private MovementPhase currentPhase;
    private float entryStartY;
    private float normalStartY;
    private float diveStartY;
    private float diveEndY;
    private int entryPhaseBeats;
    private int normalPhaseBeats;
    private int diveChargeBeats = 3;
    private int diveMovementBeats = 1;
    private int elapsedNormalPhaseBeats;
    private float entryTargetY;
    private int continuousPhaseStartBeat;
    private float entryDecelerationPower;
    private float diveChargeScaleX;
    private float diveChargeScaleY;
    private float squashScaleX;
    private float squashScaleY;
    private float restoreEndBeatProgress;
    private bool isInitialized;
    private bool isSubscribed;
    private bool isRestoringScale;
    private bool hasStartedDiveMovement;
    private bool hasStoppedCombat;

    /// <summary>終了拍でチャージ演出を停止するための通知。</summary>
    public event Action CombatStopped;

    private int DivePhaseBeats => diveChargeBeats + diveMovementBeats;

    /// <summary>
    /// 現在、敵グループが通常フェーズ中かを表す。
    /// </summary>
    public bool IsInNormalPhase => currentPhase == MovementPhase.Normal;

    /// <summary>
    /// 現在、敵グループが突入フェーズ中かを表す。
    /// </summary>
    public bool IsInDivePhase => currentPhase == MovementPhase.Dive;

    /// <summary>
    /// 敵グループが突入前のチャージを開始したときに通知する。
    /// </summary>
    public event Action DiveChargeStarted;

    /// <summary>
    /// 敵グループがチャージを終えて突撃を開始したときに通知する。
    /// </summary>
    public event Action DiveStarted;

    private void OnEnable()
    {
        TrySubscribeToBeatClock();
    }

    private void OnDisable()
    {
        UnsubscribeFromBeatClock();
        RestoreEnemyVisuals();
    }

    private void Update()
    {
        if (musicBeatClock != null && musicBeatClock.IsCombatStopped &&
            !hasStoppedCombat)
        {
            hasStoppedCombat = true;
            CombatStopped?.Invoke();
        }

        if (!isInitialized || musicBeatClock == null || musicBeatClock.IsCombatStopped)
        {
            return;
        }

        UpdateContinuousPhaseMovement();
        UpdateScaleRestoration();
    }

    /// <summary>
    /// 初期配置グループを通常フェーズの途中から開始する。
    /// </summary>
    /// <param name="beatClock">楽曲の拍を通知するクロック</param>
    /// <param name="entryY">進入フェーズ開始時のY座標</param>
    /// <param name="normalY">通常フェーズ開始時のY座標</param>
    /// <param name="diveY">突入フェーズ開始時のY座標</param>
    /// <param name="endY">突入フェーズ終了時のY座標</param>
    /// <param name="entryMeasures">進入フェーズの小節数</param>
    /// <param name="normalMeasures">通常フェーズの小節数</param>
    /// <param name="elapsedNormalMeasures">通常フェーズで経過済みとする小節数</param>
    /// <param name="chargeBeats">突入前にチャージする拍数</param>
    /// <param name="movementBeats">突入移動に使う拍数</param>
    /// <param name="entryPower">進入時の減速指数</param>
    /// <param name="chargeScaleX">突入チャージ完了時の横方向倍率</param>
    /// <param name="chargeScaleY">突入チャージ完了時の縦方向倍率</param>
    /// <param name="beatSquashScaleX">拍先頭で適用する横方向の倍率</param>
    /// <param name="beatSquashScaleY">拍先頭で適用する縦方向の倍率</param>
    /// <param name="restoreBeatProgress">元の大きさへ戻り終える拍内進行度</param>
    public void InitializeNormalPhase(
        MusicBeatClock beatClock,
        float entryY,
        float normalY,
        float diveY,
        float endY,
        int entryMeasures,
        int normalMeasures,
        float elapsedNormalMeasures,
        int chargeBeats,
        int movementBeats,
        float entryPower,
        float chargeScaleX,
        float chargeScaleY,
        float beatSquashScaleX,
        float beatSquashScaleY,
        float restoreBeatProgress)
    {
        Configure(
            beatClock,
            entryY,
            normalY,
            diveY,
            endY,
            entryMeasures,
            normalMeasures,
            chargeBeats,
            movementBeats,
            entryPower,
            chargeScaleX,
            chargeScaleY,
            beatSquashScaleX,
            beatSquashScaleY,
            restoreBeatProgress
        );

        if (!isInitialized)
        {
            return;
        }

        currentPhase = MovementPhase.Normal;
        // 0拍目の通知で最初の移動を行うため、初期状態は1拍手前にする。
        elapsedNormalPhaseBeats = Mathf.Clamp(
            Mathf.RoundToInt(
                elapsedNormalMeasures * musicBeatClock.BeatsPerMeasure
            ) - 1,
            0,
            normalPhaseBeats
        );
        UpdateNormalPhasePosition();
        TrySubscribeToBeatClock();
    }

    /// <summary>
    /// 新しく生成したグループを進入フェーズから開始する。
    /// </summary>
    /// <param name="beatClock">楽曲の拍を通知するクロック</param>
    /// <param name="entryY">進入フェーズ開始時のY座標</param>
    /// <param name="normalY">通常フェーズ開始時のY座標</param>
    /// <param name="diveY">突入フェーズ開始時のY座標</param>
    /// <param name="endY">突入フェーズ終了時のY座標</param>
    /// <param name="entryMeasures">進入フェーズの小節数</param>
    /// <param name="normalMeasures">通常フェーズの小節数</param>
    /// <param name="chargeBeats">突入前にチャージする拍数</param>
    /// <param name="movementBeats">突入移動に使う拍数</param>
    /// <param name="entryPower">進入時の減速指数</param>
    /// <param name="chargeScaleX">突入チャージ完了時の横方向倍率</param>
    /// <param name="chargeScaleY">突入チャージ完了時の縦方向倍率</param>
    /// <param name="beatSquashScaleX">拍先頭で適用する横方向の倍率</param>
    /// <param name="beatSquashScaleY">拍先頭で適用する縦方向の倍率</param>
    /// <param name="restoreBeatProgress">元の大きさへ戻り終える拍内進行度</param>
    public void InitializeEntryPhase(
        MusicBeatClock beatClock,
        float entryY,
        float normalY,
        float diveY,
        float endY,
        int entryMeasures,
        int normalMeasures,
        int chargeBeats,
        int movementBeats,
        float entryPower,
        float chargeScaleX,
        float chargeScaleY,
        float beatSquashScaleX,
        float beatSquashScaleY,
        float restoreBeatProgress,
        float elapsedNormalMeasures = 0f)
    {
        Configure(
            beatClock,
            entryY,
            normalY,
            diveY,
            endY,
            entryMeasures,
            normalMeasures,
            chargeBeats,
            movementBeats,
            entryPower,
            chargeScaleX,
            chargeScaleY,
            beatSquashScaleX,
            beatSquashScaleY,
            restoreBeatProgress
        );

        if (!isInitialized)
        {
            return;
        }

        currentPhase = MovementPhase.Entry;
        elapsedNormalPhaseBeats = Mathf.Clamp(
            Mathf.RoundToInt(elapsedNormalMeasures * musicBeatClock.BeatsPerMeasure),
            0, normalPhaseBeats - 1);
        entryTargetY = GetNormalPositionY(normalStartY, diveStartY,
            elapsedNormalPhaseBeats, normalPhaseBeats);
        continuousPhaseStartBeat = Mathf.Max(
            0,
            musicBeatClock.CurrentBeatIndex
        );
        SetGroupY(entryStartY);
        TrySubscribeToBeatClock();
    }

    /// <summary>
    /// 拍に合わせて伸縮させる敵を登録する。
    /// </summary>
    /// <param name="enemyTransform">登録する敵のTransform</param>
    public void RegisterEnemy(Transform enemyTransform)
    {
        if (enemyTransform == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer =
            enemyTransform.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"{enemyTransform.name}: SpriteRendererが見つかりません。");
            return;
        }

        enemyVisualStates.Add(
            new EnemyVisualState(enemyTransform, spriteRenderer)
        );
    }

    /// <summary>
    /// 各フェーズで共通して使う移動設定を保存する。
    /// </summary>
    private void Configure(
        MusicBeatClock beatClock,
        float entryY,
        float normalY,
        float diveY,
        float endY,
        int entryMeasures,
        int normalMeasures,
        int chargeBeats,
        int movementBeats,
        float entryPower,
        float chargeScaleX,
        float chargeScaleY,
        float beatSquashScaleX,
        float beatSquashScaleY,
        float restoreBeatProgress)
    {
        UnsubscribeFromBeatClock();

        musicBeatClock = beatClock;
        enemyGroupController = GetComponent<EnemyGroupController>();
        entryStartY = entryY;
        normalStartY = normalY;
        diveStartY = diveY;
        diveEndY = endY;
        diveChargeBeats = Mathf.Max(1, chargeBeats);
        diveMovementBeats = Mathf.Max(1, movementBeats);
        entryDecelerationPower = Mathf.Max(1f, entryPower);
        diveChargeScaleX = Mathf.Max(0.01f, chargeScaleX);
        diveChargeScaleY = Mathf.Max(0.01f, chargeScaleY);
        squashScaleX = Mathf.Max(0.01f, beatSquashScaleX);
        squashScaleY = Mathf.Max(0.01f, beatSquashScaleY);
        restoreEndBeatProgress = Mathf.Clamp(restoreBeatProgress, 0.01f, 1f);

        int beatsPerMeasure = musicBeatClock != null
            ? musicBeatClock.BeatsPerMeasure
            : 0;
        entryPhaseBeats = Mathf.Max(1, entryMeasures * beatsPerMeasure);
        normalPhaseBeats = Mathf.Max(1, normalMeasures * beatsPerMeasure);
        elapsedNormalPhaseBeats = 0;
        hasStartedDiveMovement = false;
        isInitialized = musicBeatClock != null &&
            enemyGroupController != null && beatsPerMeasure > 0;
    }

    /// <summary>
    /// 拍の先頭で現在のフェーズを進行させる。
    /// </summary>
    /// <param name="beatIndex">曲全体での拍番号</param>
    private void HandleBeat(int beatIndex)
    {
        if (!isInitialized)
        {
            return;
        }

        if (currentPhase == MovementPhase.Entry)
        {
            if (beatIndex - continuousPhaseStartBeat < entryPhaseBeats)
            {
                return;
            }

            currentPhase = MovementPhase.Normal;
            UpdateNormalPhasePosition();
            StartBeatScaleAnimation();
            return;
        }

        if (currentPhase != MovementPhase.Normal)
        {
            return;
        }

        elapsedNormalPhaseBeats++;
        UpdateNormalPhasePosition();
        StartBeatScaleAnimation();

        if (elapsedNormalPhaseBeats < normalPhaseBeats)
        {
            return;
        }

        currentPhase = MovementPhase.Dive;
        continuousPhaseStartBeat = beatIndex;
        SetGroupY(diveStartY);
        RestoreEnemyVisuals();
        DiveChargeStarted?.Invoke();
    }

    /// <summary>
    /// 進入または突入フェーズの連続移動を現在の拍位置から更新する。
    /// </summary>
    private void UpdateContinuousPhaseMovement()
    {
        if (currentPhase != MovementPhase.Entry &&
            currentPhase != MovementPhase.Dive)
        {
            return;
        }

        int phaseDurationBeats = currentPhase == MovementPhase.Entry
            ? entryPhaseBeats
            : DivePhaseBeats;
        float currentBeatPosition = musicBeatClock.CurrentBeatIndex +
            musicBeatClock.BeatProgress;
        float phaseProgress = Mathf.Clamp01(
            (currentBeatPosition - continuousPhaseStartBeat) /
            phaseDurationBeats
        );

        if (currentPhase == MovementPhase.Entry)
        {
            float easedProgress = 1f - Mathf.Pow(
                1f - phaseProgress,
                entryDecelerationPower
            );
            SetGroupY(Mathf.Lerp(entryStartY, entryTargetY, easedProgress));
            return;
        }

        UpdateDivePhase(phaseProgress);

        if (phaseProgress >= 1f)
        {
            CompleteMovement();
        }
    }

    /// <summary>
    /// 突入フェーズのチャージと等速移動を更新する。
    /// </summary>
    /// <param name="phaseProgress">突入フェーズ全体の進行度</param>
    private void UpdateDivePhase(float phaseProgress)
    {
        float chargeEndProgress =
            (float)diveChargeBeats / DivePhaseBeats;

        if (phaseProgress < chargeEndProgress)
        {
            float chargeProgress = phaseProgress / chargeEndProgress;
            float currentScaleX = Mathf.Lerp(
                1f,
                diveChargeScaleX,
                chargeProgress
            );
            float currentScaleY = Mathf.Lerp(
                1f,
                diveChargeScaleY,
                chargeProgress
            );

            SetGroupY(diveStartY);
            ApplyEnemyVisualScale(currentScaleX, currentScaleY);
            return;
        }

        NotifyDiveStarted();
        ApplyEnemyVisualScale(1f, 1f);

        float movementProgress = Mathf.InverseLerp(
            chargeEndProgress,
            1f,
            phaseProgress
        );
        SetGroupY(Mathf.Lerp(diveStartY, diveEndY, movementProgress));
    }

    /// <summary>
    /// 突撃開始を一度だけ通知する。
    /// </summary>
    private void NotifyDiveStarted()
    {
        if (hasStartedDiveMovement)
        {
            return;
        }

        hasStartedDiveMovement = true;
        DiveStarted?.Invoke();
    }

    /// <summary>
    /// 通常フェーズの経過拍数からグループのY座標を更新する。
    /// </summary>
    private void UpdateNormalPhasePosition()
    {
        SetGroupY(GetNormalPositionY(normalStartY, diveStartY,
            elapsedNormalPhaseBeats, normalPhaseBeats));
    }

    /// <summary>
    /// 通常フェーズの拍位置に対応するY座標を計算する。
    /// </summary>
    /// <returns>通常フェーズのワールドY座標</returns>
    public static float GetNormalPositionY(
        float startY, float endY, int elapsedBeats, int durationBeats)
    {
        return Mathf.Lerp(startY, endY,
            (float)(elapsedBeats + 1) / (durationBeats + 1));
    }

    /// <summary>
    /// 指定した将来の拍で生存している場合、そのグループ位置を予測する。
    /// 演出・イベント・実際の移動状態は変更しない。
    /// </summary>
    /// <param name="targetBeat">進入完了時の絶対拍位置</param>
    /// <returns>指定拍でもグループが存在する場合はtrue</returns>
    public bool TryPredictPositionY(float targetBeat, out float positionY)
    {
        positionY = transform.position.y;
        if (!isInitialized || currentPhase == MovementPhase.Completed)
        {
            return false;
        }

        if (musicBeatClock.IsCombatStopped)
        {
            return true;
        }

        float normalEndBeat;
        int normalElapsed;
        if (currentPhase == MovementPhase.Entry)
        {
            float entryEndBeat = continuousPhaseStartBeat + entryPhaseBeats;
            if (targetBeat < entryEndBeat)
            {
                float progress = Mathf.Clamp01(
                    (targetBeat - continuousPhaseStartBeat) / entryPhaseBeats);
                float eased = 1f - Mathf.Pow(1f - progress, entryDecelerationPower);
                positionY = Mathf.Lerp(entryStartY, entryTargetY, eased);
                return true;
            }

            normalElapsed = elapsedNormalPhaseBeats +
                Mathf.FloorToInt(targetBeat - entryEndBeat);
            normalEndBeat = entryEndBeat + normalPhaseBeats - elapsedNormalPhaseBeats;
        }
        else if (currentPhase == MovementPhase.Normal)
        {
            int currentBeat = musicBeatClock.CurrentBeatIndex;
            normalElapsed = elapsedNormalPhaseBeats +
                Mathf.Max(0, Mathf.FloorToInt(targetBeat) - currentBeat);
            normalEndBeat = currentBeat + normalPhaseBeats - elapsedNormalPhaseBeats;
        }
        else
        {
            normalElapsed = normalPhaseBeats;
            normalEndBeat = continuousPhaseStartBeat;
        }

        if (normalElapsed < normalPhaseBeats)
        {
            positionY = GetNormalPositionY(normalStartY, diveStartY,
                normalElapsed, normalPhaseBeats);
            return true;
        }

        float diveElapsed = targetBeat - normalEndBeat;
        if (diveElapsed >= DivePhaseBeats)
        {
            return false;
        }

        float diveProgress = Mathf.Clamp01(
            (diveElapsed - diveChargeBeats) / diveMovementBeats);
        positionY = Mathf.Lerp(diveStartY, diveEndY, diveProgress);
        return true;
    }

    /// <summary>
    /// グループのY座標を指定位置へ変更する。
    /// </summary>
    /// <param name="positionY">変更後のワールドY座標</param>
    private void SetGroupY(float positionY)
    {
        Vector3 groupPosition = transform.position;
        groupPosition.y = positionY;
        transform.position = groupPosition;
    }

    /// <summary>
    /// 拍先頭の伸縮を適用し、元の大きさへの復元を開始する。
    /// </summary>
    private void StartBeatScaleAnimation()
    {
        isRestoringScale = true;
        ApplyEnemyVisualScale(squashScaleX, squashScaleY);
    }

    /// <summary>
    /// 拍内進行度に合わせて敵を元の大きさへ戻す。
    /// </summary>
    private void UpdateScaleRestoration()
    {
        if (!isRestoringScale)
        {
            return;
        }

        float restoreProgress = Mathf.Clamp01(
            musicBeatClock.BeatProgress / restoreEndBeatProgress
        );
        float currentScaleX = Mathf.Lerp(squashScaleX, 1f, restoreProgress);
        float currentScaleY = Mathf.Lerp(squashScaleY, 1f, restoreProgress);

        ApplyEnemyVisualScale(currentScaleX, currentScaleY);

        if (restoreProgress >= 1f)
        {
            isRestoringScale = false;
        }
    }

    /// <summary>
    /// 敵の足元を維持しながら見た目の拡縮率を適用する。
    /// </summary>
    /// <param name="scaleX">元の大きさに対する横方向の倍率</param>
    /// <param name="scaleY">元の大きさに対する縦方向の倍率</param>
    private void ApplyEnemyVisualScale(float scaleX, float scaleY)
    {
        foreach (EnemyVisualState visualState in enemyVisualStates)
        {
            Transform enemyTransform = visualState.EnemyTransform;
            if (enemyTransform == null ||
                !enemyTransform.gameObject.activeInHierarchy ||
                enemyTransform.parent != transform)
            {
                continue;
            }

            enemyTransform.localPosition = visualState.BaseLocalPosition;
            enemyTransform.localScale = visualState.BaseLocalScale;
            float baseBottomY = visualState.SpriteRenderer.bounds.min.y;

            Vector3 scaledLocalScale = visualState.BaseLocalScale;
            scaledLocalScale.x *= scaleX;
            scaledLocalScale.y *= scaleY;
            enemyTransform.localScale = scaledLocalScale;

            float scaledBottomY = visualState.SpriteRenderer.bounds.min.y;
            enemyTransform.position += Vector3.up *
                (baseBottomY - scaledBottomY);
        }
    }

    /// <summary>
    /// 登録されている敵を元の位置と大きさへ戻す。
    /// </summary>
    private void RestoreEnemyVisuals()
    {
        foreach (EnemyVisualState visualState in enemyVisualStates)
        {
            Transform enemyTransform = visualState.EnemyTransform;
            if (enemyTransform == null || enemyTransform.parent != transform)
            {
                continue;
            }

            enemyTransform.localPosition = visualState.BaseLocalPosition;
            enemyTransform.localScale = visualState.BaseLocalScale;
        }

        isRestoringScale = false;
    }

    /// <summary>
    /// 突入フェーズを完了し、残っている敵を得点なしでプールへ戻す。
    /// </summary>
    private void CompleteMovement()
    {
        if (currentPhase == MovementPhase.Completed)
        {
            return;
        }

        currentPhase = MovementPhase.Completed;
        RestoreEnemyVisuals();
        UnsubscribeFromBeatClock();
        enemyGroupController.DespawnRemainingEnemies();
    }

    /// <summary>
    /// 拍通知を受け取れる場合にMusicBeatClockを購読する。
    /// </summary>
    private void TrySubscribeToBeatClock()
    {
        if (!isActiveAndEnabled || !isInitialized || isSubscribed)
        {
            return;
        }

        musicBeatClock.OnBeat += HandleBeat;
        isSubscribed = true;
    }

    /// <summary>
    /// MusicBeatClockの拍通知購読を解除する。
    /// </summary>
    private void UnsubscribeFromBeatClock()
    {
        if (!isSubscribed || musicBeatClock == null)
        {
            return;
        }

        musicBeatClock.OnBeat -= HandleBeat;
        isSubscribed = false;
    }
}
