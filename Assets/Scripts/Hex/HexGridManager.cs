using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 实验地图：Hex 格悬停高亮（射线每帧更新），并统一处理 Hex 格的世界射线点击。
/// </summary>
public class HexGridManager : MonoBehaviour
{
    public static HexGridManager Instance { get; private set; }

    [Header("Hex click (world ray)")]
    [Tooltip("优先用于 ScreenPointToRay；不绑则使用 Camera.main")]
    [SerializeField] Camera clickCamera;
    [SerializeField] float raycastDistance = 500f;
    [Tooltip("塔点击优先于 Hex；不绑则 FindObjectOfType")]
    [SerializeField] TowerSelector towerSelector;

    /// <summary>当前鼠标射线命中的可建空位格；仅用于悬停高亮，不表示“锁定选中”。</summary>
    HexCell _hoveredCell;

    bool _loggedTowerUiHoverSuspend;

    int _hexClickMask = -1;

    void Awake()
    {
        Instance = this;
        int previewLayer = LayerMask.NameToLayer("Preview");
        if (previewLayer >= 0)
            _hexClickMask = Physics.DefaultRaycastLayers & ~(1 << previewLayer);
        else
            _hexClickMask = Physics.DefaultRaycastLayers;

        if (towerSelector == null)
            towerSelector = FindObjectOfType<TowerSelector>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        UpdateHexHover();

        if (!Input.GetMouseButtonDown(0))
            return;

        // 任意屏幕 UI（含塔环形菜单、底部栏）上的点击不穿透到 Hex / 世界射线
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Debug.Log("[HexInput] Mouse left click detected");

        Camera cam = clickCamera != null ? clickCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError("[HexInput] No camera: clickCamera and Camera.main are both null.");
            return;
        }

        Debug.Log($"[HexInput] Using camera = {cam.name}");

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        int mask = _hexClickMask >= 0 ? _hexClickMask : Physics.DefaultRaycastLayers;

        if (towerSelector != null && towerSelector.TryHandleTowerClickFromRay(ray, raycastDistance, mask))
            return;

        Debug.Log("[TowerClick] No tower hit, fallback to Hex input");

        if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (towerSelector != null)
                towerSelector.ClearWorldSelectionAndTowerMenu();
            Debug.Log("[HexInput] Raycast hit nothing");
            return;
        }

        int hl = hit.collider.gameObject.layer;
        string layerName = (hl >= 0 && hl < 32) ? LayerMask.LayerToName(hl) : "?";
        if (string.IsNullOrEmpty(layerName))
            layerName = hl.ToString();
        Debug.Log($"[HexInput] Raycast hit = {hit.collider.gameObject.name} layer={layerName}");

        HexCell cell = hit.collider.GetComponent<HexCell>()
            ?? hit.collider.GetComponentInParent<HexCell>()
            ?? hit.collider.GetComponentInChildren<HexCell>();

        if (cell == null)
        {
            if (towerSelector != null)
                towerSelector.ClearWorldSelectionAndTowerMenu();
            if (BuildSelectionUI.Instance != null && BuildSelectionUI.Instance.IsOpen)
                Debug.Log("[HexInput] Hit object is not a HexCell — ignored (BuildUI open, HexGridManager does not Close)");
            else
                Debug.Log("[HexInput] Hit object is not a HexCell");
            return;
        }

        BuildSelectionUI buildSelectionUi = BuildSelectionUI.ResolveExisting();

        Debug.Log($"[HexInput] Resolved HexCell {cell.GridX},{cell.GridY}");
        if (towerSelector != null)
            towerSelector.OnBeforeHexCellWorldClick();

        if (!cell.TryGetBuildSelectionSpot(out BuildSpot buildSpot))
            return;

        if (buildSelectionUi == null)
        {
            Debug.LogError("[HexBuild] Ignored: UI not resolved");
            return;
        }

        buildSelectionUi.OpenForSpot(buildSpot);
    }

    /// <summary>每帧根据鼠标射线更新可建格的悬停高亮；指针在 UI 上时不做世界悬停。</summary>
    void UpdateHexHover()
    {
        bool towerUiBlocks = TowerMenu.Instance != null && TowerMenu.Instance.IsOpen;
        bool selectionPanelBlocks = SelectionInfoPanel.Instance != null && SelectionInfoPanel.Instance.IsShowing;
        if (towerUiBlocks || selectionPanelBlocks)
        {
            if (!_loggedTowerUiHoverSuspend)
            {
                _loggedTowerUiHoverSuspend = true;
                Debug.Log("[HexHover] Suspended because TowerUI is open");
            }
            SetHoveredCell(null);
            return;
        }

        _loggedTowerUiHoverSuspend = false;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetHoveredCell(null);
            return;
        }

        Camera cam = clickCamera != null ? clickCamera : Camera.main;
        if (cam == null)
        {
            SetHoveredCell(null);
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        int mask = _hexClickMask >= 0 ? _hexClickMask : Physics.DefaultRaycastLayers;
        HexCell hitCell = null;
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, mask, QueryTriggerInteraction.Ignore))
        {
            hitCell = hit.collider.GetComponent<HexCell>()
                ?? hit.collider.GetComponentInParent<HexCell>()
                ?? hit.collider.GetComponentInChildren<HexCell>();
        }

        if (hitCell != null && !hitCell.CanPlaceTower())
            hitCell = null;

        SetHoveredCell(hitCell);
    }

    void SetHoveredCell(HexCell newHovered)
    {
        if (_hoveredCell == newHovered)
            return;

        if (_hoveredCell != null)
            _hoveredCell.ClearHighlight();

        _hoveredCell = newHovered;

        if (_hoveredCell != null)
            _hoveredCell.ApplyHighlight();
    }

    /// <summary>建塔成功后由 HexCell 调用：同步悬停引用（视觉已在 NotifyTowerPlaced 里 ClearHighlight）。</summary>
    public void OnHexBuilt(HexCell cell)
    {
        if (cell != null && _hoveredCell == cell)
            _hoveredCell = null;
    }
}
