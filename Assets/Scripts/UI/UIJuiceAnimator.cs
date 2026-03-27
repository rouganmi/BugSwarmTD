using System.Collections;
using UnityEngine;

public class UIJuiceAnimator : MonoBehaviour
{
    [Header("Scale Pulse")]
    [SerializeField] private float pulseScale = 1.12f;
    [SerializeField] private float pulseUpTime = 0.06f;
    [SerializeField] private float pulseDownTime = 0.10f;

    private Vector3 _baseScale;
    private Coroutine _pulseRoutine;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Ensure base scale is correct when reused/disabled.
        if (_baseScale == Vector3.zero) _baseScale = transform.localScale;
    }

    public void Pulse()
    {
        if (!isActiveAndEnabled) return;

        if (_pulseRoutine != null)
        {
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }
        _pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        Vector3 from = _baseScale;
        Vector3 to = _baseScale * Mathf.Max(1f, pulseScale);

        float t = 0f;
        float up = Mathf.Max(0.001f, pulseUpTime);
        while (t < up)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / up);
            transform.localScale = Vector3.LerpUnclamped(from, to, EaseOutCubic(a));
            yield return null;
        }

        t = 0f;
        float down = Mathf.Max(0.001f, pulseDownTime);
        while (t < down)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / down);
            transform.localScale = Vector3.LerpUnclamped(to, from, EaseInCubic(a));
            yield return null;
        }

        transform.localScale = _baseScale;
        _pulseRoutine = null;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;
}

