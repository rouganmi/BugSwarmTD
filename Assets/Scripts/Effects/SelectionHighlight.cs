using UnityEngine;
using System.Collections.Generic;

public class SelectionHighlight : MonoBehaviour
{
    [SerializeField] private Color highlightColor;
    [SerializeField, Range(0f, 1f)] private float intensity = 0.52f;
    [Header("Polish")]
    [SerializeField] private bool pulseWhenSelected = true;
    [SerializeField, Range(0f, 0.35f)] private float pulseAmplitude = 0.16f;
    [SerializeField, Range(0.2f, 6f)] private float pulseSpeed = 1.65f;

    [Header("Type styling (auto)")]
    [SerializeField] private bool autoDifferentiateTypes = true;
    [SerializeField] private Color towerColor = new Color(0.18f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color enemyColor = new Color(1.0f, 0.36f, 0.12f, 1f);

    [Header("Ground ring")]
    [SerializeField] private bool showGroundRing = true;
    [SerializeField, Range(0.4f, 6f)] private float ringRadius = 1.25f;
    [SerializeField, Range(0.01f, 0.12f)] private float ringWidth = 0.045f;
    [SerializeField, Range(16, 96)] private int ringSegments = 48;
    [SerializeField] private float ringY = 0.04f;

    private Renderer[] _renderers;
    private bool _isOn;
    float _pulseT;
    LineRenderer _ring;
    Transform _ringT;

    private struct ColorCacheEntry
    {
        public Material material;
        public string propertyName; // "_BaseColor" or "_Color"
        public Color original;
    }

    private readonly List<ColorCacheEntry> _colorCache = new List<ColorCacheEntry>(16);

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        if (autoDifferentiateTypes)
        {
            // Purely visual categorization: Tower = calm teal, Enemy = warning orange.
            if (GetComponent<Tower>() != null || GetComponentInParent<Tower>() != null)
                highlightColor = towerColor;
            else if (GetComponent<Enemy>() != null || GetComponentInParent<Enemy>() != null)
                highlightColor = enemyColor;
        }

        if (highlightColor.a <= 0.0001f)
            highlightColor = towerColor;

        CacheOriginalColors();
        EnsureRingBuilt();
        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        _isOn = value;
        _pulseT = 0f;
        ApplyTint(_isOn ? intensity : 0f);
        if (_ring != null)
            _ring.enabled = _isOn && showGroundRing;
    }

    private void Update()
    {
        if (!_isOn || !pulseWhenSelected)
        {
            if (_ringT != null)
                _ringT.rotation = Quaternion.identity;
            return;
        }

        _pulseT += Time.unscaledDeltaTime * pulseSpeed;
        float s = (Mathf.Sin(_pulseT) + 1f) * 0.5f; // 0..1
        float a = Mathf.Clamp01(intensity + (s - 0.5f) * 2f * pulseAmplitude);
        ApplyTint(a);

        if (_ring != null)
        {
            Color c = highlightColor;
            c.a = Mathf.Clamp01(0.28f + a * 0.45f);
            _ring.startColor = c;
            _ring.endColor = c;
        }

        if (_ringT != null)
            _ringT.rotation = Quaternion.identity;
    }

    void EnsureRingBuilt()
    {
        if (!showGroundRing || _ring != null)
            return;

        var go = new GameObject("SelectionRing");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, ringY, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _ringT = go.transform;

        _ring = go.AddComponent<LineRenderer>();
        _ring.enabled = false;
        _ring.loop = true;
        _ring.useWorldSpace = false;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;
        _ring.allowOcclusionWhenDynamic = false;
        _ring.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _ring.widthMultiplier = ringWidth;
        _ring.numCapVertices = 4;
        _ring.numCornerVertices = 2;
        _ring.positionCount = Mathf.Clamp(ringSegments, 16, 96);

        // Built-in, safe material (no project dependency).
        _ring.material = new Material(Shader.Find("Sprites/Default"));

        UpdateRingGeometry();
    }

    void UpdateRingGeometry()
    {
        if (_ring == null)
            return;

        int n = _ring.positionCount;
        float r = Mathf.Max(0.001f, ringRadius);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float a = t * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * r;
            float z = Mathf.Sin(a) * r;
            _ring.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    private void ApplyTint(float amount01)
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (_colorCache == null || _colorCache.Count == 0) return;

        float a = Mathf.Clamp01(amount01);

        if (a <= 0f)
        {
            RestoreOriginalColors();
            return;
        }

        for (int i = 0; i < _colorCache.Count; i++)
        {
            var e = _colorCache[i];
            if (e.material == null || string.IsNullOrEmpty(e.propertyName)) continue;

            Color target = Color.LerpUnclamped(e.original, highlightColor, a);
            e.material.SetColor(e.propertyName, target);
        }
    }

    private void CacheOriginalColors()
    {
        _colorCache.Clear();
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;

            Material[] mats;
            try
            {
                mats = r.materials;
            }
            catch
            {
                continue;
            }

            if (mats == null) continue;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;

                if (mat.HasProperty("_BaseColor"))
                {
                    _colorCache.Add(new ColorCacheEntry
                    {
                        material = mat,
                        propertyName = "_BaseColor",
                        original = mat.GetColor("_BaseColor")
                    });
                }
                else if (mat.HasProperty("_Color"))
                {
                    _colorCache.Add(new ColorCacheEntry
                    {
                        material = mat,
                        propertyName = "_Color",
                        original = mat.GetColor("_Color")
                    });
                }
            }
        }
    }

    private void RestoreOriginalColors()
    {
        if (_colorCache == null || _colorCache.Count == 0) return;

        for (int i = 0; i < _colorCache.Count; i++)
        {
            var e = _colorCache[i];
            if (e.material == null || string.IsNullOrEmpty(e.propertyName)) continue;
            e.material.SetColor(e.propertyName, e.original);
        }
    }
}

