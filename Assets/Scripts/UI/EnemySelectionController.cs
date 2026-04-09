using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Left-click raycast to select an <see cref="Enemy"/> for the bottom info panel.
/// Runs after <see cref="TowerSelector"/> (default execution order) so tower/build-spot clicks are resolved first.
/// </summary>
[DefaultExecutionOrder(50)]
public class EnemySelectionController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float raycastDistance = 200f;

    int _worldRaycastMask = -1;
    Enemy _selectedEnemy;
    SelectionHighlight _selectedHighlight;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        int previewLayer = LayerMask.NameToLayer("Preview");
        if (previewLayer >= 0)
            _worldRaycastMask = Physics.DefaultRaycastLayers & ~(1 << previewLayer);
        else
            _worldRaycastMask = Physics.DefaultRaycastLayers;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, _worldRaycastMask, QueryTriggerInteraction.Ignore))
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.IsAliveForInfoPanel())
            {
                SelectionInfoPanel.EnsureBuilt(FindObjectOfType<Canvas>());
                SelectionInfoPanel.Instance?.ShowEnemy(enemy);
                SetSelectedEnemy(enemy);
                return;
            }
        }

        // 塔由 HexGridManager / TowerSelector 处理；若射线落在塔或塔位上，不得关闭共用信息栏（否则会抵消刚执行的 ShowTower）
        if (RayIndicatesTowerWorldTarget(ray))
            return;

        ClearSelection();
        SelectionInfoPanel.Instance?.Hide();
    }

    void SetSelectedEnemy(Enemy enemy)
    {
        if (_selectedEnemy == enemy)
            return;

        if (_selectedHighlight != null)
            _selectedHighlight.SetSelected(false);

        _selectedEnemy = enemy;
        _selectedHighlight = null;

        if (_selectedEnemy != null)
        {
            _selectedHighlight = _selectedEnemy.GetComponent<SelectionHighlight>();
            if (_selectedHighlight == null)
                _selectedHighlight = _selectedEnemy.gameObject.AddComponent<SelectionHighlight>();
            _selectedHighlight.SetSelected(true);
        }
    }

    void ClearSelection()
    {
        if (_selectedHighlight != null)
            _selectedHighlight.SetSelected(false);
        _selectedEnemy = null;
        _selectedHighlight = null;
    }

    /// <summary>与 <see cref="TowerSelector.TryHandleTowerClickFromRay"/> 的塔解析一致：点到塔/Hex 上的塔位时视为塔交互，不由本脚本 Hide。</summary>
    bool RayIndicatesTowerWorldTarget(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, raycastDistance, _worldRaycastMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            Tower tower = h.collider.GetComponent<Tower>()
                ?? h.collider.GetComponentInParent<Tower>()
                ?? h.collider.GetComponentInChildren<Tower>();
            BuildSpot spot = tower != null ? tower.OwningSpot : null;

            if (tower == null)
            {
                HexCell hex = h.collider.GetComponent<HexCell>()
                    ?? h.collider.GetComponentInParent<HexCell>()
                    ?? h.collider.GetComponentInChildren<HexCell>();
                if (hex != null)
                {
                    BuildSpot hexSpot = hex.GetBuildSpot();
                    if (hexSpot != null)
                    {
                        tower = hexSpot.GetCurrentTower();
                        spot = tower != null ? tower.OwningSpot : null;
                    }
                }
            }

            if (tower == null || spot == null)
                continue;
            if (spot.GetCurrentTower() != tower)
                continue;

            return true;
        }

        return false;
    }
}
