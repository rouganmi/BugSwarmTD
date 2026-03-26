using UnityEngine;
using TMPro;

public class TowerMenu : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public TMP_Text infoText;
    public CurrencySystem currencySystem;
    public Camera mainCamera;
    public Vector3 screenOffset = new Vector3(0f, 80f, 0f);

    private Tower selectedTower;
    private BuildSpot selectedSpot;
    private RectTransform panelRectTransform;

    private void Start()
    {
        if (panel != null)
        {
            panelRectTransform = panel.GetComponent<RectTransform>();
        }

        HideMenu();
    }

    private void Update()
    {
        UpdatePanelPosition();
    }

    public void ShowMenu(Tower tower, BuildSpot spot)
    {
        selectedTower = tower;
        selectedSpot = spot;

        if (panel != null)
            panel.SetActive(true);

        RefreshInfo();
        UpdatePanelPosition();
    }

    public void HideMenu()
    {
        selectedTower = null;
        selectedSpot = null;

        if (panel != null)
            panel.SetActive(false);
    }

    public void UpgradeSelectedTower()
    {
        if (selectedTower == null || currencySystem == null)
            return;

        int cost = selectedTower.GetUpgradeCost();

        if (!currencySystem.SpendGold(cost))
            return;

        selectedTower.UpgradeTower();
        RefreshInfo();
    }

    public void SellSelectedTower()
    {
        if (selectedTower == null || currencySystem == null || selectedSpot == null)
            return;

        currencySystem.AddGold(selectedTower.GetSellValue());

        Destroy(selectedTower.gameObject);
        selectedSpot.ClearTower();

        HideMenu();
    }

    private void RefreshInfo()
    {
        if (selectedTower != null && infoText != null)
        {
            infoText.text =
                "Tower Lv." + selectedTower.level +
                "\nUpgrade: " + selectedTower.GetUpgradeCost() +
                "\nSell: " + selectedTower.GetSellValue();
        }
    }

    private void UpdatePanelPosition()
    {
        if (selectedTower == null || panelRectTransform == null || mainCamera == null || panel == null || !panel.activeSelf)
            return;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(selectedTower.transform.position);

        if (screenPos.z < 0)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(true);
        panelRectTransform.position = screenPos + screenOffset;
    }
}