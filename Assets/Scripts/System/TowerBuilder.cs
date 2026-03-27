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
        }
        else
        {
            Debug.LogWarning("[TowerBuilder] towerPreviewPrefab is null. Build preview is disabled.");
        }
    }

    private void Update()
    {
        UpdatePreview();
    }

    /// <summary>Called from TowerSelector with the spot under the cursor for this frame (same raycast as selection).</summary>
    public bool TryBuildOnSpot(BuildSpot spot)
    {
        if (towerPrefab == null || spot == null || currencySystem == null)
            return false;

        if (!spot.CanBuild())
            return false;

        if (!currencySystem.SpendGold(towerCost))
            return false;

        Vector3 buildPosition = spot.transform.position + spot.transform.up * previewYOffset;
        Quaternion buildRotation = spot.transform.rotation;

        GameObject towerObj = Instantiate(towerPrefab, buildPosition, buildRotation);
        if (towerObj == null)
        {
            Debug.LogError("[TowerBuilder] Instantiate returned null for towerPrefab.");
            return false;
        }

        Tower tower = towerObj.GetComponent<Tower>();
        spot.SetCurrentTower(tower);

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

            if (spot != null)
            {
                if (_lastSpot != null && _lastSpot != spot)
                    _lastSpot.ApplyHover(false);

                currentSpot = spot;
                _lastSpot = spot;
                currentPreview.SetActive(true);

                Vector3 previewPosition = spot.transform.position + spot.transform.up * previewYOffset;
                Quaternion previewRotation = spot.transform.rotation;
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
