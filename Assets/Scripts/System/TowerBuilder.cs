using UnityEngine;
using UnityEngine.EventSystems;

public class TowerBuilder : MonoBehaviour
{
    [Header("References")]
    public GameObject towerPrefab;
    public GameObject towerPreviewPrefab;
    public Camera mainCamera;
    public CurrencySystem currencySystem;

    [Header("Build Settings")]
    public LayerMask buildSpotLayer;
    public int towerCost = 30;
    [SerializeField] private float previewYOffset = 0.5f;

    /// <summary>Vertical offset from build spot surface; shared with <see cref="BuildPreviewController"/>.</summary>
    public float PreviewYOffset => previewYOffset;

    [Header("Preview Materials")]
    public Material validPreviewMaterial;
    public Material invalidPreviewMaterial;

    [Header("Preview tint")]
    [SerializeField] private Color validTint = new Color(0.2f, 0.92f, 0.38f, 0.82f);
    [SerializeField] private Color invalidTint = new Color(0.95f, 0.22f, 0.22f, 0.82f);

    private GameObject currentPreview;
    private BuildSpot currentSpot;
    private BuildSpot _lastSpot;
    private bool _lastPreviewValid;
    private bool _hasPreviewValidState;

    public bool IsPointerOnBuildSpot => currentSpot != null;

    public bool CanShowBuildPreview =>
        towerPrefab != null && towerPreviewPrefab != null && mainCamera != null;

    private void Start()
    {
        if (towerPreviewPrefab != null)
        {
            currentPreview = Instantiate(towerPreviewPrefab);
            currentPreview.SetActive(false);
            ApplyPreviewLayerAndNoPhysics(currentPreview);
        }
        else
        {
            Debug.LogWarning("[TowerBuilder] towerPreviewPrefab is null. Build preview is disabled.");
        }
    }

    static void ApplyPreviewLayerAndNoPhysics(GameObject root)
    {
        if (root == null) return;
        int previewLayer = LayerMask.NameToLayer("Preview");
        if (previewLayer < 0) previewLayer = 7;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null)
                t.gameObject.layer = previewLayer;
        }
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        {
            if (c != null) c.enabled = false;
        }
    }

    private void Update()
    {
        UpdatePreview();
    }

    /// <summary>Called from TowerSelector / <see cref="BuildSelectionUI"/> with the spot under the cursor for this frame (same raycast as selection).</summary>
    public bool TryBuildOnSpot(BuildSpot spot)
    {
        return TryBuildOnSpot(spot, towerPrefab, towerCost);
    }

    /// <summary>Build using an explicit prefab and cost (e.g. tower pick list). Falls back to default prefab/cost when null / negative.</summary>
    public bool TryBuildOnSpot(BuildSpot spot, GameObject prefabOverride, int costOverride)
    {
        GameObject prefab = prefabOverride != null ? prefabOverride : towerPrefab;
        int cost = costOverride >= 0 ? costOverride : towerCost;

        if (prefab == null)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: prefab null");
            return false;
        }

        if (spot == null)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: spot null");
            return false;
        }

        if (currencySystem == null)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: currency system null");
            return false;
        }

        if (!spot.CanBuild())
        {
            Debug.LogWarning("[TowerBuilder] Build failed: spot cannot build");
            return false;
        }

        if (!currencySystem.SpendGold(cost))
        {
            Debug.LogWarning("[TowerBuilder] Build failed: not enough gold or spend failed");
            return false;
        }

        Vector3 buildPosition = spot.transform.position + spot.transform.up * previewYOffset;
        Quaternion buildRotation = QuaternionUtil.NormalizeOrIdentity(spot.transform.rotation);

        GameObject towerObj = Instantiate(prefab, buildPosition, buildRotation);
        if (towerObj == null)
        {
            Debug.LogError("[TowerBuilder] Build failed: instantiate returned null");
            return false;
        }

        Tower tower = towerObj.GetComponent<Tower>();
        spot.SetCurrentTower(tower);

        if (tower != null)
        {
            var hexCell = spot.GetComponentInParent<HexCell>();
            if (hexCell != null)
                hexCell.NotifyTowerPlaced(tower);
        }

#if UNITY_EDITOR
        Debug.Log($"[TowerBuild] Placed tower prefab={prefab.name} spot={spot.name} pos={buildPosition}");
#endif
        Debug.Log($"[TowerBuilder] Build success at position={buildPosition}");

        if (currentPreview != null)
            currentPreview.SetActive(false);

        currentSpot = null;
        if (_lastSpot != null) _lastSpot.ApplyHover(false);
        _lastSpot = null;

        return true;
    }

    private void UpdatePreview()
    {
        if (mainCamera == null || currentPreview == null || !CanShowBuildPreview)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HidePreviewAndHover();
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, buildSpotLayer))
        {
            BuildSpot spot = hit.collider.GetComponent<BuildSpot>();
            if (spot == null)
                spot = hit.collider.GetComponentInChildren<BuildSpot>();
            if (spot == null)
                spot = hit.collider.GetComponentInParent<BuildSpot>();

            if (spot != null)
            {
                if (_lastSpot != null && _lastSpot != spot)
                    _lastSpot.ApplyHover(false);

                currentSpot = spot;
                _lastSpot = spot;
                currentPreview.SetActive(true);

                Vector3 previewPosition = spot.transform.position + spot.transform.up * previewYOffset;
                Quaternion previewRotation = QuaternionUtil.NormalizeOrIdentity(spot.transform.rotation);
                currentPreview.transform.SetPositionAndRotation(previewPosition, previewRotation);

                bool hasFunds = currencySystem != null && currencySystem.HasEnoughGold(towerCost);
                bool spotBuildable = spot.CanBuild();
                bool canBuild = spotBuildable && hasFunds;

                spot.ApplyHover(spotBuildable);
                SetPreviewVisual(canBuild);
                return;
            }
        }

        HidePreviewAndHover();
    }

    private void HidePreviewAndHover()
    {
        currentSpot = null;
        if (_lastSpot != null) _lastSpot.ApplyHover(false);
        _lastSpot = null;
        currentPreview.SetActive(false);
        _hasPreviewValidState = false;
    }

    private void SetPreviewVisual(bool canBuild)
    {
        if (!_hasPreviewValidState || _lastPreviewValid != canBuild)
        {
            _hasPreviewValidState = true;
            _lastPreviewValid = canBuild;
            SetPreviewMaterial(canBuild ? validPreviewMaterial : invalidPreviewMaterial);
        }

        ApplyPreviewTint(canBuild ? validTint : invalidTint);
    }

    private void SetPreviewMaterial(Material previewMat)
    {
        if (currentPreview == null || previewMat == null)
            return;

        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            renderer.material = previewMat;
        }
    }

    private void ApplyPreviewTint(Color tint)
    {
        if (currentPreview == null) return;

        var renderers = currentPreview.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

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
                var mat = mats[m];
                if (mat == null) continue;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            }
        }
    }
}
