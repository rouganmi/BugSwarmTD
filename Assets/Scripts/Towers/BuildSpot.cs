using UnityEngine;
using System.Collections.Generic;

public class BuildSpot : MonoBehaviour
{
    // Truth-shell state. Keep occupancy and anchor semantics stable.
    [SerializeField] private bool isOccupied = false;
    [SerializeField] private Tower currentTower;
    // Visual compatibility only. Serialized shape stays intact for current scenes.

    [Header("Visual — pad tint")]
    [SerializeField] private bool enableHoverHighlight = true;
    [SerializeField] private Color hoverColor;
    [SerializeField, Range(0f, 1f)] private float hoverIntensity = 0.5f;
    [SerializeField] private Color idleColor = new Color(0.42f, 0.78f, 0.58f, 1f);
    [SerializeField, Range(0f, 1f)] private float idleIntensity = 0.22f;
    [SerializeField] private Color occupiedColor = new Color(0.22f, 0.22f, 0.24f, 1f);
    [SerializeField, Range(0f, 1f)] private float occupiedIntensity = 0.06f;

    [Header("Visual — ring (decorative)")]
    [SerializeField] private bool showRingWhenEmpty = false;
    [SerializeField, Range(0.05f, 0.9f)] private float ringRadiusFactor = 0.52f;
    [SerializeField] private float ringYOffset = 0.12f;
    [SerializeField] private int ringSegments = 48;
    [SerializeField, Range(0.002f, 0.08f)] private float ringWidthIdle = 0.028f;
    [SerializeField, Range(0.002f, 0.12f)] private float ringWidthHover = 0.044f;
    [SerializeField] private Color ringColorIdle = new Color(0.45f, 0.88f, 0.55f, 0.42f);
    [SerializeField] private Color ringColorHover = new Color(0.35f, 1f, 0.52f, 0.9f);

    [Header("Visual — hover scale (empty only)")]
    [SerializeField] private bool enableHoverScale = true;
    [SerializeField, Range(1f, 1.15f)] private float hoverScaleMultiplier = 1.045f;

    [Header("Visual — occupied")]
    [SerializeField] private bool hidePadWhenOccupied = true;
    [SerializeField] private bool hideRingWhenOccupied = true;

    private Renderer[] _renderers;
    private LineRenderer _ring;
    private Material _ringMaterial;
    private Vector3 _idleLocalScale;
    private bool _isHovered;

    private struct ColorCacheEntry
    {
        public Material material;
        public string propertyName;
        public Color original;
    }

    private readonly List<ColorCacheEntry> _colorCache = new List<ColorCacheEntry>(16);

    static bool _loggedHexVisualRingHidden;

    // Truth-shell API: occupancy state plus this transform as the build anchor.
    public bool CanBuild()
    {
        return !isOccupied && currentTower == null;
    }

    private void Awake()
    {
        _idleLocalScale = transform.localScale;

        if (hoverColor.r == 0f && hoverColor.g == 0f && hoverColor.b == 0f && hoverColor.a == 0f)
            hoverColor = new Color(0.32f, 0.98f, 0.48f, 1f);

        EnsureRing();
        _renderers = GetComponentsInChildren<Renderer>(true);
        CacheOriginalColors();
        SyncVisualStateFromTruth();
    }

    private bool HasTintableMaterials()
    {
        return _colorCache != null && _colorCache.Count > 0;
    }

    private void EnsureRing()
    {
        if (!showRingWhenEmpty)
        {
            if (!_loggedHexVisualRingHidden && Application.isPlaying)
            {
                _loggedHexVisualRingHidden = true;
                Debug.Log("[HexVisual] Green marker hidden on runtime (BuildSpot decorative ring disabled by default).");
                Debug.Log("[AOEControl] Hidden green markers remain disabled");
            }
            return;
        }

        if (_ring != null)
            return;

        var go = new GameObject("BuildSpotRing");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localPosition = new Vector3(0f, ringYOffset, 0f);

        _ring = go.AddComponent<LineRenderer>();
        _ring.loop = true;
        _ring.useWorldSpace = false;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;
        _ring.numCornerVertices = 2;
        _ring.numCapVertices = 2;

        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        _ringMaterial = new Material(sh);
        _ringMaterial.renderQueue = 3200;
        if (_ringMaterial.HasProperty("_BaseColor"))
            _ringMaterial.SetColor("_BaseColor", ringColorIdle);
        else
            _ringMaterial.color = ringColorIdle;

        _ring.material = _ringMaterial;
        RebuildRingPoints();
    }

