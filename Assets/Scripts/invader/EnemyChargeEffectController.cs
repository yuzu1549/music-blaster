using UnityEngine;

public class EnemyChargeEffectController : MonoBehaviour
{
    [Header("チャージ中に各敵へ表示するエフェクト")]
    [SerializeField] private ParticleSystem chargeEffectPrefab;
    [Header("突撃開始時に各敵へ表示するエフェクト")]
    [SerializeField] private ParticleSystem diveStartEffectPrefab;
    [Header("敵位置からのエフェクト表示オフセット")]
    [SerializeField] private Vector3 effectOffset;

    [Space(15)]
    [Header("チャージ開始時に再生するSE")]
    [SerializeField] private AudioClip chargeStartSE;
    [Header("チャージ開始SEの音量倍率")]
    [Tooltip("1を超える値で増幅できます。大きくしすぎると音割れします")]
    [Min(0f)]
    [SerializeField] private float chargeStartSEVolume = 1f;
    [Header("突撃開始時に再生するSE")]
    [SerializeField] private AudioClip diveStartSE;
    [Header("突撃開始SEの音量倍率")]
    [Tooltip("1を超える値で増幅できます。大きくしすぎると音割れします")]
    [Min(0f)]
    [SerializeField] private float diveStartSEVolume = 1f;

    /// <summary>
    /// 敵グループへチャージ演出を追加し、Inspector設定を渡す。
    /// </summary>
    /// <param name="groupObject">演出を追加する敵グループ</param>
    /// <returns>追加した敵グループ用チャージ演出</returns>
    public EnemyGroupChargeEffect AddToGroup(GameObject groupObject)
    {
        if (groupObject == null)
        {
            return null;
        }

        EnemyGroupChargeEffect groupChargeEffect =
            groupObject.AddComponent<EnemyGroupChargeEffect>();
        groupChargeEffect.Configure(
            chargeEffectPrefab,
            diveStartEffectPrefab,
            chargeStartSE,
            diveStartSE,
            effectOffset,
            chargeStartSEVolume,
            diveStartSEVolume
        );

        return groupChargeEffect;
    }
}
