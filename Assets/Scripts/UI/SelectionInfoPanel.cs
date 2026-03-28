using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 塔与敌人共用的底部信息条：左头像、中主体、右补充。同一时间只显示一种目标。
/// </summary>
public class SelectionInfoPanel : MonoBehaviour
{
    public enum ContentMode
    {
        Hidden,
        Tower,
        Enemy
    }

    public static SelectionInfoPanel Instance { get; private set; }

    [SerializeField] Color panelBg = new Color(0.06f, 0.07f, 0.09f, 0.94f);
    [SerializeField] Color portraitTower = new Color(0.22f, 0.24f, 0.28f, 1f);
    [SerializeField] Color portraitEnemyNormal = new Color(0.42f, 0.45f, 0.5f, 1f);
    [SerializeField] Color portraitEnemyFast = new Color(0.32f, 0.48f, 0.78f, 1f);
    [SerializeField] Color portraitEnemyTank = new Color(0.55f, 0.28f, 0.26f, 1f);
    [SerializeField] Color portraitEnemyShield = new Color(0.35f, 0.62f, 0.88f, 1f);
    [SerializeField] Color avatarFrameColor = new Color(0.1f, 0.11f, 0.14f, 1f);

    RectTransform _root;
    Image _portrait;
    TextMeshProUGUI _titleTmp;
    TextMeshProUGUI _subtitleTmp;
    TextMeshProUGUI _bodyTmp;
    TextMeshProUGUI _rightTmp;

    ContentMode _mode = ContentMode.Hidden;
    Enemy _enemy;
    TowerMenu _towerMenu;
    /// <summary>当前底部栏已绑定的塔实例；用于同塔重复 Show 时跳过无意义刷新。</summary>
    Tower _boundTower;

    int _lastRouteUiLogTowerId = int.MinValue;
    string _lastRouteUiLogKey;
    bool _loggedShieldInfoPanelOnce;

    public bool IsShowing => _root != null && _root.gameObject.activeSelf;
    public bool IsShowingEnemy => IsShowing && _mode == ContentMode.Enemy;