    private void RebuildRingPoints()
    {
        if (_ring == null || ringSegments < 3) return;

        float maxXZ = Mathf.Max(Mathf.Abs(_idleLocalScale.x), Mathf.Abs(_idleLocalScale.z));
        float r = maxXZ * 0.5f * Mathf.Clamp01(ringRadiusFactor);
        if (r < 0.05f) r = 0.5f;

        int n = ringSegments;
        _ring.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float t = (i / (float)n) * Mathf.PI * 2f;
            _ring.SetPosition(i, new Vector3(Mathf.Cos(t) * r, 0f, Mathf.Sin(t) * r));
        }

        ApplyRingAppearance(false);
    }

    private void ApplyRingAppearance(bool hovered)
    {
        if (_ring == null) return;

        _ring.startWidth = hovered ? ringWidthHover : ringWidthIdle;
        _ring.endWidth = _ring.startWidth;

        Color c = hovered ? ringColorHover : ringColorIdle;
        if (_ringMaterial != null)
        {
            if (_ringMaterial.HasProperty("_BaseColor")) _ringMaterial.SetColor("_BaseColor", c);
            else _ringMaterial.color = c;
        }
    }

    private void OnMouseEnter()
    {
        _isHovered = true;
        ApplyHover(true);
    }

    private void OnMouseExit()
    {
        _isHovered = false;
        ApplyHover(false);
    }

    // Truth-shell API: commit occupancy after a successful build.
    public void SetCurrentTower(Tower tower)
    {
        currentTower = tower;
        isOccupied = tower != null;

        _isHovered = false;
        RestorePadScale();
        SyncVisualStateFromTruth();
    }

    // Truth-shell API: expose the currently committed tower.
    public Tower GetCurrentTower()
    {
        return currentTower;
    }

    // Truth-shell API: release occupancy after sell/remove.
    public void ClearTower()
    {
        currentTower = null;
        isOccupied = false;
        if (_ring == null) EnsureRing();
        else RebuildRingPoints();
        SyncVisualStateFromTruth();
    }

    // Visual compatibility API retained for current runtime wiring.
    public void SetHoverEnabled(bool value)
    {
        enableHoverHighlight = value;
        SyncVisualStateFromTruth();
    }

    // Visual compatibility entry point. This must not mutate occupancy truth.
    public void ApplyHover(bool hovered)
    {
        _isHovered = hovered;
        SyncVisualStateFromTruth();
    }

    void SyncVisualStateFromTruth()
    {
        ApplyVisualState();
    }

    // Visual compatibility layer only. Avoid adding build-rule truth here.
    private void ApplyVisualState()
    {
        if (!CanBuild())
        {
            if (hidePadWhenOccupied)
            {
                SetPadRenderersEnabled(false);
            }
            else if (HasTintableMaterials())
            {
                SetPadRenderersEnabled(true);
                ApplyTintColor(occupiedColor, occupiedIntensity);
            }
            else
            {
                SetPadRenderersEnabled(true);
            }

            if (hideRingWhenOccupied && _ring != null) _ring.enabled = false;
            RestorePadScale();
            return;
        }

        SetPadRenderersEnabled(true);

        if (_ring != null)
        {
            _ring.enabled = showRingWhenEmpty;
            ApplyRingAppearance(enableHoverHighlight && _isHovered);
        }

        if (enableHoverScale && enableHoverHighlight && _isHovered)
            transform.localScale = _idleLocalScale * hoverScaleMultiplier;
        else
            RestorePadScale();

        if (!HasTintableMaterials()) return;

        if (enableHoverHighlight && _isHovered)
        {
            ApplyTintColor(hoverColor, hoverIntensity);
            return;
        }

        if (idleIntensity > 0f)
        {
            ApplyTintColor(idleColor, idleIntensity);
            return;
        }

        RestoreOriginalColors();
    }

    private void SetPadRenderersEnabled(bool on)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null || r is LineRenderer) continue;
            r.enabled = on;
        }
    }

    private void RestorePadScale()
    {
        transform.localScale = _idleLocalScale;
    }

    private void CacheOriginalColors()
    {
        _colorCache.Clear();
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null || r is LineRenderer) continue;

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

    private void ApplyTintColor(Color tintColor, float amount01)
    {
        float a = Mathf.Clamp01(amount01);
        for (int i = 0; i < _colorCache.Count; i++)
        {
            var e = _colorCache[i];
            if (e.material == null || string.IsNullOrEmpty(e.propertyName)) continue;
            Color target = Color.LerpUnclamped(e.original, tintColor, a);
            e.material.SetColor(e.propertyName, target);
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && _ring != null)
            RebuildRingPoints();
    }
#endif
}
