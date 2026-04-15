using UnityEngine;
using UnityEngine.EventSystems;

public enum MapBlockingTag
{
    None,
    ExpansionBoundaryBlocked,
    NestBufferBlocked,
    SpecialZoneBlocked
}

public class TowerBuilder : MonoBehaviour
{
    public readonly struct BuildSubmissionEvaluation
    {
        public bool CanSubmit { get; }
        public bool HasSpot { get; }
        public bool IsTerrainBuildable { get; }
        public bool IsSpotBuildable { get; }
        public bool IsPrefabValid { get; }
        public bool IsCostValid { get; }
        public bool HasCurrencySystem { get; }
        public bool HasEnoughResources { get; }
        public bool IsWithinExpansionBoundary { get; }
        public bool IsInsideNestBuffer { get; }
        public bool IsOnResourceSiteOrPort { get; }
        public bool IsInsideSpecialBuildBlockZone { get; }
        public bool HasMapRuleBlock { get; }
        public MapBlockingTag MapBlockingTag { get; }

        public BuildSubmissionEvaluation(
            bool canSubmit,
            bool hasSpot,
            bool isTerrainBuildable,
            bool isSpotBuildable,
            bool isPrefabValid,
            bool isCostValid,
            bool hasCurrencySystem,
            bool hasEnoughResources,
            bool isWithinExpansionBoundary,
            bool isInsideNestBuffer,
            bool isOnResourceSiteOrPort,
            bool isInsideSpecialBuildBlockZone,
            bool hasMapRuleBlock,
            MapBlockingTag mapBlockingTag)
        {
            CanSubmit = canSubmit;
            HasSpot = hasSpot;
            IsTerrainBuildable = isTerrainBuildable;
            IsSpotBuildable = isSpotBuildable;
            IsPrefabValid = isPrefabValid;
            IsCostValid = isCostValid;
            HasCurrencySystem = hasCurrencySystem;
            HasEnoughResources = hasEnoughResources;
            IsWithinExpansionBoundary = isWithinExpansionBoundary;
            IsInsideNestBuffer = isInsideNestBuffer;
            IsOnResourceSiteOrPort = isOnResourceSiteOrPort;
            IsInsideSpecialBuildBlockZone = isInsideSpecialBuildBlockZone;
            HasMapRuleBlock = hasMapRuleBlock;
            MapBlockingTag = mapBlockingTag;
        }
    }

    private readonly struct MapBuildFacts
    {
        public bool IsWithinExpansionBoundary { get; }
        public bool IsInsideNestBuffer { get; }
        public bool IsOnResourceSiteOrPort { get; }
        public bool IsInsideSpecialBuildBlockZone { get; }
        public bool HasMapRuleBlock { get; }
        public MapBlockingTag MapBlockingTag { get; }

        public MapBuildFacts(
            bool isWithinExpansionBoundary,
            bool isInsideNestBuffer,
            bool isOnResourceSiteOrPort,
            bool isInsideSpecialBuildBlockZone,
            bool hasMapRuleBlock,
            MapBlockingTag mapBlockingTag)
        {
            IsWithinExpansionBoundary = isWithinExpansionBoundary;
            IsInsideNestBuffer = isInsideNestBuffer;
            IsOnResourceSiteOrPort = isOnResourceSiteOrPort;
            IsInsideSpecialBuildBlockZone = isInsideSpecialBuildBlockZone;
            HasMapRuleBlock = hasMapRuleBlock;
            MapBlockingTag = mapBlockingTag;
        }
    }

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

