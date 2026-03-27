using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom enemy info panel: structured layout (portrait / core stats / type and state).
/// Auto-builds under the first Canvas when references are unset. ASCII-only TMP strings.
/// </summary>
public class EnemyInfoPanelUI : MonoBehaviour
{
    private static readonly Color ColorHp = new Color(0.96f, 0.97f, 0.99f, 1f);
    private static readonly Color ColorAtk = new Color(0.98f, 0.84f, 0.72f, 1f);
    private static readonly Color ColorSpd = new Color(0.74f, 0.9f, 1f, 1f);
    private static readonly Color ColorType = new Color(0.72f, 0.76f, 0.8f, 1f);
    private static readonly Color ColorStateNormal = new Color(0.78f, 0.8f, 0.84f, 1f);
    private static readonly Color ColorStateNightBuff = new Color(1f, 0.9f, 0.38f, 1f);
    private static readonly Color ColorTitle = new Color(0.68f, 0.72f, 0.78f, 1f);
    private static readonly Color ColorPanelBg = new Color(0.05f, 0.065f, 0.09f, 0.93f);

    private static readonly Color PortraitNormal = new Color(0.45f, 0.48f, 0.52f, 1f);
    private static readonly Color PortraitFast = new Color(0.35f, 0.55f, 0.85f, 1f);
    private static readonly Color PortraitTank = new Color(0.52f, 0.26f, 0.24f, 1f);

    private const float PortraitSize = 88f;

    private static Sprite _cachedWhiteSprite;

