using UnityEngine;

/// <summary>
/// Ghost preview on the pending <see cref="BuildSpot"/> when hovering tower rows in <see cref="BuildSelectionUI"/>.
/// Does not participate in combat; colliders disabled and gameplay scripts turned off.
/// </summary>
[DisallowMultipleComponent]
public class BuildPreviewController : MonoBehaviour
{
    [SerializeField] private TowerBuilder towerBuilder;
    [SerializeField] private CurrencySystem currencySystem;
    [SerializeField] private float previewYOffset = 0.5f;

    [Header("Fallback if TowerBuilder materials missing")]
    [SerializeField] private Material validPreviewMaterial;
    [SerializeField] private Material invalidPreviewMaterial;
    [SerializeField] private Color validTint = new Color(0.2f, 0.92f, 0.38f, 0.82f);
    [SerializeField] private Color invalidTint = new Color(0.95f, 0.22f, 0.22f, 0.82f);

    private BuildSpot _spot;
    private GameObject _instance;
    private BuildTowerOption _hoveredOption;

    private void Awake()
    {
        if (towerBuilder == null) towerBuilder = FindObjectOfType<TowerBuilder>();
        if (currencySystem == null) currencySystem = FindObjectOfType<CurrencySystem>();
        SyncMaterialsFromTowerBuilder();
    }

    private void OnEnable()
    {
        GameEvents.OnGoldChanged += OnGoldChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnGoldChanged -= OnGoldChanged;
        Hide();
    }

    private void OnGoldChanged(int _)
    {
        if (_instance == null || _hoveredOption == null || _spot == null) return;
        bool affordable = currencySystem == null || currencySystem.HasEnoughGold(_hoveredOption.cost);
        ApplyVisual(affordable);
    }

    private void SyncMaterialsFromTowerBuilder()
    {
        if (towerBuilder == null) return;
        if (validPreviewMaterial == null) validPreviewMaterial = towerBuilder.validPreviewMaterial;
        if (invalidPreviewMaterial == null) invalidPreviewMaterial = towerBuilder.invalidPreviewMaterial;
    }

    /// <summary>Current spot for the open build menu; cleared on <see cref="Hide"/>.</summary>
    public void SetSpot(BuildSpot spot)
    {
        _spot = spot;
        Hide();
    }

    public void ShowHover(BuildTowerOption option)
    {
        Hide();
        if (_spot == null || option == null || option.towerPrefab == null)
            return;
        if (!_spot.CanBuild())
            return;

        _hoveredOption = option;

        float yOff = towerBuilder != null ? towerBuilder.PreviewYOffset : previewYOffset;
        Vector3 pos = _spot.transform.position + _spot.transform.up * yOff;
        Quaternion rot = _spot.transform.rotation;

        _instance = Instantiate(option.towerPrefab, pos, rot);
        StripGameplay(_instance);

        bool affordable = currencySystem == null || currencySystem.HasEnoughGold(option.cost);
        ApplyVisual(affordable);
    }

    public void Hide()
    {
        _hoveredOption = null;
        if (_instance != null)
        {
            Destroy(_instance);
            _instance = null;
        }
    }

    private void ApplyVisual(bool affordable)
    {
        if (_instance == null) return;

        Material mat = affordable ? validPreviewMaterial : invalidPreviewMaterial;
        Color tint = affordable ? validTint : invalidTint;

        var renderers = _instance.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        if (mat != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].material = mat;
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
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
                var material = mats[m];
                if (material == null) continue;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            }
        }
    }

    private static void StripGameplay(GameObject root)
    {
        int previewLayer = LayerMask.NameToLayer("Preview");
        if (previewLayer < 0) previewLayer = 7;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null) t.gameObject.layer = previewLayer;
        }

        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        {
            if (c != null) c.enabled = false;
        }

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null) continue;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null) mb.enabled = false;
        }
    }
}
