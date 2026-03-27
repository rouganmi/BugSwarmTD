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
    [SerializeField] private EnemyInfoPanelUI panel;
    [SerializeField] private float raycastDistance = 200f;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (panel == null)
            panel = GetComponent<EnemyInfoPanelUI>();
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

        if (panel == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.IsAliveForInfoPanel())
            {
                panel.Show(enemy);
                return;
            }
        }

        panel.Hide();
    }
}