    [Header("Optional (auto-built when null)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private Image portraitPlaceholder;

    private Enemy _target;

    private void Awake()
    {
        EnsureBuiltUi();
        PolishPanelLayout();
    }

    private void LateUpdate()
    {
        if (_target == null || panelRoot == null || !panelRoot.activeSelf) return;

        SnapPortraitIntoContentRow();

        if (!_target.IsAliveForInfoPanel())
        {
            Hide();
            return;
        }

        RefreshTexts();
    }

    public void Show(Enemy enemy)
    {
        EnsureBuiltUi();
        PolishPanelLayout();
        if (enemy == null || panelRoot == null) return;

        _target = enemy;
        if (!_target.IsAliveForInfoPanel())
        {
            Hide();
            return;
        }

        panelRoot.SetActive(true);
        RefreshTexts();
    }

    public void Hide()
    {
        _target = null;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public bool IsShowing => _target != null && panelRoot != null && panelRoot.activeSelf;

    private void RefreshTexts()
    {
        if (_target == null) return;

        if (titleText != null)
        {
            titleText.text = "Enemy Info";
            titleText.color = ColorTitle;
        }

        if (healthText != null)
        {
            float cur = _target.GetCurrentHealth();
            float finalMax = _target.GetFinalMaxHP();
            float baseMax = _target.GetBaseMaxHP();
            string hpLine = $"HP: {cur:0.#} / {finalMax:0.#}";
            if (_target.HasBuff() && !Mathf.Approximately(finalMax, baseMax))
                hpLine += " (Buffed)";
            healthText.text = hpLine;
            healthText.color = ColorHp;
        }

        if (attackText != null)
        {
            int atkFinal = _target.GetFinalDamage();
            string atkLine = $"ATK: {atkFinal}";
            if (_target.HasBuff() && _target.GetFinalDamage() != _target.GetBaseDamage())
                atkLine += " (Buffed)";
            attackText.text = atkLine;
            attackText.color = ColorAtk;
        }

        if (speedText != null)
        {
            float spdFinal = _target.GetFinalMoveSpeed();
            float spdBase = _target.GetBaseMoveSpeed();
            string spdLine = $"SPD: {spdFinal:0.##}";
            if (_target.HasBuff() && !Mathf.Approximately(spdFinal, spdBase))
                spdLine += " (Buffed)";
            speedText.text = spdLine;
            speedText.color = ColorSpd;
        }

        if (typeText != null)
        {
            typeText.text = $"Type: {_target.GetInfoKindDisplayName()}";
            typeText.color = ColorType;
        }

        if (stateText != null)
        {
            if (_target.HasBuff())
            {
                stateText.text = "State: Night Buff";
                stateText.color = ColorStateNightBuff;
            }
            else
            {
                stateText.text = "State: Normal";
                stateText.color = ColorStateNormal;
            }
        }

        ApplyPortraitTint(_target.GetInfoKind());
        ApplyTypographyHierarchy();
    }

    /// <summary>HP largest; ATK/SPD secondary; Type/State auxiliary. No gameplay impact.</summary>
    private void ApplyTypographyHierarchy()
    {
        const float sizeHp = 22f;
        const float sizeCore = 18f;
        const float sizeMeta = 16f;
        const float sizeTitle = 14f;

        if (titleText != null)
        {
            titleText.fontSize = sizeTitle;
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 0.35f;
        }

        if (healthText != null)
        {
            healthText.fontSize = sizeHp;
            healthText.fontStyle = FontStyles.Bold;
            healthText.lineSpacing = 0f;
        }

        if (attackText != null)
        {
            attackText.fontSize = sizeCore;
            attackText.fontStyle = FontStyles.Normal;
            attackText.lineSpacing = 0f;
        }

        if (speedText != null)
        {
            speedText.fontSize = sizeCore;
            speedText.fontStyle = FontStyles.Normal;
            speedText.lineSpacing = 0f;
        }

        if (typeText != null)
        {
            typeText.fontSize = sizeMeta;
            typeText.fontStyle = FontStyles.Normal;
            typeText.lineSpacing = 0f;
        }

        if (stateText != null)
        {
            stateText.fontSize = sizeMeta;
            stateText.fontStyle = _target != null && _target.HasBuff() ? FontStyles.Bold : FontStyles.Normal;
            stateText.lineSpacing = 0f;
        }
    }

    private void ApplyPortraitTint(Enemy.InfoKind kind)
    {
        if (portraitPlaceholder == null) return;
        portraitPlaceholder.color = kind switch
        {
            Enemy.InfoKind.Fast => PortraitFast,
            Enemy.InfoKind.Tank => PortraitTank,
            _ => PortraitNormal
        };
    }

    private void EnsureBuiltUi()
    {
        Canvas canvas = ResolveHostCanvas();
        if (canvas == null) return;

        RemoveDuplicateEnemyInfoPanelsUnder(canvas);

        if (panelRoot == null)
        {
            Transform existing = canvas.transform.Find("EnemyInfoPanel");
            if (existing != null)
                panelRoot = existing.gameObject;
        }

        MaybeUpgradeLegacyPanel();

        if (PanelStructureComplete())
        {
            PolishPortraitChain();
            return;
        }

        if (panelRoot == null)
        {
            panelRoot = new GameObject("EnemyInfoPanel");
            panelRoot.layer = canvas.gameObject.layer;
            var rt = panelRoot.AddComponent<RectTransform>();
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 20f);
            rt.sizeDelta = new Vector2(880f, 164f);

            var bg = panelRoot.AddComponent<Image>();
            bg.color = ColorPanelBg;

            var rootV = panelRoot.AddComponent<VerticalLayoutGroup>();
            rootV.padding = new RectOffset(22, 22, 18, 20);
            rootV.spacing = 12f;
            rootV.childAlignment = TextAnchor.UpperLeft;
            rootV.childControlHeight = true;
            rootV.childControlWidth = true;
            rootV.childForceExpandHeight = false;
            rootV.childForceExpandWidth = true;
        }
        else
        {
            if (panelRoot.GetComponent<VerticalLayoutGroup>() == null)
                panelRoot.AddComponent<VerticalLayoutGroup>();
        }

        var rootLayout = panelRoot.GetComponent<VerticalLayoutGroup>();
        if (rootLayout != null)
        {
            rootLayout.padding = new RectOffset(22, 22, 18, 20);
            rootLayout.spacing = 12f;
            rootLayout.childAlignment = TextAnchor.UpperLeft;
        }

        if (panelRoot.GetComponent<Outline>() == null)
        {
            var outline = panelRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.4f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        var panelImg = panelRoot.GetComponent<Image>();
        if (panelImg != null)
            panelImg.color = ColorPanelBg;

        if (titleText == null)
        {
            Transform t = panelRoot.transform.Find("EnemyInfoTitle");
            if (t != null)
                titleText = t.GetComponent<TextMeshProUGUI>();
            if (titleText == null)
            {
                titleText = CreateTmp(panelRoot.transform, "EnemyInfoTitle", 14f, ColorTitle);
                var tle = titleText.GetComponent<LayoutElement>();
                if (tle != null)
                {
                    tle.preferredHeight = 20f;
                    tle.minHeight = 20f;
                }
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.MidlineLeft;
                titleText.characterSpacing = 0.35f;
            }
        }

        Transform contentRow = panelRoot.transform.Find("ContentRow");
        if (contentRow == null)
        {
            var rowGo = new GameObject("ContentRow");
            rowGo.layer = panelRoot.layer;
            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.SetParent(panelRoot.transform, false);
            var rowLe = rowGo.AddComponent<LayoutElement>();
            rowLe.preferredHeight = 104f;
            rowLe.minHeight = 96f;
            rowLe.flexibleWidth = 1f;
            var rowH = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowH.padding = new RectOffset(2, 2, 0, 0);
            rowH.spacing = 24f;
            rowH.childAlignment = TextAnchor.MiddleLeft;
            rowH.childForceExpandHeight = false;
            rowH.childForceExpandWidth = false;
            rowH.childControlWidth = true;
            rowH.childControlHeight = true;
            contentRow = rowGo.transform;
        }
        else
        {
            var rowH = contentRow.GetComponent<HorizontalLayoutGroup>();
            if (rowH != null)
            {
                rowH.spacing = 24f;
                rowH.padding = new RectOffset(2, 2, 0, 0);
                rowH.childForceExpandHeight = false;
            }
        }

        if (portraitPlaceholder == null)
        {
            Transform pTr = contentRow.Find("Portrait");
            if (pTr != null)
                portraitPlaceholder = pTr.GetComponent<Image>();
        }

        if (portraitPlaceholder == null)
        {
            Transform deep = FindPortraitDeep(panelRoot.transform);
            if (deep != null)
                portraitPlaceholder = deep.GetComponent<Image>();
        }

        if (portraitPlaceholder == null)
        {
            var pGo = new GameObject("Portrait");
            pGo.layer = panelRoot.layer;
            pGo.transform.SetParent(contentRow, false);
            portraitPlaceholder = pGo.AddComponent<Image>();
            portraitPlaceholder.color = PortraitNormal;
        }

        ApplyPortraitSetup(contentRow);

        Transform statsCol = contentRow.Find("StatsColumn");
        if (statsCol == null)
        {
            var colGo = new GameObject("StatsColumn");
            colGo.layer = panelRoot.layer;
            var crt = colGo.AddComponent<RectTransform>();
            crt.SetParent(contentRow, false);
            var v = colGo.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(2, 8, 4, 4);
            v.spacing = 7f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            var leCol = colGo.AddComponent<LayoutElement>();
            leCol.flexibleWidth = 2.4f;
            leCol.minWidth = 220f;
            statsCol = colGo.transform;

            healthText ??= CreateTmp(statsCol, "HealthLine", 22f, ColorHp);
            attackText ??= CreateTmp(statsCol, "AttackLine", 18f, ColorAtk);
            speedText ??= CreateTmp(statsCol, "SpeedLine", 18f, ColorSpd);
        }
        else
        {
            healthText ??= statsCol.Find("HealthLine")?.GetComponent<TextMeshProUGUI>();
            attackText ??= statsCol.Find("AttackLine")?.GetComponent<TextMeshProUGUI>();
            speedText ??= statsCol.Find("SpeedLine")?.GetComponent<TextMeshProUGUI>();
            if (healthText == null) healthText = CreateTmp(statsCol, "HealthLine", 22f, ColorHp);
            if (attackText == null) attackText = CreateTmp(statsCol, "AttackLine", 18f, ColorAtk);
            if (speedText == null) speedText = CreateTmp(statsCol, "SpeedLine", 18f, ColorSpd);
            ApplyStatLineStyle(healthText, ColorHp, 22f);
            ApplyStatLineStyle(attackText, ColorAtk, 18f);
            ApplyStatLineStyle(speedText, ColorSpd, 18f);
        }

        Transform metaCol = contentRow.Find("MetaColumn");
        if (metaCol == null)
        {
            var mGo = new GameObject("MetaColumn");
            mGo.layer = panelRoot.layer;
            var mrt = mGo.AddComponent<RectTransform>();
            mrt.SetParent(contentRow, false);
            var mv = mGo.AddComponent<VerticalLayoutGroup>();
            mv.padding = new RectOffset(2, 0, 4, 4);
            mv.spacing = 6f;
            mv.childAlignment = TextAnchor.UpperLeft;
            mv.childControlHeight = true;
            mv.childControlWidth = true;
            mv.childForceExpandHeight = false;
            mv.childForceExpandWidth = true;
            var mle = mGo.AddComponent<LayoutElement>();
            mle.flexibleWidth = 1.2f;
            mle.minWidth = 140f;
            metaCol = mGo.transform;

            typeText ??= CreateTmp(metaCol, "TypeLine", 16f, ColorType);
            stateText ??= CreateTmp(metaCol, "StateLine", 16f, ColorStateNormal);
        }
        else
        {
            typeText ??= metaCol.Find("TypeLine")?.GetComponent<TextMeshProUGUI>();
            stateText ??= metaCol.Find("StateLine")?.GetComponent<TextMeshProUGUI>();
            if (typeText == null) typeText = CreateTmp(metaCol, "TypeLine", 16f, ColorType);
            if (stateText == null) stateText = CreateTmp(metaCol, "StateLine", 16f, ColorStateNormal);
            ApplyStatLineStyle(typeText, ColorType, 16f);
            ApplyStatLineStyle(stateText, ColorStateNormal, 16f);
        }

        var pr = panelRoot.GetComponent<RectTransform>();
        if (pr != null)
        {
            pr.sizeDelta = new Vector2(880f, 164f);
            pr.anchoredPosition = new Vector2(0f, 20f);
        }

        PolishPortraitChain();
        panelRoot.SetActive(false);
    }

    /// <summary>Same Canvas as this HUD host so the panel shares screen space with other UI.</summary>
    private Canvas ResolveHostCanvas()
    {
        Canvas c = GetComponentInParent<Canvas>();
        if (c != null) return c;
        return FindObjectOfType<Canvas>();
    }

    /// <summary>Keeps a single runtime EnemyInfoPanel under the host canvas (avoids wrong-canvas duplicates).</summary>
    private void RemoveDuplicateEnemyInfoPanelsUnder(Canvas canvas)
    {
        if (canvas == null) return;

        if (panelRoot != null && panelRoot.transform.parent == canvas.transform)
        {
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                Transform ch = canvas.transform.GetChild(i);
                if (ch.name != "EnemyInfoPanel" || ch.gameObject == panelRoot) continue;
                Destroy(ch.gameObject);
            }

            return;
        }

        Transform first = null;
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            Transform ch = canvas.transform.GetChild(i);
            if (ch.name == "EnemyInfoPanel")
            {
                first = ch;
                break;
            }
        }

        if (first == null) return;

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = canvas.transform.GetChild(i);
            if (ch.name != "EnemyInfoPanel" || ch == first) continue;
            Destroy(ch.gameObject);
        }
    }

