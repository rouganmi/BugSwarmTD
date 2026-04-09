using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
/// <summary>
/// Modal list of towers to place on an empty <see cref="BuildSpot"/>. Separate from tower upgrade/sell <see cref="TowerMenu"/>.
/// </summary>
public class BuildSelectionUI : MonoBehaviour
{
    /// <summary>供 Hex 建造入口等稳定获取，避免在场景中盲搜。</summary>
    public static BuildSelectionUI Instance { get; private set; }

    /// <summary>供 Hex 输入桥接解析现有 BuildSelectionUI；不负责创建。</summary>
    [Header("Data")]
    [SerializeField] private BuildTowerOption[] towerOptions;

    [Tooltip("If no entries are configured, copy prefab/cost from TowerBuilder once at runtime.")]
    [SerializeField] private bool autoFillFromTowerBuilderIfEmpty = true;

    [Header("Splash / AoE tower")]
    [Tooltip("Appended to the list at runtime unless an entry with id \"aoe\" already exists.")]
    [SerializeField] private GameObject aoeTowerPrefab;

    [SerializeField] private int aoeTowerCost = 52;

    [Header("Sniper tower")]
    [Tooltip("Appended to the list at runtime unless an entry with id \"sniper\" already exists.")]
    [SerializeField] private GameObject sniperTowerPrefab;

    [SerializeField] private int sniperTowerCost = 68;

    [Header("Refs (optional)")]
    [SerializeField] private CurrencySystem currencySystem;
    [SerializeField] private TowerBuilder towerBuilder;

