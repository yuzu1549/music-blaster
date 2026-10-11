using UnityEngine;

[CreateAssetMenu(
    fileName = "ScoreRankSettings",
    menuName = "Data/Score Rank Settings")]
public sealed class ScoreRankSettings : ScriptableObject
{
    public const float DefaultSPercent = 80f;
    public const float DefaultAPercent = 60f;
    public const float DefaultBPercent = 40f;
    public const float DefaultCPercent = 20f;

    [Header("Sランクに必要な達成率（％）")]
    [Range(0f, 100f)]
    [SerializeField] private float sPercent = DefaultSPercent;

    [Header("Aランクに必要な達成率（％）")]
    [Tooltip("上位ランクの達成率を超える値は自動補正されます")]
    [Range(0f, 100f)]
    [SerializeField] private float aPercent = DefaultAPercent;

    [Header("Bランクに必要な達成率（％）")]
    [Tooltip("上位ランクの達成率を超える値は自動補正されます")]
    [Range(0f, 100f)]
    [SerializeField] private float bPercent = DefaultBPercent;

    [Header("Cランクに必要な達成率（％）")]
    [Tooltip("上位ランクの達成率を超える値は自動補正されます")]
    [Range(0f, 100f)]
    [SerializeField] private float cPercent = DefaultCPercent;

    public float SPercent => sPercent;
    public float APercent => aPercent;
    public float BPercent => bPercent;
    public float CPercent => cPercent;

    private void OnValidate()
    {
        sPercent = Mathf.Clamp(sPercent, 0f, 100f);
        aPercent = Mathf.Clamp(aPercent, 0f, sPercent);
        bPercent = Mathf.Clamp(bPercent, 0f, aPercent);
        cPercent = Mathf.Clamp(cPercent, 0f, bPercent);
    }
}