    private void PolishPortraitChain()
    {
        if (panelRoot == null) return;
        Transform contentRow = panelRoot.transform.Find("ContentRow");
        if (contentRow == null) return;

        ApplyPortraitSetup(contentRow);

        if (portraitPlaceholder == null) return;

        RequestLayoutRebuild(panelRoot.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>Lightweight per-frame fix after layout so Portrait stays inside ContentRow (no full rebuild).</summary>
    private void SnapPortraitIntoContentRow()
    {
        if (panelRoot == null) return;
        Transform contentRow = panelRoot.transform.Find("ContentRow");
        if (contentRow == null) return;

        ApplyPortraitSetup(contentRow);
    }

    /// <summary>Resolves Image reference, reparents to ContentRow, then applies layout + rect + sprite (all paths).</summary>
    private void ApplyPortraitSetup(Transform contentRow)
    {
        if (panelRoot == null || contentRow == null) return;

        ResolvePortraitReference(contentRow);
        if (portraitPlaceholder == null) return;

        EnsurePortraitRenders(portraitPlaceholder);
        ApplyPortraitLayoutElement(portraitPlaceholder.gameObject);
        FixPortraitParentAndOrder(contentRow);
        NormalizePortraitRectTransform(portraitPlaceholder.rectTransform);
    }

    private void ResolvePortraitReference(Transform contentRow)
    {
        if (panelRoot == null || contentRow == null) return;

        if (portraitPlaceholder == null)
        {
            Transform direct = contentRow.Find("Portrait");
            if (direct != null)
                portraitPlaceholder = direct.GetComponent<Image>();
        }

        if (portraitPlaceholder == null)
        {
            Transform deep = FindPortraitDeep(panelRoot.transform);
            if (deep != null)
                portraitPlaceholder = deep.GetComponent<Image>();
        }
    }

    private static Transform FindPortraitDeep(Transform root)
    {
        if (root == null) return null;
        if (root.name == "Portrait") return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform f = FindPortraitDeep(root.GetChild(i));
            if (f != null) return f;
        }

        return null;
    }

    private void FixPortraitParentAndOrder(Transform contentRow)
    {
        if (portraitPlaceholder == null || contentRow == null) return;
        portraitPlaceholder.gameObject.name = "Portrait";
        if (portraitPlaceholder.transform.parent != contentRow)
            portraitPlaceholder.transform.SetParent(contentRow, false);
        portraitPlaceholder.transform.SetAsFirstSibling();
    }

    private static void RequestLayoutRebuild(RectTransform root)
    {
        if (root == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    /// <summary>Syncs padding/spacing/sizes for existing auto-built panels (no new GameObjects).</summary>
    private void PolishPanelLayout()
    {
        if (panelRoot == null) return;

        var rt = panelRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(880f, 164f);
            rt.anchoredPosition = new Vector2(0f, 20f);
        }

        var panelBg = panelRoot.GetComponent<Image>();
        if (panelBg != null)
            panelBg.color = ColorPanelBg;

        var rootV = panelRoot.GetComponent<VerticalLayoutGroup>();
        if (rootV != null)
        {
            rootV.padding = new RectOffset(22, 22, 18, 20);
            rootV.spacing = 12f;
            rootV.childAlignment = TextAnchor.UpperLeft;
        }

        if (panelRoot.GetComponent<Outline>() == null)
        {
            var outline = panelRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.4f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        Transform contentRow = panelRoot.transform.Find("ContentRow");
        if (contentRow != null)
        {
            var rowH = contentRow.GetComponent<HorizontalLayoutGroup>();
            if (rowH != null)
            {
                rowH.padding = new RectOffset(2, 2, 0, 0);
                rowH.spacing = 24f;
                rowH.childAlignment = TextAnchor.MiddleLeft;
                rowH.childForceExpandHeight = false;
            }

            var rowLe = contentRow.GetComponent<LayoutElement>();
            if (rowLe != null)
            {
                rowLe.preferredHeight = 104f;
                rowLe.minHeight = 96f;
            }

            Transform statsCol = contentRow.Find("StatsColumn");
            if (statsCol != null)
            {
                var v = statsCol.GetComponent<VerticalLayoutGroup>();
                if (v != null)
                {
                    v.spacing = 7f;
                    v.padding = new RectOffset(2, 8, 4, 4);
                    v.childAlignment = TextAnchor.UpperLeft;
                }
            }

            Transform metaCol = contentRow.Find("MetaColumn");
            if (metaCol != null)
            {
                var mv = metaCol.GetComponent<VerticalLayoutGroup>();
                if (mv != null)
                {
                    mv.spacing = 6f;
                    mv.padding = new RectOffset(2, 0, 4, 4);
                    mv.childAlignment = TextAnchor.UpperLeft;
                }
            }
        }

        Transform titleTr = panelRoot.transform.Find("EnemyInfoTitle");
        if (titleTr != null)
        {
            var tle = titleTr.GetComponent<LayoutElement>();
            if (tle != null)
            {
                tle.preferredHeight = 20f;
                tle.minHeight = 20f;
            }
        }

        PolishPortraitChain();
    }

    private bool PanelStructureComplete()
    {
        if (panelRoot == null || portraitPlaceholder == null) return false;
        if (panelRoot.transform.Find("ContentRow") == null) return false;
        return titleText != null && healthText != null && attackText != null && speedText != null &&
               typeText != null && stateText != null;
    }

    private void MaybeUpgradeLegacyPanel()
    {
        if (panelRoot == null) return;
        if (panelRoot.name != "EnemyInfoPanel") return;
        if (panelRoot.transform.Find("ContentRow") != null) return;

        portraitPlaceholder = null;
        titleText = null;
        healthText = null;
        attackText = null;
        speedText = null;
        typeText = null;
        stateText = null;

        for (int i = panelRoot.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(panelRoot.transform.GetChild(i).gameObject);

        var oldH = panelRoot.GetComponent<HorizontalLayoutGroup>();
        if (oldH != null)
            DestroyImmediate(oldH);
    }

    private static void ApplyStatLineStyle(TextMeshProUGUI tmp, Color c, float fontSize = 20f)
    {
        if (tmp == null) return;
        tmp.fontSize = fontSize;
        tmp.lineSpacing = 0f;
        tmp.color = c;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static TextMeshProUGUI CreateTmp(Transform parent, string name, float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30f;
        le.minHeight = 24f;
        le.flexibleWidth = 1f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = fontSize;
        tmp.lineSpacing = 0f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }

    /// <summary>Unity UI Image draws nothing when sprite is null; use a 1x1 white sprite so color shows.</summary>
    private static void EnsurePortraitRenders(Image img)
    {
        if (img == null) return;
        if (img.sprite == null)
            img.sprite = GetOrCreateWhiteSprite();
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget = false;
    }

    private static Sprite GetOrCreateWhiteSprite()
    {
        if (_cachedWhiteSprite != null) return _cachedWhiteSprite;
        Texture2D t = Texture2D.whiteTexture;
        _cachedWhiteSprite = Sprite.Create(
            t,
            new Rect(0f, 0f, t.width, t.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return _cachedWhiteSprite;
    }

    private static void ApplyPortraitLayoutElement(GameObject portraitGo)
    {
        if (portraitGo == null) return;
        var le = portraitGo.GetComponent<LayoutElement>();
        if (le == null) le = portraitGo.AddComponent<LayoutElement>();
        le.preferredWidth = PortraitSize;
        le.preferredHeight = PortraitSize;
        le.minWidth = PortraitSize;
        le.minHeight = PortraitSize;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        var rect = portraitGo.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = new Vector2(PortraitSize, PortraitSize);
    }

    /// <summary>Forces a centered, fixed-size cell so old stretch anchors / offsets cannot push Portrait off-panel.</summary>
    private static void NormalizePortraitRectTransform(RectTransform rt)
    {
        if (rt == null) return;

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        // Break stretch / inherited anchor presets, then use a standard layout child preset.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // Fixed 88×88 cell (with pivot 0.5/0.5). Do not set offsetMin/offsetMax to zero here: for unified
        // anchors that collapses the rect; switching away from stretch is enough, sizeDelta defines size.
        rt.sizeDelta = new Vector2(PortraitSize, PortraitSize);
    }
}