    public static void EnsureBuilt(Canvas canvas)
    {
        if (canvas == null || Instance != null)
            return;
        var go = new GameObject("SelectionInfoPanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var panel = go.AddComponent<SelectionInfoPanel>();
        panel.BuildUi(canvas);
        Instance = panel;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void BuildUi(Canvas canvas)
    {
        // 原约 118px → 压缩到约 55%~60%
        const float panelH = 70f;
        const float padH = 10f;
        const float padV = 5f;
        const float colGap = 10f;
        const float avatarOuter = 48f;
        const float frameInset = 2f;
        const float extraColW = 148f;
        const float titleRowH = 17f;
        const float subtitleRowH = 12f;
        const float mainTopInset = titleRowH + subtitleRowH;

        // 字号层级：名称 > 等级/类型 > 属性 ≈ 右侧补充
        const float fsTitle = 13f;
        const float fsSubtitle = 10f;
        const float fsBody = 9f;
        const float fsRight = 9f;

        _root = GetComponent<RectTransform>();
        _root.anchorMin = new Vector2(0f, 0f);
        _root.anchorMax = new Vector2(1f, 0f);
        _root.pivot = new Vector2(0.5f, 0f);
        _root.sizeDelta = new Vector2(0f, panelH);
        _root.anchoredPosition = Vector2.zero;

        var bg = gameObject.AddComponent<Image>();
        bg.color = panelBg;
        bg.raycastTarget = false;

        var rowGo = new GameObject("ContentRow", typeof(RectTransform));
        rowGo.transform.SetParent(transform, false);
        var rowRt = rowGo.GetComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;
        rowRt.offsetMin = new Vector2(padH, padV);
        rowRt.offsetMax = new Vector2(-padH, -padV);

        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = colGap;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.padding = new RectOffset(0, 0, 0, 0);

        // —— 左：头像模块（外框 + 内色块）——
        var avatarContainer = new GameObject("AvatarContainer", typeof(RectTransform));
        avatarContainer.transform.SetParent(rowGo.transform, false);
        var avLe = avatarContainer.AddComponent<LayoutElement>();
        avLe.minWidth = avatarOuter;
        avLe.preferredWidth = avatarOuter;
        avLe.minHeight = avatarOuter;
        avLe.preferredHeight = avatarOuter;
        avLe.flexibleWidth = 0f;

        var frameGo = new GameObject("AvatarFrame", typeof(RectTransform));
        frameGo.transform.SetParent(avatarContainer.transform, false);
        var frameRt = frameGo.GetComponent<RectTransform>();
        frameRt.anchorMin = Vector2.zero;
        frameRt.anchorMax = Vector2.one;
        frameRt.offsetMin = Vector2.zero;
        frameRt.offsetMax = Vector2.zero;
        var frameImg = frameGo.AddComponent<Image>();
        frameImg.color = avatarFrameColor;
        frameImg.raycastTarget = false;

        var portraitGo = new GameObject("Portrait", typeof(RectTransform));
        portraitGo.transform.SetParent(frameGo.transform, false);
        var pRt = portraitGo.GetComponent<RectTransform>();
        pRt.anchorMin = Vector2.zero;
        pRt.anchorMax = Vector2.one;
        pRt.offsetMin = new Vector2(frameInset, frameInset);
        pRt.offsetMax = new Vector2(-frameInset, -frameInset);
        _portrait = portraitGo.AddComponent<Image>();
        _portrait.raycastTarget = false;

        // —— 中：主体信息 ——
        var mainGo = new GameObject("MainInfoContainer", typeof(RectTransform));
        mainGo.transform.SetParent(rowGo.transform, false);
        var mainLe = mainGo.AddComponent<LayoutElement>();
        mainLe.flexibleWidth = 1f;
        mainLe.minWidth = 80f;

        void MakeMainTmp(string name, float topY, float height, float fs, FontStyles sty, TextAlignmentOptions align,
            Color32? color, out TextMeshProUGUI tmp)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(mainGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, topY);
            tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fs;
            tmp.fontStyle = sty;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.lineSpacing = 0f;
            if (color.HasValue)
                tmp.color = color.Value;
            CopyFont(tmp);
        }

        MakeMainTmp("Title", 0f, titleRowH, fsTitle, FontStyles.Bold, TextAlignmentOptions.Left,
            null, out _titleTmp);
        MakeMainTmp("Subtitle", -titleRowH, subtitleRowH, fsSubtitle, FontStyles.Normal, TextAlignmentOptions.Left,
            new Color32(180, 188, 198, 255), out _subtitleTmp);

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(mainGo.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = new Vector2(0f, -mainTopInset);
        _bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        _bodyTmp.fontSize = fsBody;
        _bodyTmp.fontStyle = FontStyles.Normal;
        _bodyTmp.alignment = TextAlignmentOptions.TopLeft;
        _bodyTmp.raycastTarget = false;
        _bodyTmp.enableWordWrapping = true;
        _bodyTmp.lineSpacing = -12f;
        _bodyTmp.margin = new Vector4(0f, 0f, 4f, 0f);
        CopyFont(_bodyTmp);

        // —— 右：补充信息列 ——
        var extraGo = new GameObject("ExtraInfoContainer", typeof(RectTransform));
        extraGo.transform.SetParent(rowGo.transform, false);
        var exLe = extraGo.AddComponent<LayoutElement>();
        exLe.minWidth = extraColW;
        exLe.preferredWidth = extraColW;
        exLe.flexibleWidth = 0f;

        var rightRtGo = new GameObject("Right", typeof(RectTransform));
        rightRtGo.transform.SetParent(extraGo.transform, false);
        var rr = rightRtGo.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;
        _rightTmp = rightRtGo.AddComponent<TextMeshProUGUI>();
        _rightTmp.fontSize = fsRight;
        _rightTmp.fontStyle = FontStyles.Normal;
        _rightTmp.alignment = TextAlignmentOptions.TopRight;
        _rightTmp.raycastTarget = false;
        _rightTmp.enableWordWrapping = true;
        _rightTmp.lineSpacing = -2f;
        _rightTmp.color = new Color32(200, 206, 214, 255);
        CopyFont(_rightTmp);

        gameObject.SetActive(false);
    }

    public void ShowTower(TowerMenu menu)
    {
        Canvas c = FindObjectOfType<Canvas>();
        if (c == null) return;
        EnsureBuilt(c);
        if (menu == null || menu.SelectedTower == null)
            return;

        Tower t = menu.SelectedTower;
        // 已由 TowerMenu 每帧 Refresh 触发的重复 Show：同塔且已显示则不再跑内容与日志
        if (_mode == ContentMode.Tower && _boundTower == t && _towerMenu == menu && IsShowing)
            return;

        bool wasEnemy = _mode == ContentMode.Enemy;
        bool wasTower = _mode == ContentMode.Tower;
        bool wasHidden = _mode == ContentMode.Hidden || !IsShowing;

        if (wasEnemy)
            Debug.Log($"[SelectionUI] Switch info target to tower = {t.gameObject.name}");
        else if (wasTower && _boundTower != null && _boundTower != t)
            Debug.Log($"[SelectionUI] Switch info target to tower = {t.gameObject.name}");
        else if (wasHidden)
            Debug.Log($"[SelectionUI] Show tower info = {t.gameObject.name}");

        _mode = ContentMode.Tower;
        _towerMenu = menu;
        _enemy = null;
        _boundTower = t;
        _portrait.color = portraitTower;

        ApplyTowerContent(menu);
        _root.gameObject.SetActive(true);
    }

    /// <summary>属性/金币等变化后由 TowerMenu 显式调用，不经过 ShowTower 的“选中”语义与重复防护。</summary>
    public void RefreshTowerFromMenu(TowerMenu menu, string reason = "sync")
    {
        if (_mode != ContentMode.Tower || menu == null || menu.SelectedTower == null)
            return;

        Tower t = menu.SelectedTower;

        _towerMenu = menu;
        _boundTower = t;
        ApplyTowerContent(menu);
        if (!_root.gameObject.activeSelf)
            _root.gameObject.SetActive(true);
    }

    void ApplyTowerContent(TowerMenu menu)
    {
        Tower t = menu.SelectedTower;
        if (t == null)
            return;

        string name = t.gameObject.name.Replace("(Clone)", "").Trim();
        _titleTmp.text = name;
        _subtitleTmp.text = $"Route: {t.GetRouteSummaryForUi()} · Lv {t.level}";

        var sb = new StringBuilder(256);
        sb.Append($"DMG {t.bulletDamage:0.#}  RNG {t.attackRange:0.#}");
        float aps = t.attackInterval > 1e-4f ? 1f / t.attackInterval : 0f;
        sb.Append($"\nAPS {aps:0.##}");
        if (t.IsSplashTower) sb.Append($"\nSplash {t.splashRadius:0.##}");
        if (t.IsSniperTower) sb.Append($"\nShot {(t.bulletSpeed > 0.001f ? t.bulletSpeed : 12f):0.#}");
        if (t.IsSniperTower && t.SelectedRoute == TowerRouteKind.A)
            sb.Append("\nExecute: Tank > HP > nearest");
        if (t.IsSniperTower && t.SelectedRoute == TowerRouteKind.B && t.RouteLevel > 0)
            sb.Append($"\nPierce: {t.GetSniperPierceMaxEnemyHits()} targets · falloff past range");
        if (t.IsBasicTower && t.SelectedRoute == TowerRouteKind.B && t.RouteLevel > 0)
            sb.Append("\nOn-hit slow (Control)");
        if (t.IsSplashTower && t.SelectedRoute == TowerRouteKind.A && t.RouteLevel > 0)
            sb.Append($"\nBlast hit FX x{t.GetAoeBlastHitFxScale():0.##}");
        if (t.IsSplashTower && t.SelectedRoute == TowerRouteKind.B && t.RouteLevel > 0)
            sb.Append($"\nControl DOT {t.GetAoeControlDotPerTick():0.##}/0.5s · zone {t.GetAoeControlZoneDuration():0.#}s");
        _bodyTmp.text = sb.ToString();

        int sell = t.GetSellValue();
        bool atMax = t.IsAtMaxLevel();
        _rightTmp.text = FormatTowerRouteCostBlock(t, sell, atMax);
    }

    /// <summary>Right column: route costs and sell (English labels only).</summary>
    string FormatTowerRouteCostBlock(Tower t, int sell, bool atMax)
    {
        if (t.IsSplashTower && t.SelectedRoute != TowerRouteKind.None)
        {
            string rn = t.SelectedRoute == TowerRouteKind.A ? "Blast" : "Control";
            LogRouteUiOnce(t, $"aoe-{rn}-lv{t.RouteLevel}-{sell}", $"[AOERouteUI] Show {rn} Lv{t.RouteLevel}");
        }

        if (atMax)
        {
            LogRouteUiOnce(t, "max", "[TowerRouteUI] Show max route state");
            return $"Next: MAX\nSell: {sell}";
        }

        if (t.SelectedRoute == TowerRouteKind.None)
        {
            int firstTier = t.GetUpgradeCost();
            LogRouteUiOnce(t, $"none-{firstTier}-{sell}", $"[TowerRouteUI] Show initial route costs A={firstTier} B={firstTier}");
            return $"A Cost: {firstTier}\nB Cost: {firstTier}\nSell: {sell}";
        }

        int next = t.GetUpgradeCost();
        if (t.SelectedRoute == TowerRouteKind.A)
        {
            LogRouteUiOnce(t, $"A-{next}-{sell}", $"[TowerRouteUI] Show route A progression next={next}");
            return $"Next A: {next}\nRoute B: Locked\nSell: {sell}";
        }

        LogRouteUiOnce(t, $"B-{next}-{sell}", $"[TowerRouteUI] Show route B progression next={next}");
        return $"Next B: {next}\nRoute A: Locked\nSell: {sell}";
    }

    void LogRouteUiOnce(Tower t, string stateKey, string message)
    {
        int id = t != null ? t.GetInstanceID() : 0;
        string k = id + "|" + stateKey;
        if (_lastRouteUiLogTowerId == id && _lastRouteUiLogKey == k)
            return;
        _lastRouteUiLogTowerId = id;
        _lastRouteUiLogKey = k;
        Debug.Log(message);
    }

    public void ShowEnemy(Enemy enemy)
    {
        Canvas c = FindObjectOfType<Canvas>();
        if (c == null) return;
        EnsureBuilt(c);
        if (enemy == null || !enemy.IsAliveForInfoPanel())
            return;

        bool wasTower = _mode == ContentMode.Tower;
        TowerMenu.Instance?.HideRadialAndDeselectForEnemy();

        if (wasTower)
            Debug.Log($"[SelectionUI] Switch info target to enemy = {enemy.gameObject.name}");
        else
            Debug.Log($"[SelectionUI] Show enemy info = {enemy.gameObject.name}");

        _mode = ContentMode.Enemy;
        _enemy = enemy;
        _towerMenu = null;
        _boundTower = null;

        RefreshEnemyVisual();
        _root.gameObject.SetActive(true);
    }

    void RefreshEnemyVisual()
    {
        if (_enemy == null)
            return;

        Enemy enemy = _enemy;
        _portrait.color = enemy.GetInfoKind() switch
        {
            Enemy.InfoKind.Fast => portraitEnemyFast,
            Enemy.InfoKind.Tank => portraitEnemyTank,
            Enemy.InfoKind.Shield => portraitEnemyShield,
            _ => portraitEnemyNormal
        };

        string n = enemy.gameObject.name.Replace("(Clone)", "").Trim();
        _titleTmp.text = n;
        _subtitleTmp.text = $"Type: {enemy.GetInfoKindDisplayName()}";

        float curHp = enemy.GetCurrentHealth();
        float maxHp = enemy.GetFinalMaxHP();
        int atk = enemy.GetFinalDamage();
        float spd = enemy.GetFinalMoveSpeed();
        var sb = new StringBuilder(256);
        sb.Append($"HP {curHp:0.#} / {maxHp:0.#}");
        if (enemy.GetInfoKind() == Enemy.InfoKind.Shield)
        {
            sb.Append($"\nShield {enemy.GetCurrentShield():0.#} / {enemy.GetMaxShield():0.#}");
            if (!_loggedShieldInfoPanelOnce)
            {
                _loggedShieldInfoPanelOnce = true;
                Debug.Log("[ShieldEnemy] Show shield info in panel");
            }
        }

        sb.Append($"\nATK {atk}");
        sb.Append($"\nSPD {spd:0.##}");
        _bodyTmp.text = sb.ToString();

        string state = enemy.HasBuff() ? "Night Buff" : "Normal";
        string buff = enemy.GetBuffDescription();
        if (string.IsNullOrEmpty(buff)) buff = "—";
        string threat = enemy.GetInfoKind() == Enemy.InfoKind.Tank ? "High priority" :
            enemy.GetInfoKind() == Enemy.InfoKind.Fast ? "Fast mover" :
            enemy.GetInfoKind() == Enemy.InfoKind.Shield ? "Shielded" : "Standard";
        _rightTmp.text = $"State  {state}\nBuff  {buff}\nThreat  {threat}";
    }

    public void Hide()
    {
        if (_root == null || !_root.gameObject.activeSelf)
        {
            _mode = ContentMode.Hidden;
            _enemy = null;
            _towerMenu = null;
            _boundTower = null;
            return;
        }

        Debug.Log("[SelectionUI] Hide shared info panel");
        _root.gameObject.SetActive(false);
        _mode = ContentMode.Hidden;
        _enemy = null;
        _towerMenu = null;
        _boundTower = null;
    }

    void LateUpdate()
    {
        if (_mode != ContentMode.Enemy || _enemy == null || !_root.gameObject.activeSelf)
            return;
        if (!_enemy.IsAliveForInfoPanel())
        {
            Hide();
            return;
        }
        RefreshEnemyVisual();
    }

    static void CopyFont(TextMeshProUGUI tmp)
    {
        if (tmp.font != null) return;
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            return;
        }
        var any = FindObjectOfType<TextMeshProUGUI>();
        if (any != null && any.font != null)
            tmp.font = any.font;
    }
}
