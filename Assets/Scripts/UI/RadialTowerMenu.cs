using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Screen-space radial menu: Route A, Route B, Sell (English labels only).</summary>
public class RadialTowerMenu : MonoBehaviour
{
    public const float UiScale = 1.125f;

    const float RouteBtnW = 92f;
    const float RouteBtnH = 102f;
    const float IconSize = 48f;
    const float OffUpLeftX = -54f;
    const float OffUpLeftY = 42f;
    const float OffUpRightX = 54f;
    const float OffSellY = -50f;
    const float SellBtnW = 100f;
    const float SellBtnH = 44f;

    TowerMenu _menu;
    Camera _cam;
    Canvas _rootCanvas;
    RectTransform _canvasRect;
    RectTransform _clusterRt;
    Button _routeABtn;
    Button _routeBBtn;
    Button _sellBtn;
    Image _routeAIcon;
    Image _routeBIcon;
    TextMeshProUGUI _routeALabel;
    TextMeshProUGUI _routeBLabel;

    Color _routeANormalColor = new Color(0.22f, 0.48f, 0.58f, 0.96f);
    Color _routeBNormalColor = new Color(0.38f, 0.28f, 0.55f, 0.96f);
    Color _lockedColor = new Color(0.22f, 0.22f, 0.24f, 0.75f);

    public void SetupScreenRadial(TowerMenu menu, Canvas canvas)
    {
        if (_clusterRt != null)
            return;

        _menu = menu;
        _cam = menu.mainCamera != null ? menu.mainCamera : Camera.main;
        _rootCanvas = canvas;
        _canvasRect = canvas.GetComponent<RectTransform>();

        Transform host = transform;
        var clusterGo = new GameObject("RadialCluster", typeof(RectTransform));
        clusterGo.transform.SetParent(host, false);
        clusterGo.layer = host.gameObject.layer;

        _clusterRt = clusterGo.GetComponent<RectTransform>();
        _clusterRt.anchorMin = new Vector2(0.5f, 0.5f);
        _clusterRt.anchorMax = new Vector2(0.5f, 0.5f);
        _clusterRt.pivot = new Vector2(0.5f, 0.5f);
        _clusterRt.sizeDelta = new Vector2(340f * UiScale, 260f * UiScale);
        _clusterRt.anchoredPosition = Vector2.zero;

        float rw = RouteBtnW * UiScale;
        float rh = RouteBtnH * UiScale;
        float fs = 12.5f * UiScale;

        _routeABtn = CreateRouteStyleButton(_clusterRt, "RouteA",
            new Vector2(OffUpLeftX * UiScale, OffUpLeftY * UiScale),
            new Vector2(rw, rh), _routeANormalColor,
            out _routeAIcon, out _routeALabel, fs,
            () =>
            {
                if (_menu != null)
                    _menu.TryPurchaseRoute(TowerRouteKind.A);
            });

        _routeBBtn = CreateRouteStyleButton(_clusterRt, "RouteB",
            new Vector2(OffUpRightX * UiScale, OffUpLeftY * UiScale),
            new Vector2(rw, rh), _routeBNormalColor,
            out _routeBIcon, out _routeBLabel, fs,
            () =>
            {
                if (_menu != null)
                    _menu.TryPurchaseRoute(TowerRouteKind.B);
            });

        _sellBtn = CreateSimpleButton(_clusterRt, "Sell",
            new Vector2(0f, OffSellY * UiScale),
            new Vector2(SellBtnW * UiScale, SellBtnH * UiScale),
            14f * UiScale,
            new Color(0.52f, 0.2f, 0.2f, 0.95f),
            () =>
            {
                if (_menu != null)
                    _menu.SellSelectedTower();
            });

        clusterGo.SetActive(false);
    }

    Button CreateRouteStyleButton(RectTransform parent, string nodeName, Vector2 anchored, Vector2 size, Color iconBg,
        out Image iconOut, out TextMeshProUGUI labelOut, float labelFontSize, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject(nodeName, typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);

        var rt = btnGo.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;

        var bg = btnGo.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.14f, 0.88f);
        bg.raycastTarget = true;

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        float pad = 4f * UiScale;
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(btnGo.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 1f);
        iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.sizeDelta = new Vector2(IconSize * UiScale, IconSize * UiScale);
        iconRt.anchoredPosition = new Vector2(0f, -pad);

