using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TowerMenuPanelPresenter : MonoBehaviour
{
    [Header("Input (existing text from TowerMenu.infoText)")]
    [SerializeField] private TextMeshProUGUI sourceInfoText;

    [Header("Generated")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI warningText;

    [Header("Layout")]
    [SerializeField] private Vector4 padding = new Vector4(22, 22, 18, 20); // L,R,T,B
    [SerializeField] private float spacing = 10f;

    [Header("Upgrade feedback")]
    [SerializeField] private float panelPunchScale = 1.1f;
    [SerializeField] private float panelPunchUpTime = 0.07f;
    [SerializeField] private float panelPunchDownTime = 0.10f;
    [SerializeField] private float costPunchScale = 1.12f;
    [SerializeField] private float flashAlpha = 0.5f;
    [SerializeField] private float flashTime = 0.06f;

    private string _lastSource;
    private Vector3 _panelBaseScale;
    private Vector3 _costBaseScale;
    private Coroutine _feedbackRoutine;

    private void Awake()
    {
        if (sourceInfoText == null)
        {
            sourceInfoText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        EnsureLayout();
        EnsureTexts();
        ApplyStyle();
        CacheBaseScales();
        RefreshFromSource(force: true);
    }

    private void LateUpdate()
    {
        RefreshFromSource(force: false);
    }

    private void EnsureLayout()
    {
        var img = GetComponent<Image>();
        if (img != null)
        {
            var c = img.color;
            c.a = 0.85f;
            img.color = c;
        }

        var vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.padding = new RectOffset(
            Mathf.RoundToInt(padding.x),
            Mathf.RoundToInt(padding.y),
            Mathf.RoundToInt(padding.z),
            Mathf.RoundToInt(padding.w)
        );
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperLeft; // stats read as a block; buttons still full-width
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ConfigureButtonsForLayout();
        ConfigureTextLayout();
    }

    private void ConfigureTextLayout()
    {
        ConfigureTextLayoutElement("TitleText", preferredHeight: 38f);
        ConfigureTextLayoutElement("StatsText", preferredHeight: 84f);
        ConfigureTextLayoutElement("CostText", preferredHeight: 56f);
        ConfigureTextLayoutElement("WarningText", preferredHeight: 22f);
    }

    private void ConfigureTextLayoutElement(string childName, float preferredHeight)
    {
        var t = transform.Find(childName);
        if (t == null) return;

        var le = t.GetComponent<LayoutElement>();
        if (le == null) le = t.gameObject.AddComponent<LayoutElement>();

        le.minHeight = preferredHeight;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight = 0f;
    }

    private void ConfigureButtonsForLayout()
    {
        // Make Upgrade/Sell buttons larger and centered without changing their logic.
        ConfigureButtonLayoutElement("UpgradeButton", preferredWidth: 240f, preferredHeight: 52f);
        ConfigureButtonLayoutElement("SellButton", preferredWidth: 240f, preferredHeight: 44f);
    }

    private void ConfigureButtonLayoutElement(string childName, float preferredWidth, float preferredHeight)
    {
        var t = transform.Find(childName);
        if (t == null) return;

        var le = t.GetComponent<LayoutElement>();
        if (le == null) le = t.gameObject.AddComponent<LayoutElement>();

        le.minWidth = preferredWidth;
        le.preferredWidth = preferredWidth;
        le.flexibleWidth = 0f;

        le.minHeight = preferredHeight;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight = 0f;
    }

    private void EnsureTexts()
    {
        // Keep the existing sourceInfoText object in hierarchy, but stop using it for rendering.
        if (sourceInfoText != null)
        {
            sourceInfoText.gameObject.SetActive(false);
        }

        titleText = titleText != null ? titleText : FindOrCreate("TitleText");
        statsText = statsText != null ? statsText : FindOrCreate("StatsText");
        costText = costText != null ? costText : FindOrCreate("CostText");
        warningText = warningText != null ? warningText : FindOrCreate("WarningText");
    }

    private void CacheBaseScales()
    {
        _panelBaseScale = transform.localScale;
        if (costText != null) _costBaseScale = costText.transform.localScale;
    }

    public void PlayUpgradeFeedback()
    {
        if (!isActiveAndEnabled) return;

        if (_feedbackRoutine != null)
        {
            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
        }

        CacheBaseScales();
        _feedbackRoutine = StartCoroutine(UpgradeFeedbackRoutine());
    }

    private IEnumerator UpgradeFeedbackRoutine()
    {
        // Panel punch
        yield return PunchScale(transform, _panelBaseScale, Mathf.Max(1f, panelPunchScale), panelPunchUpTime, panelPunchDownTime);

        // Cost punch (in parallel-ish right after)
        if (costText != null)
        {
            StartCoroutine(PunchScale(costText.transform, _costBaseScale, Mathf.Max(1f, costPunchScale), 0.06f, 0.10f));
        }

        // Text flash alpha
        if (titleText != null) StartCoroutine(FlashAlpha(titleText, flashAlpha, flashTime));
        if (statsText != null) StartCoroutine(FlashAlpha(statsText, flashAlpha, flashTime));
        if (costText != null) StartCoroutine(FlashAlpha(costText, flashAlpha, flashTime));

        _feedbackRoutine = null;
    }

    private IEnumerator PunchScale(Transform t, Vector3 baseScale, float peakScale, float upTime, float downTime)
    {
        if (t == null) yield break;
        float up = Mathf.Max(0.001f, upTime);
        float down = Mathf.Max(0.001f, downTime);

        Vector3 peak = baseScale * peakScale;

        float time = 0f;
        while (time < up)
        {
            time += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(time / up);
            t.localScale = Vector3.LerpUnclamped(baseScale, peak, EaseOutCubic(a));
            yield return null;
        }

        time = 0f;
        while (time < down)
        {
            time += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(time / down);
            t.localScale = Vector3.LerpUnclamped(peak, baseScale, EaseInCubic(a));
            yield return null;
        }

        t.localScale = baseScale;
    }

    private IEnumerator FlashAlpha(TextMeshProUGUI tmp, float toAlpha, float t)
    {
        if (tmp == null) yield break;
        float duration = Mathf.Max(0.001f, t);

        Color c = tmp.color;
        float fromA = c.a;
        float midA = Mathf.Clamp01(toAlpha);

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(time / duration);
            c.a = Mathf.LerpUnclamped(fromA, midA, a);
            tmp.color = c;
            yield return null;
        }

        time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(time / duration);
            c.a = Mathf.LerpUnclamped(midA, fromA, a);
            tmp.color = c;
            yield return null;
        }

        c.a = fromA;
        tmp.color = c;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;

    private TextMeshProUGUI FindOrCreate(string name)
    {
        var t = transform.Find(name);
        if (t != null)
        {
            var existing = t.GetComponent<TextMeshProUGUI>();
            if (existing != null) return existing;
        }

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();

        // Stretch full width for consistent left alignment.
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, 0);

        return tmp;
    }

    private void ApplyStyle()
    {
        if (titleText != null)
        {
            titleText.fontSize = 26;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.enableWordWrapping = false;
            titleText.characterSpacing = 0.2f;
        }

        if (statsText != null)
        {
            statsText.fontSize = 16;
            statsText.fontStyle = FontStyles.Normal;
            statsText.lineSpacing = 2f;
            statsText.alignment = TextAlignmentOptions.Left;
            statsText.enableWordWrapping = true;
            statsText.overflowMode = TextOverflowModes.Overflow;
        }

        if (costText != null)
        {
            costText.fontSize = 16;
            costText.fontStyle = FontStyles.Bold;
            costText.lineSpacing = 4f;
            costText.alignment = TextAlignmentOptions.Left;
            costText.color = new Color(0.96f, 0.82f, 0.2f, 1f);
            costText.enableWordWrapping = true;
            costText.overflowMode = TextOverflowModes.Overflow;
        }

        if (warningText != null)
        {
            warningText.fontSize = 14;
            warningText.fontStyle = FontStyles.Bold;
            warningText.alignment = TextAlignmentOptions.Left;
            warningText.color = new Color(1f, 0.35f, 0.35f, 1f);
            warningText.text = string.Empty;
            warningText.gameObject.SetActive(false);
        }
    }

    /// <summary>Max level (gold warning off) vs insufficient gold while upgrades remain.</summary>
    public void SetUpgradeState(bool atMaxLevel, bool notEnoughGold)
    {
        if (warningText == null) return;

        if (atMaxLevel)
        {
            warningText.gameObject.SetActive(true);
            warningText.text = "Max level";
            warningText.color = new Color(1f, 0.88f, 0.4f, 1f);
            return;
        }

        warningText.gameObject.SetActive(notEnoughGold);
        warningText.text = notEnoughGold ? "Not enough gold" : string.Empty;
        warningText.color = new Color(1f, 0.35f, 0.35f, 1f);
    }

    public void SetNotEnoughGold(bool value)
    {
        SetUpgradeState(false, value);
    }

    private void RefreshFromSource(bool force)
    {
        if (sourceInfoText == null) return;

        string src = sourceInfoText.text ?? string.Empty;
        if (!force && src == _lastSource) return;
        _lastSource = src;

        // Current TowerMenu format:
        // Tower Lv.X
        //
        // Damage: a -> b
        // Range: c -> d
        //
        // Upgrade Cost: N
        // Sell Value: M
        string[] lines = src.Split('\n');

        string title = "Tower";
        var stats = new System.Collections.Generic.List<string>(8);
        var costs = new System.Collections.Generic.List<string>(4);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = (lines[i] ?? string.Empty).Trim();
            if (i == 0) title = string.IsNullOrEmpty(line) ? title : line;

            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("Upgrade Cost") || line.StartsWith("Sell Value"))
            {
                costs.Add(ColorizeCostLine(line));
            }
            else if (i != 0)
            {
                // treat remaining non-title lines as stats
                stats.Add(ColorizeStatLine(line));
            }
        }

        if (titleText != null) titleText.text = title;
        if (statsText != null) statsText.text = string.Join("\n", stats);
        if (costText != null) costText.text = string.Join("\n", costs);
    }

    private static string ColorizeStatLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return string.Empty;

        // Make Damage line more prominent.
        if (line.StartsWith("Damage:", System.StringComparison.OrdinalIgnoreCase))
            return "<color=#E86A6A>" + line + "</color>";

        return line;
    }

    private static string ColorizeCostLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return string.Empty;

        if (line.StartsWith("Upgrade Cost", System.StringComparison.OrdinalIgnoreCase))
            return "<color=#E65050>" + line + "</color>";

        if (line.StartsWith("Sell Value", System.StringComparison.OrdinalIgnoreCase))
            return "<color=#1A1A1A>" + line + "</color>";

        return line;
    }
}