    [Header("Runtime-built UI")]
    [SerializeField] private Color panelColor = new Color(0.12f, 0.14f, 0.18f, 0.96f);
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color rowNormalColor = Color.white;
    [SerializeField] private Color rowDisabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);

    private Canvas _rootCanvas;
    private RectTransform _modalRoot;
    private GameObject _panelGo;
    private Transform _rowsParent;
    private readonly List<RowWidgets> _rows = new List<RowWidgets>(8);

    private BuildSpot _pendingSpot;
    private bool _uiBuilt;
    private BuildPreviewController _previewController;

    [Header("Backdrop click guard")]
    [Tooltip("打开后短时间内忽略 Backdrop 关闭，避免同一次鼠标操作立刻关掉面板。")]
    [SerializeField] private float backdropCloseGraceSeconds = 0.15f;

    private float _suppressBackdropCloseUntilUnscaledTime;

    private struct RowWidgets
    {
        public Button Button;
        public CanvasGroup Group;
        public TextMeshProUGUI Label;
        public BuildTowerOption Option;
    }

    private void Awake()
    {
        Instance = this;

        if (currencySystem == null) currencySystem = FindObjectOfType<CurrencySystem>();
        if (towerBuilder == null) towerBuilder = FindObjectOfType<TowerBuilder>();

        _previewController = GetComponent<BuildPreviewController>();
        if (_previewController == null)
            _previewController = gameObject.AddComponent<BuildPreviewController>();

        EnsureOptionsPopulated();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.OnGoldChanged += OnGoldChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnGoldChanged -= OnGoldChanged;
    }

    private void OnGoldChanged(int _)
    {
        if (_modalRoot != null && _modalRoot.gameObject.activeSelf)
            RefreshAffordability();
    }

    private void EnsureOptionsPopulated()
    {
        var list = new List<BuildTowerOption>();
        if (towerOptions != null)
        {
            for (int i = 0; i < towerOptions.Length; i++)
            {
                if (towerOptions[i] != null && towerOptions[i].towerPrefab != null)
                    list.Add(towerOptions[i]);
            }
        }

        if (list.Count == 0 && autoFillFromTowerBuilderIfEmpty && towerBuilder != null && towerBuilder.towerPrefab != null)
        {
            list.Add(new BuildTowerOption
            {
                id = "basic",
                displayName = "Basic Tower",
                towerPrefab = towerBuilder.towerPrefab,
                cost = towerBuilder.towerCost
            });
        }

        if (aoeTowerPrefab != null && !ContainsOptionId(list, "aoe"))
        {
            list.Add(new BuildTowerOption
            {
                id = "aoe",
                displayName = "Splash Tower",
                towerPrefab = aoeTowerPrefab,
                cost = aoeTowerCost
            });
        }

        GameObject sniperPrefab = sniperTowerPrefab;
        if (sniperPrefab == null)
            sniperPrefab = Resources.Load<GameObject>("Prefabs/Tower_Sniper");

        if (sniperPrefab != null && !ContainsOptionId(list, "sniper"))
        {
            list.Add(new BuildTowerOption
            {
                id = "sniper",
                displayName = "Sniper Tower",
                towerPrefab = sniperPrefab,
                cost = sniperTowerCost
            });
        }

        towerOptions = list.ToArray();
    }

    private static bool ContainsOptionId(List<BuildTowerOption> list, string id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].id == id) return true;
        }
        return false;
    }

    /// <summary>Opens the panel for an empty build spot. No-op if spot cannot build.</summary>
    public void OpenForSpot(BuildSpot spot)
    {
        Debug.Log("[BuildUI] OpenForSpot called");

        if (spot == null)
        {
            Debug.Log("[BuildUI] OpenForSpot ignored: spot null");
            return;
        }

        EnsureOptionsPopulated();
        Debug.Log("[BuildUI] EnsureOptionsPopulated done");

        if (!CanOpenForSpot(spot))
        {
            Debug.Log("[BuildUI] OpenForSpot ignored: spot cannot build");
            return;
        }

        EnsureUiBuilt();
        if (_modalRoot == null || _rowsParent == null)
        {
            Debug.LogError("[BuildUI] OpenForSpot aborted: no canvas / ui build failed");
            return;
        }
        Debug.Log("[BuildUI] EnsureUiBuilt done");

        RebuildOptionRows();
        Debug.Log($"[BuildUI] RebuildOptionRows done, rowCount={_rows.Count}");

        if (towerOptions == null || towerOptions.Length == 0)
        {
            Debug.LogWarning("[BuildUI] OpenForSpot aborted: no tower options");
            return;
        }

        _pendingSpot = spot;
        Debug.Log("[BuildUI] Pending spot assigned");

        if (_previewController != null)
            _previewController.SetSpot(spot);

        _suppressBackdropCloseUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0.01f, backdropCloseGraceSeconds);

        if (_modalRoot != null)
        {
            _modalRoot.gameObject.SetActive(true);
            _modalRoot.SetAsLastSibling();
            Debug.Log("[BuildUI] Modal set active = true");
        }
        else
        {
            Debug.LogError("[BuildUI] OpenForSpot aborted: modal root missing after build");
            return;
        }

        RefreshAffordability();
        Debug.Log("[BuildUI] OpenForSpot success");
    }

    /// <summary>外部调用关闭（无细分来源）。</summary>
    public void Close() => CloseInternal("External");

    void OnBackdropClicked()
    {
        if (Time.unscaledTime < _suppressBackdropCloseUntilUnscaledTime)
        {
            Debug.Log("[BuildUI] Backdrop close ignored (grace period after open)");
            return;
        }
        CloseInternal("Backdrop");
    }

    void CloseInternal(string source)
    {
        Debug.Log($"[BuildUI] Close called — source={source}");

        if (_previewController != null)
            _previewController.Hide();
        _pendingSpot = null;
        if (_modalRoot != null)
            _modalRoot.gameObject.SetActive(false);
    }

    public bool IsOpen => _modalRoot != null && _modalRoot.gameObject.activeSelf;

    private void RefreshAffordability()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            RowWidgets r = _rows[i];
            if (r.Button == null || r.Option == null) continue;

            bool ok = TryEvaluateOption(r.Option, out var evaluation) && evaluation.CanSubmit;
            r.Button.interactable = ok;
            if (r.Group != null)
                r.Group.alpha = ok ? 1f : 0.55f;
            if (r.Label != null)
                r.Label.color = ok ? rowNormalColor : rowDisabledColor;
        }
    }

    private void EnsureUiBuilt()
    {
        if (_uiBuilt)
        {
            Debug.Log("[BuildUI] EnsureUiBuilt skipped (already built)");
            return;
        }

        Debug.Log("[BuildUI] EnsureUiBuilt start");

        _rootCanvas = FindObjectOfType<Canvas>();
        if (_rootCanvas == null)
        {
            Debug.LogError("[BuildUI] EnsureUiBuilt: no Canvas found.");
            return;
        }

        Debug.Log($"[BuildUI] Root canvas found = {_rootCanvas.name}");

        var modal = new GameObject("BuildSelectionModal", typeof(RectTransform), typeof(CanvasRenderer));
        modal.layer = _rootCanvas.gameObject.layer;
        _modalRoot = modal.GetComponent<RectTransform>();
        _modalRoot.SetParent(_rootCanvas.transform, false);
        StretchFull(_modalRoot);
        modal.transform.SetAsLastSibling();

        // Backdrop (closes on click)
        GameObject backdrop = CreateUiObject("Backdrop", _modalRoot);
        StretchFull(backdrop.GetComponent<RectTransform>());
        var backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = backdropColor;
        backdropImage.raycastTarget = true;
        var backdropBtn = backdrop.AddComponent<Button>();
        backdropBtn.targetGraphic = backdropImage;
        backdropBtn.onClick.AddListener(OnBackdropClicked);

        // Panel (blocks raycasts to backdrop)
        _panelGo = CreateUiObject("Panel", _modalRoot);
        RectTransform panelRt = _panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(420f, 360f);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImage = _panelGo.AddComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = true;

        var titleGo = CreateUiObject("Title", panelRt);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(0f, 44f);
        titleRt.anchoredPosition = new Vector2(0f, -8f);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Build Tower";
        titleTmp.fontSize = 22f;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.raycastTarget = false;
        CopyTmpFont(titleTmp);

        var contentGo = CreateUiObject("Content", panelRt);
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.offsetMin = new Vector2(16f, 52f);
        contentRt.offsetMax = new Vector2(-16f, -52f);
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        _rowsParent = contentGo.transform;

        var cancelGo = CreateUiObject("Cancel", panelRt);
        var cancelRt = cancelGo.GetComponent<RectTransform>();
        cancelRt.anchorMin = new Vector2(0.5f, 0f);
        cancelRt.anchorMax = new Vector2(0.5f, 0f);
        cancelRt.pivot = new Vector2(0.5f, 0f);
        cancelRt.sizeDelta = new Vector2(160f, 36f);
        cancelRt.anchoredPosition = new Vector2(0f, 12f);
        var cancelImg = cancelGo.AddComponent<Image>();
        cancelImg.color = new Color(0.25f, 0.27f, 0.32f, 1f);
        var cancelBtn = cancelGo.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelImg;
        cancelBtn.onClick.AddListener(() => CloseInternal("Cancel"));
        GameObject cancelTextGo = CreateUiObject("Text", cancelRt);
        var cancelTmp = cancelTextGo.AddComponent<TextMeshProUGUI>();
        cancelTmp.text = "Cancel";
        cancelTmp.fontSize = 18f;
        cancelTmp.alignment = TextAlignmentOptions.Center;
        cancelTmp.raycastTarget = false;
        StretchFull(cancelTextGo.GetComponent<RectTransform>());
        CopyTmpFont(cancelTmp);

        _modalRoot.gameObject.SetActive(false);
        _uiBuilt = true;
        Debug.Log("[BuildUI] EnsureUiBuilt finished");
    }

    /// <summary>Clears and rebuilds row widgets from current <see cref="towerOptions"/> (call after <see cref="EnsureOptionsPopulated"/>).</summary>
    private void RebuildOptionRows()
    {
        if (_rowsParent == null)
        {
            Debug.LogWarning("[BuildUI] RebuildOptionRows: rows parent null");
            return;
        }
        for (int i = _rowsParent.childCount - 1; i >= 0; i--)
        {
            Transform child = _rowsParent.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
        _rows.Clear();
        BuildRows();
    }

    private void BuildRows()
    {
        _rows.Clear();
        if (_rowsParent == null || towerOptions == null)
        {
            Debug.Log("[BuildUI] BuildRows skipped (no parent or options)");
            return;
        }

        for (int i = 0; i < towerOptions.Length; i++)
        {
            BuildTowerOption opt = towerOptions[i];
            if (opt == null || opt.towerPrefab == null) continue;

            GameObject row = CreateUiObject("Row_" + i, _rowsParent);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 44f);

            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;

            var img = row.AddComponent<Image>();
            img.color = new Color(0.2f, 0.22f, 0.26f, 1f);
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = img;
            var cg = row.AddComponent<CanvasGroup>();

            var labelGo = CreateUiObject("Label", rowRt);
            var labelRt = labelGo.GetComponent<RectTransform>();
            StretchFull(labelRt);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18f;
            tmp.margin = new Vector4(12f, 4f, 12f, 4f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.text = $"{opt.displayName}  -  {opt.cost} gold";
            CopyTmpFont(tmp);
            tmp.raycastTarget = false;

            BuildTowerOption capturedOpt = opt;
            btn.onClick.AddListener(() => OnRowClicked(capturedOpt));

            var rowHover = row.AddComponent<BuildTowerRowHover>();
            rowHover.preview = _previewController;
            rowHover.option = opt;

            _rows.Add(new RowWidgets
            {
                Button = btn,
                Group = cg,
                Label = tmp,
                Option = opt
            });
        }

        Debug.Log($"[BuildUI] BuildRows finished, rowCount={_rows.Count}");
    }

    private void OnRowClicked(BuildTowerOption option)
    {
        string optId = option != null ? option.id : "null";
        Debug.Log($"[BuildUI] OnRowClicked called option={optId}");

        if (_pendingSpot == null)
        {
            Debug.LogWarning("[BuildUI] OnRowClicked aborted: pendingSpot null");
            CloseInternal("RowInvalid");
            return;
        }

        if (option == null)
        {
            Debug.LogWarning("[BuildUI] OnRowClicked aborted: option null");
            CloseInternal("RowInvalid");
            return;
        }

        if (option.towerPrefab == null)
        {
            Debug.LogWarning($"[BuildUI] OnRowClicked aborted: option.towerPrefab null (id={optId})");
            CloseInternal("RowInvalid");
            return;
        }

        if (towerBuilder == null) towerBuilder = FindObjectOfType<TowerBuilder>();
        if (towerBuilder == null)
        {
            Debug.LogWarning("[BuildUI] OnRowClicked aborted: towerBuilder null");
            CloseInternal("NoTowerBuilder");
            return;
        }

        if (!TryEvaluateOption(option, out var evaluation) || !evaluation.CanSubmit)
        {
            Debug.LogWarning($"[BuildUI] OnRowClicked aborted: unified evaluation rejected build (option={optId})");
            RefreshAffordability();
            return;
        }

        Debug.Log($"[BuildUI] Calling TryBuildOnSpot for option={optId}, cost={option.cost}");
        bool ok = towerBuilder.TryBuildOnSpot(_pendingSpot, option.towerPrefab, option.cost);
        if (ok)
        {
            Debug.Log($"[BuildUI] Build success for option={optId}");
            CloseInternal("BuildSuccess");
        }
        else
        {
            Debug.LogWarning($"[BuildUI] OnRowClicked: TryBuildOnSpot returned false (option={optId})");
            RefreshAffordability();
        }
    }

    private bool CanOpenForSpot(BuildSpot spot)
    {
        if (!EnsureTowerBuilderResolved())
            return false;

        var evaluation = towerBuilder.EvaluateBuildRequest(spot, null, 0);
        return evaluation.HasSpot && evaluation.IsTerrainBuildable && evaluation.IsSpotBuildable;
    }

    private bool TryEvaluateOption(BuildTowerOption option, out TowerBuilder.BuildSubmissionEvaluation evaluation)
    {
        if (!EnsureTowerBuilderResolved() || option == null)
        {
            evaluation = default;
            return false;
        }

        evaluation = towerBuilder.EvaluateBuildRequest(
            _pendingSpot,
            option.towerPrefab,
            option.cost
        );
        return true;
    }

    private bool EnsureTowerBuilderResolved()
    {
        if (towerBuilder == null)
            towerBuilder = FindObjectOfType<TowerBuilder>();
        return towerBuilder != null;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CopyTmpFont(TextMeshProUGUI tmp)
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