        iconOut = iconGo.AddComponent<Image>();
        iconOut.color = iconBg;
        iconOut.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(btnGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 0.42f);
        textRt.pivot = new Vector2(0.5f, 0f);
        textRt.offsetMin = new Vector2(pad, 2f);
        textRt.offsetMax = new Vector2(-pad, -2f);

        labelOut = textGo.AddComponent<TextMeshProUGUI>();
        labelOut.fontSize = labelFontSize;
        labelOut.alignment = TextAlignmentOptions.Midline;
        labelOut.raycastTarget = false;
        labelOut.enableWordWrapping = true;
        CopyTmpFont(labelOut);

        return btn;
    }

    Button CreateSimpleButton(RectTransform parent, string label, Vector2 anchored, Vector2 size, float fontSize, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject("Btn_" + label, typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);

        var rt = btnGo.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;

        var img = btnGo.AddComponent<Image>();
        img.color = bg;
        img.raycastTarget = true;
        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = bg;
        colors.highlightedColor = new Color(
            Mathf.Min(1f, bg.r * 1.15f),
            Mathf.Min(1f, bg.g * 1.15f),
            Mathf.Min(1f, bg.b * 1.15f),
            1f);
        colors.pressedColor = new Color(bg.r * 0.82f, bg.g * 0.82f, bg.b * 0.82f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(btnGo.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.raycastTarget = false;
        CopyTmpFont(tmp);

        return btn;
    }

    public void Show(TowerMenu menu)
    {
        _menu = menu;
        if (_clusterRt != null)
            _clusterRt.gameObject.SetActive(true);
        RefreshRouteLabels();
        RefreshButtonStates();
    }

    void RefreshRouteLabels()
    {
        if (_menu == null || _menu.SelectedTower == null)
            return;
        Tower t = _menu.SelectedTower;
        if (_routeALabel != null)
            _routeALabel.text = t.GetRouteAButtonLabel();
        if (_routeBLabel != null)
            _routeBLabel.text = t.GetRouteBButtonLabel();
    }

    public void Hide()
    {
        if (_clusterRt != null)
            _clusterRt.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (_clusterRt == null || !_clusterRt.gameObject.activeInHierarchy || _menu == null || _cam == null || _canvasRect == null)
            return;

        Tower tower = _menu.SelectedTower;
        if (tower == null)
            return;

        Vector3 sp = _cam.WorldToScreenPoint(tower.transform.position);
        if (sp.z < 0.01f)
        {
            _clusterRt.gameObject.SetActive(false);
            return;
        }

        Camera eventCam = GetEventCamera();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, new Vector2(sp.x, sp.y), eventCam, out Vector2 local))
            _clusterRt.anchoredPosition = local;
    }

    Camera GetEventCamera()
    {
        if (_rootCanvas == null)
            return null;
        if (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return _rootCanvas.worldCamera != null ? _rootCanvas.worldCamera : _cam;
    }

    public void RefreshButtonStates()
    {
        if (_menu == null || _menu.SelectedTower == null)
            return;

        Tower t = _menu.SelectedTower;
        RefreshRouteLabels();
        ApplyRouteButtonVisual(_routeABtn, _routeAIcon, t, TowerRouteKind.A, _routeANormalColor);
        ApplyRouteButtonVisual(_routeBBtn, _routeBIcon, t, TowerRouteKind.B, _routeBNormalColor);
    }

    void ApplyRouteButtonVisual(Button btn, Image icon, Tower t, TowerRouteKind route, Color normalCol)
    {
        if (btn == null)
            return;

        bool locked = t.IsRouteButtonLocked(route);
        bool canInteract = t.IsRouteButtonInteractable(route);

        if (locked)
        {
            btn.interactable = false;
            if (icon != null)
                icon.color = _lockedColor;
            return;
        }

        if (!canInteract)
        {
            btn.interactable = false;
            if (icon != null)
                icon.color = new Color(normalCol.r * 0.45f, normalCol.g * 0.45f, normalCol.b * 0.45f, 0.85f);
            return;
        }

        btn.interactable = true;
        if (icon != null)
            icon.color = normalCol;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void CopyTmpFont(TextMeshProUGUI tmp)
    {
        if (tmp.font != null) return;
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
            return;
        }
        var any = Object.FindObjectOfType<TextMeshProUGUI>();
        if (any != null && any.font != null)
            tmp.font = any.font;
    }
}
