using UnityEngine;

/// <summary>
/// 突入開始位置の線を滑らかに明滅させる。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public sealed class DiveStartLinePulse : MonoBehaviour
{
    [Header("明滅が一巡する秒数")]
    [Min(0.1f)]
    [SerializeField] private float pulsePeriodSeconds = 2f;

    [Header("明滅の最小不透明度（0～1）")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumOpacity = 0.1f;

    [Header("明滅の最大不透明度（0～1）")]
    [Range(0f, 1f)]
    [SerializeField] private float maximumOpacity = 0.5f;

    private SpriteRenderer lineRenderer;
    private float originalOpacity;
    private float pulseElapsedSeconds;

    private void Awake()
    {
        lineRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (lineRenderer == null)
        {
            return;
        }

        originalOpacity = lineRenderer.color.a;
        pulseElapsedSeconds = 0f;
        UpdateOpacity();
    }

    private void Update()
    {
        // ポーズと再開カウントダウンでは、明滅の位相も停止する。
        if (lineRenderer == null || Time.deltaTime <= 0f)
        {
            return;
        }

        float period = Mathf.Max(0.1f, pulsePeriodSeconds);
        pulseElapsedSeconds = Mathf.Repeat(
            pulseElapsedSeconds + Time.deltaTime, period);
        UpdateOpacity();
    }

    private void OnDisable()
    {
        if (lineRenderer != null)
        {
            SetOpacity(originalOpacity);
        }
    }

    /// <summary>
    /// 現在の位相に対応する不透明度を計算し、線へ反映する。
    /// </summary>
    private void UpdateOpacity()
    {
        float period = Mathf.Max(0.1f, pulsePeriodSeconds);
        float pulse = 0.5f + 0.5f * Mathf.Cos(
            pulseElapsedSeconds / period * Mathf.PI * 2f);
        float minimum = Mathf.Clamp01(Mathf.Min(minimumOpacity, maximumOpacity));
        float maximum = Mathf.Clamp01(Mathf.Max(minimumOpacity, maximumOpacity));
        SetOpacity(Mathf.Lerp(minimum, maximum, pulse));
    }

    /// <summary>
    /// 線の色を維持し、不透明度だけを変更する。
    /// </summary>
    private void SetOpacity(float opacity)
    {
        Color color = lineRenderer.color;
        color.a = opacity;
        lineRenderer.color = color;
    }
}
