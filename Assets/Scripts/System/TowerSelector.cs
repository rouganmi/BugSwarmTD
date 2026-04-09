using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TowerSelector : MonoBehaviour
{
    private const float RaycastDistance = 150f;

    public Camera mainCamera;
    public LayerMask buildSpotLayer;
    public TowerMenu towerMenu;
    [SerializeField] private TowerBuilder towerBuilder;
    [SerializeField] private BuildSelectionUI buildSelectionUI;

    private Tower _selectedTower;
    private SelectionHighlight _selectedHighlight;

    private void Awake()
    {
        if (towerBuilder == null)
            towerBuilder = FindObjectOfType<TowerBuilder>();
        if (buildSelectionUI == null)
            buildSelectionUI = GetComponent<BuildSelectionUI>();
        if (buildSelectionUI == null)
            buildSelectionUI = FindObjectOfType<BuildSelectionUI>();
    }

    /// <summary>Hex 地图：点击空白或非 Hex 时关闭塔菜单与选中高亮。</summary>
    public void ClearWorldSelectionAndTowerMenu()
    {
        ClearSelection();
        if (towerMenu != null)
            towerMenu.HideMenu();
    }

    /// <summary>Hex 地图：点击 Hex 格（建造流程）前关闭塔 UI，避免与建造窗叠加。</summary>
    public void OnBeforeHexCellWorldClick()
    {
        ClearWorldSelectionAndTowerMenu();
    }

    /// <summary>
    /// Hex 地图：由 <see cref="HexGridManager"/> 在判定 Hex 点击之前调用。
    /// 沿射线按距离遍历所有命中，优先解析 <see cref="Tower"/>（含子物体无 Collider 时经 <see cref="HexCell"/> 取塔）。
    /// </summary>
    public bool TryHandleTowerClickFromRay(Ray ray, float maxDistance, int layerMask)
    {
        if (towerMenu == null)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Tower tower = hit.collider.GetComponent<Tower>()
                ?? hit.collider.GetComponentInParent<Tower>()
                ?? hit.collider.GetComponentInChildren<Tower>();
            BuildSpot spot = tower != null ? tower.OwningSpot : null;

            if (tower == null)
            {
                HexCell hex = hit.collider.GetComponent<HexCell>()
                    ?? hit.collider.GetComponentInParent<HexCell>()
                    ?? hit.collider.GetComponentInChildren<HexCell>();
                if (hex != null)
                {
                    BuildSpot hexSpot = hex.GetBuildSpot();
                    if (hexSpot != null)
                    {
                        tower = hexSpot.GetCurrentTower();
                        spot = tower != null ? tower.OwningSpot : null;
                    }

                    if (tower == null)
                    {
                        Tower fallbackTower = hex.GetPlacedTower();
                        Debug.Log($"[GetPlacedTowerFallback][TowerSelector] hexExists=true hexSpotNull={(hexSpot == null)} hexSpotCurrentTowerNull={(hexSpot == null || hexSpot.GetCurrentTower() == null)} fallbackTowerNonNull={(fallbackTower != null)}");
                        tower = fallbackTower;
                        spot = tower != null ? tower.OwningSpot : null;
                    }
                }
            }

            if (tower == null || spot == null)
                continue;
            if (spot.GetCurrentTower() != tower)
                continue;

            Debug.Log($"[TowerClick] Hit tower = {tower.gameObject.name}");
            if (buildSelectionUI != null)
                buildSelectionUI.Close();
            SetSelectedTower(tower);
            towerMenu.ShowMenu(tower);
            Debug.Log($"[TowerClick] Open tower menu for {tower.gameObject.name}");
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (HexGridManager.Instance != null)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (buildSelectionUI != null && buildSelectionUI.IsOpen)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (mainCamera == null || towerMenu == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, RaycastDistance, buildSpotLayer))
        {
            BuildSpot spot = hit.collider.GetComponent<BuildSpot>();
            if (spot == null)
            {
                ClearSelectionAndMenu();
                return;
            }

            Tower towerOnSpot = spot.GetCurrentTower();

            if (towerOnSpot != null)
            {
                if (buildSelectionUI != null) buildSelectionUI.Close();
                SetSelectedTower(towerOnSpot);
                towerMenu.ShowMenu(towerOnSpot);
                return;
            }

            if (buildSelectionUI != null)
            {
                buildSelectionUI.OpenForSpot(spot);
                towerMenu.HideMenu();
                ClearSelection();
                return;
            }

            ClearSelection();
            towerMenu.HideMenu();
            return;
        }

        ClearSelectionAndMenu();
    }

    private void ClearSelectionAndMenu()
    {
        if (buildSelectionUI != null && buildSelectionUI.IsOpen)
        {
            Debug.Log("[HexInput] Ignore clear/close because BuildUI is open");
            return;
        }

        ClearSelection();
        towerMenu.HideMenu();
        if (buildSelectionUI != null) buildSelectionUI.Close();
    }

    private void SetSelectedTower(Tower tower)
    {
        if (_selectedTower == tower) return;

        if (_selectedHighlight != null) _selectedHighlight.SetSelected(false);
        _selectedTower = tower;
        _selectedHighlight = null;

        if (_selectedTower != null)
        {
            _selectedHighlight = _selectedTower.GetComponent<SelectionHighlight>();
            if (_selectedHighlight == null) _selectedHighlight = _selectedTower.gameObject.AddComponent<SelectionHighlight>();
            _selectedHighlight.SetSelected(true);
        }
    }

    private void ClearSelection()
    {
        if (_selectedHighlight != null) _selectedHighlight.SetSelected(false);
        _selectedTower = null;
        _selectedHighlight = null;
    }

    /// <summary>供 <see cref="TowerMenu.HideRadialAndDeselectForEnemy"/> 等仅清除高亮，不调用 <see cref="TowerMenu.HideMenu"/>。</summary>
    public void ClearTowerSelectionPublic()
    {
        ClearSelection();
    }
}
