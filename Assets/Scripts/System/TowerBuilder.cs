using UnityEngine;

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

    [Header("Preview Materials")]
    public Material validPreviewMaterial;
    public Material invalidPreviewMaterial;

    private GameObject currentPreview;
    private BuildSpot currentSpot;

    private void Start()
    {
        if (towerPreviewPrefab != null)
        {
            currentPreview = Instantiate(towerPreviewPrefab);
            currentPreview.SetActive(false);
        }
    }

    private void Update()
    {
        UpdatePreview();

        if (Input.GetMouseButtonDown(0))
        {
            TryBuildTower();
        }
    }

    private void UpdatePreview()
    {
        if (mainCamera == null || currentPreview == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, buildSpotLayer))
        {
            BuildSpot spot = hit.collider.GetComponent<BuildSpot>();

            if (spot != null)
            {
                currentSpot = spot;
                currentPreview.SetActive(true);

                Vector3 previewPosition = spot.transform.position;
                previewPosition.y = 0.5f;
                currentPreview.transform.position = previewPosition;

                bool canBuild = spot.CanBuild() && currencySystem != null && currencySystem.HasEnoughGold(towerCost);
                SetPreviewMaterial(canBuild ? validPreviewMaterial : invalidPreviewMaterial);
                return;
            }
        }

        currentSpot = null;
        currentPreview.SetActive(false);
    }

    private void TryBuildTower()
    {
        if (towerPrefab == null || currencySystem == null || currentSpot == null)
            return;

        if (!currentSpot.CanBuild())
            return;

        if (!currencySystem.SpendGold(towerCost))
            return;

        Vector3 buildPosition = currentSpot.transform.position;
        buildPosition.y = 0.5f;

        GameObject towerObj = Instantiate(towerPrefab, buildPosition, Quaternion.identity);
        Tower tower = towerObj.GetComponent<Tower>();

        currentSpot.SetCurrentTower(tower);
        currentPreview.SetActive(false);
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
}