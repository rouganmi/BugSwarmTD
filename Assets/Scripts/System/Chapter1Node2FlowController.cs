using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class Chapter1Node2FlowController : MonoBehaviour
{
    private const string MainSceneName = "Main";

    private enum Node2State
    {
        IntroShowing,
        Running,
        DefeatShowing
    }

    private Node2State _state;
    private RuntimeNodeContext _ctx;
    private Canvas _canvas;
    private BaseHealth _baseHealth;

    private GameObject _introRoot;
    private GameObject _resultRoot;
    private TMP_Text _resultTitle;
    private TMP_Text _resultBody;
    private Button _resultRetryButton;
    private Button _resultCloseButton;

    private float _nextUpdateLogTime;
    private string _lastUpdateSnapshot;
    private bool _isInvalidForReuse;

    private static bool IsTargetNode2(NodeDefinition def) =>
        def != null &&
        string.Equals(def.chapterId, "Chapter1", System.StringComparison.Ordinal) &&
        string.Equals(def.nodeId, "Node2", System.StringComparison.Ordinal);

    private void Log(string msg)
    {
        Debug.Log("[Node2FlowCheck] " + msg, this);
    }

    private string ContextKey()
    {
        var def = _ctx != null ? _ctx.CurrentDefinition : null;
        if (def == null) return "<null>";
        return $"{def.chapterId}:{def.nodeId}";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePresent()
    {
        var s = SceneManager.GetActiveScene();
        if (!s.IsValid() || !string.Equals(s.name, MainSceneName, System.StringComparison.Ordinal))
            return;

        var ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        var def = ctx != null ? ctx.CurrentDefinition : null;
        if (!IsTargetNode2(def))
            return;

        // DDOL-safe de-dupe: avoid duplicate flows in container scene.
        var existingAll = Resources.FindObjectsOfTypeAll<Chapter1Node2FlowController>();
        if (existingAll != null)
        {
            for (int i = 0; i < existingAll.Length; i++)
            {
                var e = existingAll[i];
                if (e == null) continue;
                if (e._isInvalidForReuse)
                {
                    Debug.Log("[Node2FlowCheck] EnsurePresent.ignoreExisting invalidForReuse");
                    continue;
                }
                var go = e.gameObject;
                if (go == null) continue;
                var sc = go.scene;
                if (!sc.IsValid()) continue;
                if (string.Equals(sc.name, "DontDestroyOnLoad", System.StringComparison.Ordinal) || sc.isLoaded)
                    return;
            }
        }

        var flowGo = new GameObject("Chapter1_Node2_Flow");
        flowGo.hideFlags = HideFlags.DontSave;
        Object.DontDestroyOnLoad(flowGo);
        flowGo.AddComponent<Chapter1Node2FlowController>();
    }

    /// <summary>
    /// Safe public entry: re-check whether the Node2 flow should exist for the current Context/scene.
    /// Intended for cases where Context is explicitly set after the first auto-check.
    /// </summary>
    public static void EnsurePresentForCurrentContext()
    {
        EnsurePresent();
    }

    private void Awake()
    {
        Log($"Awake.enter state={_state} activeScene={SceneManager.GetActiveScene().name} selfScene={gameObject.scene.name}");
        var s = SceneManager.GetActiveScene();
        if (!s.IsValid() || !string.Equals(s.name, MainSceneName, System.StringComparison.Ordinal))
        {
            Log("Awake.destroy (not Main scene)");
            Destroy(gameObject);
            return;
        }

        _ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        if (_ctx == null || !IsTargetNode2(_ctx.CurrentDefinition))
        {
            Log($"Awake.destroy (ctx missing or not Node2) ctx={(_ctx==null ? "<null>" : ContextKey())}");
            Destroy(gameObject);
            return;
        }

        _state = Node2State.IntroShowing;
        Log($"Awake.ok state={_state} ctx={ContextKey()}");
    }

    private void Start()
    {
        _canvas = FindObjectOfType<Canvas>();
        _baseHealth = FindObjectOfType<BaseHealth>();
        BuildUiIfNeeded();
        Log($"Start.afterBuildUi canvas={(_canvas!=null)} baseHealth={(_baseHealth!=null)} ctx={ContextKey()}");
        ShowIntro();
    }

    private void Update()
    {
        // Throttled state snapshot log (max ~1/sec unless state changes).
        if (Time.unscaledTime >= _nextUpdateLogTime)
        {
            int hp = _baseHealth != null ? _baseHealth.GetCurrentHealth() : int.MinValue;
            string snap = $"state={_state} ctx={ContextKey()} baseHealthNull={(_baseHealth==null)} hp={(hp==int.MinValue ? "<n/a>" : hp.ToString())} introActive={(_introRoot!=null && _introRoot.activeSelf)} resultActive={(_resultRoot!=null && _resultRoot.activeSelf)}";
            if (snap != _lastUpdateSnapshot)
            {
                _lastUpdateSnapshot = snap;
                Log("Update.snapshot " + snap);
            }
            _nextUpdateLogTime = Time.unscaledTime + 1.0f;
        }

        if (_state != Node2State.Running)
        {
            // Keep this log very light: only when throttled snapshot changes above.
            return;
        }

        if (_ctx == null || !IsTargetNode2(_ctx.CurrentDefinition))
        {
            Log($"Update.return (ctx not Node2) ctx={(_ctx==null ? "<null>" : ContextKey())}");
            return;
        }

        if (_baseHealth == null)
            _baseHealth = FindObjectOfType<BaseHealth>();

        if (_baseHealth == null)
        {
            Log("Update.return (baseHealth null)");
            return;
        }

        if (_baseHealth != null && _baseHealth.GetCurrentHealth() <= 0)
        {
            Log($"Update.willShowDefeat hp={_baseHealth.GetCurrentHealth()}");
            ShowDefeatResult();
        }
    }

    private void BuildUiIfNeeded()
    {
        if (_canvas == null)
            _canvas = FindObjectOfType<Canvas>();
        if (_canvas == null)
            return;

        if (_introRoot == null)
            _introRoot = BuildIntroPanel(_canvas.transform);
        if (_resultRoot == null)
            _resultRoot = BuildResultPanel(_canvas.transform);

        Log($"BuildUiIfNeeded.done introBuilt={(_introRoot!=null)} resultBuilt={(_resultRoot!=null)}");
    }

    private void ShowIntro()
    {
        if (_introRoot != null)
            _introRoot.SetActive(true);

        // Node2 intro is the combat gate: pause until Start is pressed.
        Time.timeScale = 0f;
    }

    private void StartRunning()
    {
        Log($"StartRunning.enter prevState={_state} ctx={ContextKey()}");
        _state = Node2State.Running;
        Log($"StartRunning.stateChanged state={_state}");
        if (_introRoot != null)
            _introRoot.SetActive(false);
        if (_resultRoot != null)
            _resultRoot.SetActive(false);

        // Open the combat gate.
        Time.timeScale = 1f;
    }

    private void ShowDefeatResult()
    {
        Log($"ShowDefeatResult.enter state={_state} ctx={ContextKey()}");
        if (_state == Node2State.DefeatShowing)
            return;

        _state = Node2State.DefeatShowing;
        BuildUiIfNeeded();

        if (_introRoot != null)
            _introRoot.SetActive(false);
        if (_resultRoot != null)
            _resultRoot.SetActive(true);
        Log($"ShowDefeatResult.show resultActive={(_resultRoot!=null && _resultRoot.activeSelf)}");

        if (_resultTitle != null) _resultTitle.text = "Mission Failed";
        if (_resultBody != null) _resultBody.text = "Try again.";

        if (_resultRetryButton != null)
        {
            _resultRetryButton.onClick.RemoveAllListeners();
            _resultRetryButton.onClick.AddListener(RetryToMain);
        }
        if (_resultCloseButton != null)
        {
            _resultCloseButton.onClick.RemoveAllListeners();
            _resultCloseButton.onClick.AddListener(CloseResult);
        }
    }

    private void RetryToMain()
    {
        Log("RetryToMain.enter");
        _isInvalidForReuse = true;
        Log("RetryToMain.marked invalidForReuse");
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }

    private void CloseResult()
    {
        Log("CloseResult.enter");
        if (_resultRoot != null)
            _resultRoot.SetActive(false);
    }

    private GameObject BuildIntroPanel(Transform parent)
    {
        var root = new GameObject("Chapter1_Node2_IntroPanel", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root.transform, false);
        var dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(620f, 260f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        string titleText = _ctx != null && _ctx.CurrentDefinition != null ? _ctx.CurrentDefinition.introTitle : "Chapter1_Node2";
        string bodyText = _ctx != null && _ctx.CurrentDefinition != null ? _ctx.CurrentDefinition.introBody : "Node2 ready (placeholder).";

        var title = BuildText(card.transform, "Title", titleText, 32, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(580f, 46f));

        var body = BuildText(card.transform, "Body", bodyText, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        body.enableWordWrapping = true;
        SetAnchored(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(580f, 130f));

        var startBtn = BuildButton(card.transform, "StartButton", "Start");
        SetAnchored(startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(220f, 44f));
        startBtn.onClick.AddListener(StartRunning);
        Log("Intro.StartButton.bound -> StartRunning()");

        root.SetActive(false);
        return root;
    }

    private GameObject BuildResultPanel(Transform parent)
    {
        var root = new GameObject("Chapter1_Node2_ResultPanel", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root.transform, false);
        var dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(620f, 240f);
        card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        _resultTitle = BuildText(card.transform, "Title", "Mission Failed", 32, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchored(_resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(580f, 46f));

        _resultBody = BuildText(card.transform, "Body", "Try again.", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _resultBody.enableWordWrapping = true;
        SetAnchored(_resultBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(580f, 110f));

        _resultRetryButton = BuildButton(card.transform, "RetryButton", "Retry");
        SetAnchored(_resultRetryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 22f), new Vector2(240f, 44f));

        _resultCloseButton = BuildButton(card.transform, "CloseButton", "Close");
        SetAnchored(_resultCloseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 22f), new Vector2(240f, 44f));

        root.SetActive(false);
        return root;
    }

    private static TextMeshProUGUI BuildText(Transform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            t.font = TMP_Settings.defaultFontAsset;
        t.text = text;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.alignment = align;
        t.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        t.raycastTarget = false;
        return t;
    }

    private static Button BuildButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var txt = BuildText(go.transform, "Label", label, 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchored(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return btn;
    }

    private static void SetAnchored(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        if (min == max)
            rt.sizeDelta = size;
        else
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

