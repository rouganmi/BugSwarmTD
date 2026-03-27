using System.Collections;
using UnityEngine;

public class SimpleHitFeedback : MonoBehaviour
{
    [Header("Scale Punch")]
    [SerializeField] private bool enableScalePunch = true;
    [SerializeField] private float punchScale = 1.06f;
    [SerializeField] private float punchUpTime = 0.04f;
    [SerializeField] private float punchDownTime = 0.06f;

    [Header("Flash (optional)")]
    [SerializeField] private bool enableFlash = true;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashTime = 0.05f;

    private Vector3 _baseScale;
    private Coroutine _routine;
    private MaterialPropertyBlock _mpb;
    private Renderer[] _renderers;
    private int _colorId;
    private int _baseColorId;

    private float _scaleMultiplier = 1f;
    private float _flashMultiplier = 1f;

    private void Awake()
    {
        _baseScale = transform.localScale;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();

        _colorId = Shader.PropertyToID("_Color");
        _baseColorId = Shader.PropertyToID("_BaseColor");
    }

    public void SetStrength(float multiplier)
    {
        float m = Mathf.Max(0.1f, multiplier);
        _scaleMultiplier = m;
        _flashMultiplier = m;
    }

    public void Play()
    {
        if (!isActiveAndEnabled) return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _routine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (_baseScale == Vector3.zero) _baseScale = transform.localScale;

        if (enableFlash)
        {
            ApplyFlash(1f);
        }

        if (enableScalePunch)
        {
            Vector3 from = _baseScale;
            float ps = 1f + (Mathf.Max(1f, punchScale) - 1f) * _scaleMultiplier;
            Vector3 to = _baseScale * ps;

            float up = Mathf.Max(0.001f, punchUpTime) * Mathf.Lerp(1f, 1.2f, Mathf.Clamp01(_scaleMultiplier - 1f));
            float t = 0f;
            while (t < up)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / up);
                transform.localScale = Vector3.LerpUnclamped(from, to, EaseOutCubic(a));
                yield return null;
            }

            float down = Mathf.Max(0.001f, punchDownTime) * Mathf.Lerp(1f, 1.2f, Mathf.Clamp01(_scaleMultiplier - 1f));
            t = 0f;
            while (t < down)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / down);
                transform.localScale = Vector3.LerpUnclamped(to, from, EaseInCubic(a));
                yield return null;
            }

            transform.localScale = _baseScale;
        }

        if (enableFlash)
        {
            float ft = Mathf.Max(0.001f, flashTime) * Mathf.Lerp(1f, 1.25f, Mathf.Clamp01(_flashMultiplier - 1f));
            float t = 0f;
            while (t < ft)
            {
                t += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(t / ft);
                ApplyFlash(a);
                yield return null;
            }
            ApplyFlash(0f);
        }

        _routine = null;
    }

    private void ApplyFlash(float amount01)
    {
        if (_mpb == null) return;
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null || r.sharedMaterial == null) continue;

            // Avoid passing a null/invalid destination block.
            _mpb.Clear();

            // Use whichever color property the shader supports.
            if (r.sharedMaterial.HasProperty(_baseColorId))
            {
                Color baseC = r.sharedMaterial.GetColor(_baseColorId);
                float amt = Mathf.Clamp01(amount01 * _flashMultiplier);
                _mpb.SetColor(_baseColorId, Color.LerpUnclamped(baseC, flashColor, amt));
                r.SetPropertyBlock(_mpb);
            }
            else if (r.sharedMaterial.HasProperty(_colorId))
            {
                Color baseC = r.sharedMaterial.GetColor(_colorId);
                float amt = Mathf.Clamp01(amount01 * _flashMultiplier);
                _mpb.SetColor(_colorId, Color.LerpUnclamped(baseC, flashColor, amt));
                r.SetPropertyBlock(_mpb);
            }
        }
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;
}

