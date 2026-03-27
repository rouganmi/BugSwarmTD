using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TowerMenu : MonoBehaviour
{
    [Header("References")]
    public GameObject panel;
    public TMP_Text infoText;
    // Keep field name `currencySystem` to preserve existing Inspector wiring.
    // Scene currently uses CurrencySystem on the CurrencyManager GameObject.
    public CurrencySystem currencySystem;
    public Camera mainCamera;
    public Vector3 screenOffset = new Vector3(0f, 80f, 0f);

    [Header("Optional UI")]
    [SerializeField] private Button upgradeButton;

    private Tower selectedTower;
    private BuildSpot selectedSpot;
    private RectTransform panelRectTransform;

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Start()
    {
        if (panel != null)
        {
            panelRectTransform = panel.GetComponent<RectTransform>();
            if (upgradeButton == null)
            {
                var t = panel.transform.Find("UpgradeButton");
                if (t != null) upgradeButton = t.GetComponent<Button>();
            }
        }

        HideMenu();
    }

    private void Update()
    {
        if (IsOpen)
        {
            if (!ValidateSelectionOrClose())
                return;

            RefreshInfo();
        }

        UpdatePanelPosition();
    }

    /// <summary>Returns false if the menu should close (tower missing / sold / spot changed).</summary>
    private bool ValidateSelectionOrClose()
    {
        if (selectedTower == null)
        {
            HideMenu();
            return false;
        }

        if (!selectedTower.gameObject || !selectedTower.gameObject.activeInHierarchy)
        {
            HideMenu();
            return false;
        }

        if (selectedSpot != null && selectedSpot.GetCurrentTower() != selectedTower)
        {
            HideMenu();
            return false;
        }

        return true;
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
        int gold = currencySystem.GetCurrentGold();
        Debug.Log($"[TowerMenu] Upgrade clicked. TowerLv={selectedTower.level}, Gold={gold}, UpgradeCost={cost}");

        if (!currencySystem.HasEnoughGold(cost))
        {
            Debug.Log($"[TowerMenu] Not enough gold. Gold={gold}, UpgradeCost={cost}");
            return;
        }

        bool spent = currencySystem.SpendGold(cost);
        Debug.Log($"[TowerMenu] SpendGold({cost}) result: {spent}");
        if (!spent)
            return;

        selectedTower.UpgradeTower();
        RefreshInfo();

        if (panel != null)
        {
            var presenter = panel.GetComponent<TowerMenuPanelPresenter>();
            presenter?.PlayUpgradeFeedback();
        }
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
        if (selectedTower == null || infoText == null)
            return;

        Tower tower = selectedTower;
        float currentDamage = tower.bulletDamage;
        float nextDamage = currentDamage + 1f;

        float currentRange = tower.attackRange;
        float nextRange = currentRange + 1f;

        int cost = tower.GetUpgradeCost();
        int sell = tower.GetSellValue();

        bool atMax = tower.IsAtMaxLevel();
        bool canAfford = currencySystem != null && currencySystem.HasEnoughGold(cost);
        bool canUpgrade = !atMax && canAfford;

        if (atMax)
        {
            infoText.text =
                $"Tower Lv.{tower.level}  [MAX]\n\n" +
                $"Damage: {currentDamage:0.#}\n" +
                $"Range: {currentRange:0.#}\n\n" +
                $"Upgrade Cost: MAX\n" +
                $"Sell Value: {sell}";
        }
        else
        {
            infoText.text =
                $"Tower Lv.{tower.level}\n\n" +
                $"Damage: {currentDamage:0.#} -> {nextDamage:0.#}\n" +
                $"Range: {currentRange:0.#} -> {nextRange:0.#}\n\n" +
                $"Upgrade Cost: {cost}\n" +
                $"Sell Value: {sell}";
        }

        if (upgradeButton != null)
            upgradeButton.interactable = canUpgrade;

        if (panel != null)
        {
            var presenter = panel.GetComponent<TowerMenuPanelPresenter>();
            presenter?.SetUpgradeState(atMax, !atMax && !canAfford);
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
        Vector3 offset = screenOffset;

        // Auto place panel away from the tower body.
        if (screenPos.x > Screen.width * 0.62f) offset.x = -Mathf.Abs(offset.x) - 180f;
        else if (screenPos.x < Screen.width * 0.38f) offset.x = Mathf.Abs(offset.x) + 180f;
        else offset.x = 220f;

        if (screenPos.y > Screen.height * 0.62f) offset.y = -Mathf.Abs(offset.y) - 140f;
        else if (screenPos.y < Screen.height * 0.38f) offset.y = Mathf.Abs(offset.y) + 140f;

        Vector3 desired = screenPos + offset;

        // Clamp to screen to avoid going off-screen.
        float pad = 12f;
        desired.x = Mathf.Clamp(desired.x, pad, Screen.width - pad);
        desired.y = Mathf.Clamp(desired.y, pad, Screen.height - pad);

        panelRectTransform.position = desired;
    }
}