        var evaluation = EvaluateBuildRequest(spot, prefab, cost);
        if (!evaluation.CanSubmit)
        {
            LogInvalidBuildRequest(evaluation);
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
            tower.SetOwningSpot(spot);
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

    public BuildSubmissionEvaluation EvaluateBuildRequest(BuildSpot spot, GameObject prefab, int cost)
    {
        bool hasSpot = spot != null;
        var hexCell = hasSpot ? spot.GetComponentInParent<HexCell>() : null;
        bool isTerrainBuildable = hasSpot && (hexCell == null || hexCell.IsBuildable());
        bool isSpotBuildable = hasSpot && spot.CanBuild();
        bool isPrefabValid = prefab != null;
        bool isCostValid = cost >= 0;
        bool hasCurrency = currencySystem != null;
        bool hasEnoughResources = hasCurrency && isCostValid && currencySystem.HasEnoughGold(cost);
        MapBuildFacts mapFacts = ReadMapBuildFacts(hexCell);
        bool canSubmit =
            hasSpot &&
            isTerrainBuildable &&
            isSpotBuildable &&
            isPrefabValid &&
            isCostValid &&
            hasCurrency &&
            hasEnoughResources &&
            !mapFacts.HasMapRuleBlock;

        return new BuildSubmissionEvaluation(
            canSubmit,
            hasSpot,
            isTerrainBuildable,
            isSpotBuildable,
            isPrefabValid,
            isCostValid,
            hasCurrency,
            hasEnoughResources,
            mapFacts.IsWithinExpansionBoundary,
            mapFacts.IsInsideNestBuffer,
            mapFacts.IsOnResourceSiteOrPort,
            mapFacts.IsInsideSpecialBuildBlockZone,
            mapFacts.HasMapRuleBlock,
            mapFacts.MapBlockingTag
        );
    }

    private static MapBuildFacts ReadMapBuildFacts(HexCell hexCell)
    {
        var expansionBoundaryProvider =
            hexCell != null ? hexCell.GetComponentInParent<HexGridExpansionBoundaryProvider>() : null;
        bool isWithinExpansionBoundary =
            expansionBoundaryProvider == null ||
            expansionBoundaryProvider.IsWithinTemporaryAllowedBuildBoundary(hexCell);
        bool isInsideNestBuffer =
            hexCell != null && hexCell.GetComponent<HexNestBufferMarker>() != null;
        bool isOnResourceSiteOrPort =
            hexCell != null && hexCell.GetComponent<HexResourceSiteOrPortMarker>() != null;
        bool isInsideSpecialBuildBlockZone =
            hexCell != null && hexCell.GetComponent<HexSpecialBuildBlockMarker>() != null;
        bool hasMapRuleBlock =
            !isWithinExpansionBoundary || isInsideSpecialBuildBlockZone || isInsideNestBuffer;
        MapBlockingTag mapBlockingTag =
            !isWithinExpansionBoundary ? MapBlockingTag.ExpansionBoundaryBlocked :
            isInsideSpecialBuildBlockZone ? MapBlockingTag.SpecialZoneBlocked :
            isInsideNestBuffer ? MapBlockingTag.NestBufferBlocked :
            MapBlockingTag.None;

        return new MapBuildFacts(
            isWithinExpansionBoundary: isWithinExpansionBoundary,
            isInsideNestBuffer: isInsideNestBuffer,
            isOnResourceSiteOrPort: isOnResourceSiteOrPort,
            isInsideSpecialBuildBlockZone: isInsideSpecialBuildBlockZone,
            hasMapRuleBlock: hasMapRuleBlock,
            mapBlockingTag: mapBlockingTag
        );
    }

    private void LogInvalidBuildRequest(BuildSubmissionEvaluation evaluation)
    {
        if (!evaluation.HasSpot)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: spot null");
            return;
        }

        if (!evaluation.IsTerrainBuildable)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: hex cell not buildable");
            return;
        }

        if (!evaluation.IsSpotBuildable)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: spot cannot build");
            return;
        }

        if (!evaluation.IsPrefabValid)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: prefab null");
            return;
        }

        if (!evaluation.IsCostValid)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: cost invalid");
            return;
        }

        if (!evaluation.HasCurrencySystem)
        {
            Debug.LogWarning("[TowerBuilder] Build failed: currency system null");
            return;
        }

        if (!evaluation.HasEnoughResources)
            Debug.LogWarning("[TowerBuilder] Build failed: not enough gold");
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
