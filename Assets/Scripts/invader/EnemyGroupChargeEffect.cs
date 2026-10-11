using System.Collections.Generic;
using UnityEngine;

public class EnemyGroupChargeEffect : MonoBehaviour
{
    private sealed class EnemyEffectState
    {
        public Transform EnemyTransform { get; }
        public EnemyHealth EnemyHealth { get; }
        public ParticleSystem ChargeEffect { get; }
        public ParticleSystem DiveStartEffect { get; }

        public EnemyEffectState(
            Transform enemyTransform,
            EnemyHealth enemyHealth,
            ParticleSystem chargeEffect,
            ParticleSystem diveStartEffect)
        {
            EnemyTransform = enemyTransform;
            EnemyHealth = enemyHealth;
            ChargeEffect = chargeEffect;
            DiveStartEffect = diveStartEffect;
        }
    }

    private static readonly Dictionary<AudioClip, int> LastPlayedFrameByClip =
        new();

    private readonly List<EnemyEffectState> enemyEffectStates = new();

    private EnemyGroupMovement enemyGroupMovement;
    private ParticleSystem chargeEffectPrefab;
    private ParticleSystem diveStartEffectPrefab;
    private AudioClip chargeStartSE;
    private AudioClip diveStartSE;
    private Vector3 effectOffset;
    private float chargeStartSEVolume = 1f;
    private float diveStartSEVolume = 1f;
    private bool isSubscribed;

    private void Awake()
    {
        enemyGroupMovement = GetComponent<EnemyGroupMovement>();
    }

    private void OnEnable()
    {
        SubscribeToMovementEvents();
    }

