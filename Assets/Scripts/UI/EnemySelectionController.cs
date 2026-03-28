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
                return;
            }
        }

        // 塔由 HexGridManager / TowerSelector 处理；若射线落在塔或塔位上，不得关闭共用信息栏（否则会抵消刚执行的 ShowTower）
        if (RayIndicatesTowerWorldTarget(ray))
            return;

        SelectionInfoPanel.Instance?.Hide();
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
            BuildSpot spot = null;

            if (tower != null)
            {
                spot = tower.GetComponentInParent<BuildSpot>();
            }
            else
            {
                HexCell hex = h.collider.GetComponent<HexCell>()
                    ?? h.collider.GetComponentInParent<HexCell>()
                    ?? h.collider.GetComponentInChildren<HexCell>();
                if (hex != null)
                {
                    spot = hex.GetTowerSocket();
                    if (spot != null)
                        tower = spot.GetCurrentTower();
                }
                else
                {
                    spot = h.collider.GetComponent<BuildSpot>() ?? h.collider.GetComponentInParent<BuildSpot>();
                    if (spot != null)
                        tower = spot.GetCurrentTower();
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
