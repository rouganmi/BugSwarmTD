using UnityEngine;

public class TowerSelector : MonoBehaviour
{
    public Camera mainCamera;
    public LayerMask buildSpotLayer;
    public TowerMenu towerMenu;

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            SelectTower();
        }
    }

    private void SelectTower()
    {
        if (mainCamera == null || towerMenu == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, buildSpotLayer))
        {
            BuildSpot spot = hit.collider.GetComponent<BuildSpot>();

            if (spot != null && spot.GetCurrentTower() != null)
            {
                towerMenu.ShowMenu(spot.GetCurrentTower(), spot);
                return;
            }
        }

        towerMenu.HideMenu();
    }
}
