using UnityEngine;
using UnityEngine.EventSystems;

public class TowerSelector : MonoBehaviour
{
    private const float RaycastDistance = 150f;

    public Camera mainCamera;
    public LayerMask buildSpotLayer;
    public TowerMenu towerMenu;
    [SerializeField] private TowerBuilder towerBuilder;

    private Tower _selectedTower;
    private SelectionHighlight _selectedHighlight;

    private void Awake()
    {
        if (towerBuilder == null)
            towerBuilder = FindObjectOfType<TowerBuilder>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
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
                SetSelectedTower(towerOnSpot);
                towerMenu.ShowMenu(towerOnSpot, spot);
                return;
            }

            if (towerBuilder == null)
                towerBuilder = FindObjectOfType<TowerBuilder>();

            if (towerBuilder != null && towerBuilder.TryBuildOnSpot(spot))
            {
                ClearSelection();
                towerMenu.HideMenu();
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
        ClearSelection();
        towerMenu.HideMenu();
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
}
