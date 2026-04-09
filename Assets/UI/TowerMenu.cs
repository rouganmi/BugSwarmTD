using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TowerMenu : MonoBehaviour
{
    static TowerMenu _instance;
    private const string PrototypeSceneName = "Chapter1_Node1_Prototype";
    private const float PrototypeUpgradeCostMultiplier = 1.3f;

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

    /// <summary>Hex：Canvas 下全屏占位，承载屏幕空间环形菜单（<see cref="RadialTowerMenu"/>）。底部信息见 <see cref="SelectionInfoPanel"/>。</summary>
    GameObject _radialUiHost;

    RadialTowerMenu _radialMenu;

    public Tower SelectedTower => selectedTower;

    public bool IsOpen =>
        (panel != null && panel.activeSelf) ||
        (_radialUiHost != null && _radialUiHost.activeSelf);

    /// <summary>供 <see cref="HexGridManager"/> 等在无引用时判断塔菜单是否打开。</summary>
    public static TowerMenu Instance => _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
            return;
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

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

    void EnsureHexChrome()
    {
        if (_radialUiHost != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[TowerUI] EnsureHexChrome failed: no Canvas in scene.");
            return;
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        SelectionInfoPanel.EnsureBuilt(canvas);

        var radialRoot = new GameObject("RadialTowerMenuRoot", typeof(RectTransform));
        radialRoot.transform.SetParent(canvas.transform, false);
        radialRoot.transform.SetAsLastSibling();
        var rrt = radialRoot.GetComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = Vector2.zero;
        _radialUiHost = radialRoot;
        _radialMenu = radialRoot.AddComponent<RadialTowerMenu>();
        _radialMenu.SetupScreenRadial(this, canvas);

        _radialUiHost.SetActive(false);
    }

    /// <summary>选中敌人时关闭环形菜单与塔选中，但不隐藏共用底部信息栏（将由 <see cref="SelectionInfoPanel"/> 切换为敌人）。</summary>
    public void HideRadialAndDeselectForEnemy()
    {
        selectedTower = null;
        selectedSpot = null;

        if (_radialUiHost != null)
        {
            bool radialWasOn = _radialUiHost.activeSelf;
            _radialUiHost.SetActive(false);
            _radialMenu?.Hide();
            if (radialWasOn)
                Debug.Log("[TowerUI] Hide radial menu");
        }

        var ts = FindObjectOfType<TowerSelector>();
        ts?.ClearTowerSelectionPublic();

        if (panel != null)
            panel.SetActive(false);
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

        BuildSpot activeSpot = GetActiveSelectedSpot();
        if (activeSpot != null && activeSpot.GetCurrentTower() != selectedTower)
        {
            HideMenu();
            return false;
        }

        return true;
    }

    public void ShowMenu(Tower tower)
    {
        selectedTower = tower;
        if (selectedTower != null && selectedTower.OwningSpot != null)
            selectedSpot = selectedTower.OwningSpot;

        if (HexGridManager.Instance != null)
        {
            EnsureHexChrome();
            if (_radialUiHost == null)
            {
                Debug.LogWarning("[TowerUI] Hex tower UI failed: RadialTowerMenuRoot not created (missing Canvas?).");
                return;
            }

            if (panel != null)
                panel.SetActive(false);

            _radialUiHost.SetActive(true);

            SelectionInfoPanel.Instance?.ShowTower(this);
            _radialMenu?.Show(this);

            Debug.Log($"[TowerUI] Show radial menu for {tower.gameObject.name}");

            RefreshInfo();
            UpdatePanelPosition();
            return;
        }

        if (panel != null)
            panel.SetActive(true);

        RefreshInfo();
        UpdatePanelPosition();
    }

    public void HideMenu()
    {
        bool hadHexChrome = HexGridManager.Instance != null &&
            (_radialUiHost != null && _radialUiHost.activeSelf);

        if (hadHexChrome)
            Debug.Log("[TowerUI] Hide tower menu");

        selectedTower = null;
        selectedSpot = null;

        if (_radialUiHost != null)
        {
            _radialUiHost.SetActive(false);
            _radialMenu?.Hide();
            if (hadHexChrome)
                Debug.Log("[TowerUI] Hide radial menu");
        }

        if (panel != null)
            panel.SetActive(false);

        SelectionInfoPanel.Instance?.Hide();
    }

    BuildSpot GetActiveSelectedSpot()
    {
        if (selectedTower != null && selectedTower.OwningSpot != null)
            return selectedTower.OwningSpot;
        return selectedSpot;
    }

    /// <summary>Legacy UI / UnityEvent: picks Route A if none selected, else continues current route.</summary>
    public void UpgradeSelectedTower()
    {
        if (selectedTower == null || currencySystem == null)
        {
            Debug.Log("[TowerUpgrade] Upgrade failed reason=no_tower_or_currency");
            return;
        }

        TowerRouteKind r = selectedTower.SelectedRoute == TowerRouteKind.None
            ? TowerRouteKind.A
            : selectedTower.SelectedRoute;
        TryPurchaseRoute(r);
    }

    public void TryPurchaseRoute(TowerRouteKind route)
    {
        if (selectedTower == null || currencySystem == null)
            return;

        Tower t = selectedTower;

        if (t.IsRouteButtonLocked(route))
        {
            Debug.Log($"[TowerRoute] Route button locked = {route}");
            return;
        }

        if (!t.IsRouteButtonInteractable(route))
            return;

        if (t.IsAtMaxLevel())
        {
            Debug.Log("[TowerUpgrade] Upgrade failed reason=max_level");
            RefreshInfo();
            SyncSelectionInfoPanel("max_level");
            return;
        }

        int cost = t.GetNextRouteUpgradeCost();
        if (IsPrototypeScene())
            cost = AdjustPrototypeUpgradeCost(cost);
        if (cost <= 0)
            return;

        if (!currencySystem.HasEnoughGold(cost))
        {
            Debug.Log($"[TowerUpgrade] Upgrade failed reason=insufficient_gold need={cost} have={currencySystem.GetCurrentGold()}");
            RefreshInfo();
            SyncSelectionInfoPanel("gold_state");
            return;
        }

        if (!currencySystem.SpendGold(cost))
        {
            Debug.Log("[TowerUpgrade] Upgrade failed reason=spend_rejected");
            SyncSelectionInfoPanel("spend_rejected");
            return;
        }

        t.ApplyRouteUpgradeAfterPurchase(route, cost);
        Debug.Log($"[TowerRoute] Refresh bottom info panel for tower = {t.gameObject.name}");

        RefreshInfo();
        SyncSelectionInfoPanel("route_upgrade");

        if (panel != null && panel.activeSelf)
        {
            var presenter = panel.GetComponent<TowerMenuPanelPresenter>();
            presenter?.PlayUpgradeFeedback();
        }
    }

    public void SellSelectedTower()
    {
        BuildSpot activeSpot = GetActiveSelectedSpot();
        if (selectedTower == null || currencySystem == null || activeSpot == null)
        {
            Debug.Log("[TowerSell] Sell aborted (missing tower, currency, or spot)");
            return;
        }

        var hex = activeSpot.GetComponentInParent<HexCell>();
        int refund = selectedTower.GetSellValue();
        string towerName = selectedTower.gameObject.name;

        currencySystem.AddGold(refund);
        Destroy(selectedTower.gameObject);

        if (hex != null)
            hex.NotifyTowerSold();
        else
            activeSpot.ClearTower();

        Debug.Log($"[TowerSell] Sell success tower={towerName} refund={refund}");

        HideMenu();
    }

    private void RefreshInfo()
    {
        if (selectedTower == null)
            return;

        Tower tower = selectedTower;
        float currentDamage = tower.bulletDamage;
        bool splash = tower.splashRadius > 0.001f;
        bool sniper = tower.IsSniperTower;
        float dmgBonus = splash ? 0.4f : (sniper ? 1.15f : 1f);
        float nextDamage = currentDamage + dmgBonus;

        float currentRange = tower.attackRange;
        float rangeBonus = sniper ? 1.15f : 1f;
        float nextRange = currentRange + rangeBonus;

        float nextInterval = splash
            ? Mathf.Max(0.2f, tower.attackInterval - 0.06f)
            : sniper
                ? Mathf.Max(0.38f, tower.attackInterval - 0.05f)
                : Mathf.Max(0.2f, tower.attackInterval - 0.1f);

        float splashDmg = splash ? currentDamage * tower.splashDamageRatio : 0f;
        float nextSplashDmg = splash ? nextDamage * tower.splashDamageRatio : 0f;
        float splashRad = tower.splashRadius;
        float nextSplashRad = splash ? Mathf.Min(splashRad + 0.14f, 3.5f) : 0f;

        int cost = tower.GetUpgradeCost();
        if (IsPrototypeScene())
            cost = AdjustPrototypeUpgradeCost(cost);
        int sell = tower.GetSellValue();

        bool atMax = tower.IsAtMaxLevel();
        bool canAfford = currencySystem != null && currencySystem.HasEnoughGold(cost);
        bool canUpgrade = !atMax && canAfford;

        string splashBlock = "";
        if (splash)
        {
            if (tower.SelectedRoute == TowerRouteKind.B && tower.RouteLevel > 0)
            {
                float dot = tower.GetAoeControlDotPerTick();
                splashBlock = $"Control zone DOT: {dot:0.##} per 0.5s\n\n";
            }
            else
            {
                splashBlock =
                    $"Splash radius: {splashRad:0.##}\n" +
                    $"Splash dmg: {splashDmg:0.#}\n\n";
                if (!atMax)
                {
                    splashBlock =
                        $"Splash radius: {splashRad:0.##} -> {nextSplashRad:0.##}\n" +
                        $"Splash dmg: {splashDmg:0.#} -> {nextSplashDmg:0.#}\n\n";
                }
            }
        }

        string sniperBlock = sniper ? $"Target: Tank > High HP\nShot speed: {(tower.bulletSpeed > 0.001f ? tower.bulletSpeed : 12f):0.#}\n\n" : "";

        string intervalAtMax = sniper ? $"Interval: {tower.attackInterval:0.##}s\n" : "";
        string intervalUpgrade = sniper ? $"Interval: {tower.attackInterval:0.##}s -> {nextInterval:0.##}s\n" : "";

        bool legacyVisible = panel != null && panel.activeSelf && infoText != null;

        if (legacyVisible)
        {
            if (atMax)
            {
                infoText.text =
                    $"Tower Lv.{tower.level}  [MAX]\n\n" +
                    $"Damage: {currentDamage:0.#}\n" +
                    $"Range: {currentRange:0.#}\n" +
                    intervalAtMax +
                    sniperBlock +
                    splashBlock +
                    $"Upgrade Cost: MAX\n" +
                    $"Sell Value: {sell}";
            }
            else
            {
                infoText.text =
                    $"Tower Lv.{tower.level}\n\n" +
                    $"Damage: {currentDamage:0.#} -> {nextDamage:0.#}\n" +
                    $"Range: {currentRange:0.#} -> {nextRange:0.#}\n" +
                    intervalUpgrade +
                    sniperBlock +
                    splashBlock +
                    $"Upgrade Cost: {cost}\n" +
                    $"Sell Value: {sell}";
            }
        }

        if (upgradeButton != null && legacyVisible)
            upgradeButton.interactable = !atMax;

        if (legacyVisible && panel != null)
        {
            var presenter = panel.GetComponent<TowerMenuPanelPresenter>();
            presenter?.SetUpgradeState(atMax, !atMax && !canAfford);
        }

        if (HexGridManager.Instance != null && _radialUiHost != null && _radialUiHost.activeSelf)
            _radialMenu?.RefreshButtonStates();
    }

    private bool IsPrototypeScene()
    {
        return gameObject.scene.IsValid() &&
               string.Equals(gameObject.scene.name, PrototypeSceneName, System.StringComparison.Ordinal);
    }

    private static int AdjustPrototypeUpgradeCost(int baseCost)
    {
        if (baseCost <= 0) return baseCost;
        return Mathf.Max(1, Mathf.CeilToInt(baseCost * PrototypeUpgradeCostMultiplier));
    }

    /// <summary>仅在属性/金币等需要更新底部栏时调用；勿在 Update 每帧调用。</summary>
    void SyncSelectionInfoPanel(string reason)
    {
        if (HexGridManager.Instance == null || SelectionInfoPanel.Instance == null || _radialUiHost == null || !_radialUiHost.activeSelf)
            return;
        SelectionInfoPanel.Instance.RefreshTowerFromMenu(this, reason);
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

        // Auto place panel away from the tower body, with a bias to keep the battlefield center clearer:
        // prefer right + slightly lower placement unless near edges.
        if (screenPos.x > Screen.width * 0.66f) offset.x = -Mathf.Abs(offset.x) - 220f;
        else offset.x = Mathf.Abs(offset.x) + 280f;

        if (screenPos.y > Screen.height * 0.54f) offset.y = -Mathf.Abs(offset.y) - 170f;
        else offset.y = Mathf.Abs(offset.y) + 110f;

        Vector3 desired = screenPos + offset;

        // Clamp to screen to avoid going off-screen (account for panel size so it doesn't hang off edges).
        float pad = 12f;
        Vector2 size = panelRectTransform.rect.size;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;
        desired.x = Mathf.Clamp(desired.x, pad + halfW, Screen.width - pad - halfW);
        desired.y = Mathf.Clamp(desired.y, pad + halfH, Screen.height - pad - halfH);

        panelRectTransform.position = desired;
    }
}
