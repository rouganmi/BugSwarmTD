using UnityEngine;
using System.Collections.Generic;

public class SelectionHighlight : MonoBehaviour
{
    [SerializeField] private Color highlightColor;
    [SerializeField, Range(0f, 1f)] private float intensity = 0.35f;

    private Renderer[] _renderers;
    private bool _isOn;

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
        if (highlightColor.r == 0f && highlightColor.g == 0f && highlightColor.b == 0f && highlightColor.a == 0f)
            highlightColor = new Color(0.35f, 0.9f, 1f, 1f);

        CacheOriginalColors();
        SetSelected(false);
    }

    public void SetSelected(bool value)
    {
        _isOn = value;
        ApplyTint(_isOn ? intensity : 0f);
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