    private void LateUpdate()
    {
        foreach (EnemyEffectState effectState in enemyEffectStates)
        {
            if (!IsEnemyActive(effectState))
            {
                StopEffect(effectState.ChargeEffect);
                StopEffect(effectState.DiveStartEffect);
                continue;
            }

            Vector3 effectPosition =
                effectState.EnemyTransform.position + effectOffset;
            UpdateEffectPosition(effectState.ChargeEffect, effectPosition);
            UpdateEffectPosition(effectState.DiveStartEffect, effectPosition);
            DeactivateFinishedEffect(effectState.DiveStartEffect);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromMovementEvents();
        StopAllEffects();
    }

    /// <summary>
    /// チャージ演出に使用するPrefab、SE、調整値を設定する。
    /// </summary>
    /// <param name="newChargeEffectPrefab">チャージ中のエフェクト</param>
    /// <param name="newDiveStartEffectPrefab">突撃開始時のエフェクト</param>
    /// <param name="newChargeStartSE">チャージ開始時のSE</param>
    /// <param name="newDiveStartSE">突撃開始時のSE</param>
    /// <param name="newEffectOffset">敵位置からのエフェクト表示オフセット</param>
    /// <param name="newChargeStartSEVolume">チャージ開始SEの音量倍率</param>
    /// <param name="newDiveStartSEVolume">突撃開始SEの音量倍率</param>
    public void Configure(
        ParticleSystem newChargeEffectPrefab,
        ParticleSystem newDiveStartEffectPrefab,
        AudioClip newChargeStartSE,
        AudioClip newDiveStartSE,
        Vector3 newEffectOffset,
        float newChargeStartSEVolume,
        float newDiveStartSEVolume)
    {
        chargeEffectPrefab = newChargeEffectPrefab;
        diveStartEffectPrefab = newDiveStartEffectPrefab;
        chargeStartSE = newChargeStartSE;
        diveStartSE = newDiveStartSE;
        effectOffset = newEffectOffset;
        chargeStartSEVolume = Mathf.Max(0f, newChargeStartSEVolume);
        diveStartSEVolume = Mathf.Max(0f, newDiveStartSEVolume);
    }

    /// <summary>
    /// チャージ演出を表示する敵を登録し、演出を事前生成する。
    /// </summary>
    /// <param name="enemyTransform">登録する敵のTransform</param>
    public void RegisterEnemy(Transform enemyTransform)
    {
        if (enemyTransform == null)
        {
            return;
        }

        enemyTransform.TryGetComponent(out EnemyHealth enemyHealth);
        ParticleSystem chargeEffect = CreateEffect(
            chargeEffectPrefab,
            enemyTransform,
            "ChargeEffect"
        );
        ParticleSystem diveStartEffect = CreateEffect(
            diveStartEffectPrefab,
            enemyTransform,
            "DiveStartEffect"
        );

        enemyEffectStates.Add(
            new EnemyEffectState(
                enemyTransform,
                enemyHealth,
                chargeEffect,
                diveStartEffect
            )
        );
    }

    /// <summary>
    /// 敵グループのチャージ開始に合わせて各敵の演出とSEを再生する。
    /// </summary>
    private void HandleDiveChargeStarted()
    {
        if (!PlayEffects(isDiveStart: false))
        {
            return;
        }

        PlaySEOncePerFrame(chargeStartSE, chargeStartSEVolume);
    }

    /// <summary>
    /// 敵グループの突撃開始に合わせて各敵の演出とSEを再生する。
    /// </summary>
    private void HandleDiveStarted()
    {
        StopChargeEffects();

        if (!PlayEffects(isDiveStart: true))
        {
            return;
        }

        PlaySEOncePerFrame(diveStartSE, diveStartSEVolume);
    }

    /// <summary>
    /// 指定された種類のエフェクトを生存中の敵へ再生する。
    /// </summary>
    /// <param name="isDiveStart">突撃開始エフェクトを再生する場合はtrue</param>
    /// <returns>演出対象となる敵が存在した場合はtrue</returns>
    private bool PlayEffects(bool isDiveStart)
    {
        bool hasActiveEnemy = false;

        foreach (EnemyEffectState effectState in enemyEffectStates)
        {
            if (!IsEnemyActive(effectState))
            {
                continue;
            }

            hasActiveEnemy = true;
            ParticleSystem effect = isDiveStart
                ? effectState.DiveStartEffect
                : effectState.ChargeEffect;
            PlayEffect(
                effect,
                effectState.EnemyTransform.position + effectOffset
            );
        }

        return hasActiveEnemy;
    }

    /// <summary>
    /// エフェクトPrefabをグループ配下へ生成し、停止状態で保持する。
    /// </summary>
    /// <param name="effectPrefab">生成するParticle System Prefab</param>
    /// <param name="enemyTransform">初期表示位置に使う敵</param>
    /// <param name="effectName">生成後のオブジェクト名</param>
    /// <returns>生成したParticle System。Prefab未設定時はnull</returns>
    private ParticleSystem CreateEffect(
        ParticleSystem effectPrefab,
        Transform enemyTransform,
        string effectName)
    {
        if (effectPrefab == null)
        {
            return null;
        }

        ParticleSystem effect = Instantiate(effectPrefab, transform);
        effect.name = $"{enemyTransform.name}_{effectName}";
        effect.transform.position = enemyTransform.position + effectOffset;
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.gameObject.SetActive(false);
        return effect;
    }

    /// <summary>
    /// 登録された敵が現在も同じグループで生存しているかを返す。
    /// </summary>
    /// <param name="effectState">確認する敵と演出の状態</param>
    /// <returns>演出対象として有効な場合はtrue</returns>
    private bool IsEnemyActive(EnemyEffectState effectState)
    {
        return effectState.EnemyTransform != null &&
            effectState.EnemyTransform.gameObject.activeInHierarchy &&
            effectState.EnemyTransform.parent == transform &&
            (effectState.EnemyHealth == null ||
                effectState.EnemyHealth.currentHealth > 0);
    }

    /// <summary>
    /// Particle Systemを指定位置から再生する。
    /// </summary>
    /// <param name="effect">再生するParticle System</param>
    /// <param name="position">表示するワールド座標</param>
    private void PlayEffect(ParticleSystem effect, Vector3 position)
    {
        if (effect == null)
        {
            return;
        }

        effect.transform.position = position;
        effect.gameObject.SetActive(true);
        effect.Play(true);
    }

    /// <summary>
    /// 再生中のエフェクトを敵の現在位置へ追従させる。
    /// </summary>
    /// <param name="effect">位置を更新するParticle System</param>
    /// <param name="position">表示するワールド座標</param>
    private void UpdateEffectPosition(ParticleSystem effect, Vector3 position)
    {
        if (effect == null || !effect.gameObject.activeSelf)
        {
            return;
        }

        effect.transform.position = position;
    }

    /// <summary>
    /// 再生を終えた単発エフェクトを非表示にする。
    /// </summary>
    /// <param name="effect">確認するParticle System</param>
    private void DeactivateFinishedEffect(ParticleSystem effect)
    {
        if (effect == null || !effect.gameObject.activeSelf ||
            effect.IsAlive(true))
        {
            return;
        }

        effect.gameObject.SetActive(false);
    }

    /// <summary>
    /// 全てのチャージエフェクトを停止する。
    /// </summary>
    private void StopChargeEffects()
    {
        foreach (EnemyEffectState effectState in enemyEffectStates)
        {
            StopEffect(effectState.ChargeEffect);
        }
    }

    /// <summary>
    /// 管理している全エフェクトを停止する。
    /// </summary>
    private void StopAllEffects()
    {
        foreach (EnemyEffectState effectState in enemyEffectStates)
        {
            StopEffect(effectState.ChargeEffect);
            StopEffect(effectState.DiveStartEffect);
        }
    }

    /// <summary>
    /// Particle Systemを停止して非表示にする。
    /// </summary>
    /// <param name="effect">停止するParticle System</param>
    private void StopEffect(ParticleSystem effect)
    {
        if (effect == null || !effect.gameObject.activeSelf)
        {
            return;
        }

        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.gameObject.SetActive(false);
    }

    /// <summary>
    /// 同じAudioClipを同一フレーム内で一度だけ再生する。
    /// </summary>
    /// <param name="clip">再生するSE</param>
    /// <param name="volumeScale">SEの音量倍率</param>
    private static void PlaySEOncePerFrame(AudioClip clip, float volumeScale)
    {
        if (clip == null || AudioManager.Instance == null)
        {
            return;
        }

        if (LastPlayedFrameByClip.TryGetValue(clip, out int lastPlayedFrame) &&
            lastPlayedFrame == Time.frameCount)
        {
            return;
        }

        LastPlayedFrameByClip[clip] = Time.frameCount;
        AudioManager.Instance.PlaySE(clip, volumeScale);
    }

    /// <summary>
    /// 敵グループ移動の演出イベントを購読する。
    /// </summary>
    private void SubscribeToMovementEvents()
    {
        if (isSubscribed || enemyGroupMovement == null)
        {
            return;
        }

        enemyGroupMovement.DiveChargeStarted += HandleDiveChargeStarted;
        enemyGroupMovement.DiveStarted += HandleDiveStarted;
        enemyGroupMovement.CombatStopped += StopAllEffects;
        isSubscribed = true;
    }

    /// <summary>
    /// 敵グループ移動の演出イベント購読を解除する。
    /// </summary>
    private void UnsubscribeFromMovementEvents()
    {
        if (!isSubscribed || enemyGroupMovement == null)
        {
            return;
        }

        enemyGroupMovement.DiveChargeStarted -= HandleDiveChargeStarted;
        enemyGroupMovement.DiveStarted -= HandleDiveStarted;
        enemyGroupMovement.CombatStopped -= StopAllEffects;
        isSubscribed = false;
    }
}
