using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class Chapter1Node1FlowController : MonoBehaviour
{
    public enum NodeFlowState
    {
        NotStarted,
        IntroShowing,
        Playing,
        Victory,
        Defeat
    }

    private const string PrototypeSceneName = "Chapter1_Node1_Prototype";

    [Header("Optional override (otherwise defaults are used)")]
    [SerializeField] private ChapterNodeMetadata metadata;

    private NodeFlowState _state = NodeFlowState.NotStarted;
    private Canvas _canvas;
    private BaseHealth _baseHealth;
    private EnemySpawner _enemySpawner;
    private RuntimeNodeContext _ctx;
    private NodeDefinition _def;

    private GameObject _introRoot;
    private GameObject _resultRoot;
    private TMP_Text _resultTitle;
    private TMP_Text _resultBody;
    private Button _resultPrimaryButton;
    private TMP_Text _resultPrimaryLabel;
    private Button _resultSecondaryButton;
    private TMP_Text _resultSecondaryLabel;
    private bool _isInvalidForReuse;

    private static bool IsTargetNode1(NodeDefinition def) =>
        def != null &&
        string.Equals(def.chapterId, "Chapter1", System.StringComparison.Ordinal) &&
        string.Equals(def.nodeId, "Node1", System.StringComparison.Ordinal);

    private bool IsTargetSceneFallback(string sceneName, out NodeDefinition def)
    {
        def = null;
        if (string.Equals(sceneName, PrototypeSceneName, System.StringComparison.Ordinal))
        {
            // Compatibility: Prototype scene implies Node1.
            return true;
        }

        if (NodeRegistry.TryGetByScene(sceneName, out var d) && IsTargetNode1(d))
        {
            def = d;
            return true;
        }

        return false;
    }

    private bool TryGetTargetNode1FromContext(out RuntimeNodeContext ctx, out NodeDefinition def)
    {
        ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        def = ctx != null ? ctx.CurrentDefinition : null;
        return IsTargetNode1(def);
    }

    private static void FlowDupLog(string where, Chapter1Node1FlowController self = null)
    {
        string activeScene = SceneManager.GetActiveScene().IsValid() ? SceneManager.GetActiveScene().name : "<invalid>";
        string selfName = self != null ? self.gameObject.name : "<static>";
        int selfId = self != null ? self.GetInstanceID() : 0;
        string selfScene = self != null ? (self.gameObject.scene.IsValid() ? self.gameObject.scene.name : "<invalid>") : "<n/a>";

        var first = Object.FindObjectOfType<Chapter1Node1FlowController>();
        string firstStr = first == null ? "null" : $"{first.gameObject.name}#{first.GetInstanceID()} scene={first.gameObject.scene.name}";

        var loaded = Object.FindObjectsOfType<Chapter1Node1FlowController>();
        int loadedCount = loaded != null ? loaded.Length : 0;

        var all = Resources.FindObjectsOfTypeAll<Chapter1Node1FlowController>();
        int allCount = all != null ? all.Length : 0;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
        sb.Append("[FlowDupCheck] ")
          .Append(where)
          .Append(" activeScene=").Append(activeScene)
          .Append(" self=").Append(selfName).Append("#").Append(selfId)
          .Append(" selfScene=").Append(selfScene)
          .Append(" FindObjectOfType=").Append(firstStr)
          .Append(" FindObjectsOfType.Count=").Append(loadedCount)
          .Append(" FindObjectsOfTypeAll.Count=").Append(allCount);

        if (allCount > 0)
        {
            sb.Append(" all=[");
            for (int i = 0; i < allCount; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (i > 0) sb.Append(" | ");
                string sc = c.gameObject.scene.IsValid() ? c.gameObject.scene.name : "<invalid>";
                sb.Append(c.gameObject.name).Append("#").Append(c.GetInstanceID()).Append(" scene=").Append(sc);
            }
            sb.Append("]");
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Safe public entry: re-check whether the Node1 flow should exist for the current Context/scene.
    /// Intended for cases where Context is explicitly set after the first auto-check.
    /// </summary>
    public static void EnsurePresentForCurrentContext()
    {
        FlowDupLog("EnsurePresentForCurrentContext.enter");
        EnsurePresent();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePresent()
    {
        FlowDupLog("EnsurePresent.enter");
        var s = SceneManager.GetActiveScene();
        if (!s.IsValid())
            return;

        // Primary: explicit runtime truth source (Context).
        var ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        var ctxDef = ctx != null ? ctx.CurrentDefinition : null;
        bool ctxIsNode1 = ctxDef != null &&
                          string.Equals(ctxDef.chapterId, "Chapter1", System.StringComparison.Ordinal) &&
                          string.Equals(ctxDef.nodeId, "Node1", System.StringComparison.Ordinal);

        // Fallback: legacy scene-name based resolution (only when Context is missing or empty).
        if (!ctxIsNode1)
        {
            if (ctx != null && ctxDef != null)
                return;

            NodeRegistry.EnsureInitialized();
            bool legacyOk = string.Equals(s.name, PrototypeSceneName, System.StringComparison.Ordinal) ||
                            (NodeRegistry.TryGetByScene(s.name, out var legacyDef) &&
                             legacyDef != null &&
                             string.Equals(legacyDef.chapterId, "Chapter1", System.StringComparison.Ordinal) &&
                             string.Equals(legacyDef.nodeId, "Node1", System.StringComparison.Ordinal));
            if (!legacyOk)
                return;
        }

        FlowDupLog("EnsurePresent.preFindExisting");
        // DDOL-safe de-dupe: FindObjectOfType/FindObjectsOfType may not see DontDestroyOnLoad instances in this project chain.
        // Use Resources.FindObjectsOfTypeAll to cover DDOL and prevent creating duplicate flows after Retry.
        var existingAll = Resources.FindObjectsOfTypeAll<Chapter1Node1FlowController>();
        if (existingAll != null)
        {
            for (int i = 0; i < existingAll.Length; i++)
            {
                var e = existingAll[i];
                if (e == null) continue;
                if (e._isInvalidForReuse)
                {
                    Debug.Log("[FlowDupCheck] EnsurePresent.ignoreExisting invalidForReuse");
                    continue;
                }
                var existingGo = e.gameObject;
                if (existingGo == null) continue;
                var existingScene = existingGo.scene;
                if (!existingScene.IsValid())
                {
                    Debug.Log($"[FlowDupCheck] EnsurePresent.ignoreExisting scene=<invalid> name={existingGo.name} id={e.GetInstanceID()}");
                    continue;
                }

                bool isDdol = string.Equals(existingScene.name, "DontDestroyOnLoad", System.StringComparison.Ordinal);
                if (!isDdol && !existingScene.isLoaded)
                {
                    Debug.Log($"[FlowDupCheck] EnsurePresent.ignoreExisting sceneNotLoaded name={existingGo.name} id={e.GetInstanceID()} scene={existingScene.name}");
                    continue;
                }

                return;
            }
        }

        FlowDupLog("EnsurePresent.preCreate");
        var go = new GameObject("Chapter1_Node1_Flow");
        go.hideFlags = HideFlags.DontSave;
        Object.DontDestroyOnLoad(go);
        var c = go.AddComponent<Chapter1Node1FlowController>();
        FlowDupLog("EnsurePresent.postCreate", c);
    }

    private void Awake()
    {
        FlowDupLog("Awake.enter", this);
        // Single-instance guard: prevent duplicates after scene reload / Retry back to Main.
        FlowDupLog("Awake.preSingleInstanceGuard", this);
        var all = FindObjectsOfType<Chapter1Node1FlowController>();
        if (all != null && all.Length > 1)
        {
            FlowDupLog("Awake.guard.detectedMultiple", this);
            Chapter1Node1FlowController keep = null;
            int keepId = int.MaxValue;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                int id = c.GetInstanceID();
                if (id < keepId)
                {
                    keepId = id;
                    keep = c;
                }
            }

            if (keep != null && keep != this)
            {
                FlowDupLog("Awake.guard.destroySelf", this);
                Destroy(gameObject);
                return;
            }

            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || c == this) continue;
                FlowDupLog("Awake.guard.destroyOther", this);
                Destroy(c.gameObject);
            }
        }
        FlowDupLog("Awake.postSingleInstanceGuard", this);

        var s = SceneManager.GetActiveScene();
        // Primary: Context is the truth source.
        if (TryGetTargetNode1FromContext(out _ctx, out _def))
        {
            // Keep _def from Context. Do not override Context here.
        }
        else
        {
            // If Context exists and is explicitly NOT Node1, this flow should not run.
            if (_ctx != null && _ctx.CurrentDefinition != null)
            {
                FlowDupLog("Awake.destroyNotTargetNode1", this);
                Destroy(gameObject);
                return;
            }

            // Fallback: legacy scene-name based resolution (only when Context is missing or empty).
            NodeRegistry.EnsureInitialized();
            if (!IsTargetSceneFallback(s.name, out var legacyDef))
            {
                FlowDupLog("Awake.destroyFallbackNotMatched", this);
                Destroy(gameObject);
                return;
            }

            _def = legacyDef;
            if (_def == null && metadata == null)
                metadata = new ChapterNodeMetadata();
        }

        // Minimal formal start prompt: pause until player acknowledges (keeps gameplay chain untouched).
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        FlowDupLog("OnDestroy", this);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        _canvas = FindObjectOfType<Canvas>();
        _baseHealth = FindObjectOfType<BaseHealth>();
        _enemySpawner = FindObjectOfType<EnemySpawner>();
        if (_ctx == null) _ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        BuildUiIfNeeded();
        ShowIntro();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Primary: Context is the truth source.
        if (TryGetTargetNode1FromContext(out _ctx, out _def))
        {
            // ok
        }
        else
        {
            // If Context exists and is explicitly NOT Node1, this flow should not run.
            if (_ctx != null && _ctx.CurrentDefinition != null)
            {
                Destroy(gameObject);
                return;
            }

            // Fallback: legacy scene-name based resolution (only when Context is missing or empty).
            NodeRegistry.EnsureInitialized();
            if (!IsTargetSceneFallback(scene.name, out var legacyDef))
            {
                Destroy(gameObject);
                return;
            }
            _def = legacyDef;
        }

        // Rebind UI in case the canvas was recreated.
        _canvas = FindObjectOfType<Canvas>();
        _baseHealth = FindObjectOfType<BaseHealth>();
        _enemySpawner = FindObjectOfType<EnemySpawner>();
        if (_ctx == null) _ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        _introRoot = null;
        _resultRoot = null;
        BuildUiIfNeeded();

        _state = NodeFlowState.NotStarted;
        Time.timeScale = 0f;
        ShowIntro();
    }

    private void Update()
    {
        if (_state != NodeFlowState.Playing)
            return;

        if (_baseHealth == null)
            _baseHealth = FindObjectOfType<BaseHealth>();
        if (_enemySpawner == null)
            _enemySpawner = FindObjectOfType<EnemySpawner>();

        // Defeat: existing runtime behavior pauses time when base reaches 0.
        if (_baseHealth != null && _baseHealth.GetCurrentHealth() <= 0)
        {
            HandleDefeat();
            return;
        }

        if (ShouldCompleteNode1())
            HandleVictory();
    }

    private bool ShouldCompleteNode1()
    {
        if (_state == NodeFlowState.Victory || _state == NodeFlowState.Defeat)
            return false;

        // Context must explicitly be Chapter1:Node1 (single conclusion layer).
        var ctxDef = _ctx != null ? _ctx.CurrentDefinition : null;
        if (!IsTargetNode1(ctxDef))
            return false;

        if (_baseHealth != null && _baseHealth.GetCurrentHealth() <= 0)
            return false;

        if (_enemySpawner == null)
            return false;

        // Execution facts (no victory semantics).
        int wave = _enemySpawner.GetCurrentWave();
        bool waiting = _enemySpawner.IsWaitingForNextWave();
        bool hasMore = _enemySpawner.HasMoreConfiguredWaves;

        if (wave <= 0)
            return false;

        // Node1 completion is based on reaching the target final wave (not on spawner "no more configured waves").
        int targetFinalWave = 8;
        if (wave < targetFinalWave)
            return false;

        if (FindObjectsOfType<Enemy>().Length != 0)
            return false;

        // Read-only facts kept for debugging/telemetry parity; not part of the completion gate.
        _ = waiting;
        _ = hasMore;
        return true;
    }

    private void ShowIntro()
    {
        if (_state == NodeFlowState.Victory || _state == NodeFlowState.Defeat)
            return;

        _state = NodeFlowState.IntroShowing;
        if (_introRoot != null)
            _introRoot.SetActive(true);
        if (_resultRoot != null)
            _resultRoot.SetActive(false);
    }

    private void StartPlaying()
    {
        if (_state != NodeFlowState.IntroShowing && _state != NodeFlowState.NotStarted)
            return;

        _state = NodeFlowState.Playing;
        if (_introRoot != null)
            _introRoot.SetActive(false);
        if (_resultRoot != null)
            _resultRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    private void HandleVictory()
    {
        if (_state == NodeFlowState.Victory || _state == NodeFlowState.Defeat)
            return;

        _state = NodeFlowState.Victory;

        string title = _def != null ? _def.victoryTitle : $"{metadata.displayName} Complete";
        string body = _def != null ? _def.victoryBody : metadata.victoryText;
        bool allowRetry = _def != null ? _def.allowRetry : true;
        bool allowContinue = _def != null ? _def.allowContinue : true;

        ShowResult(
            title: title,
            body: body,
            primary: allowContinue ? "Continue" : "Close",
            secondary: allowRetry ? "Retry" : string.Empty,
            allowRetry: allowRetry
        );
    }

    private void HandleDefeat()
    {
        if (_state == NodeFlowState.Victory || _state == NodeFlowState.Defeat)
            return;

        _state = NodeFlowState.Defeat;

        string title = _def != null ? _def.defeatTitle : "Mission Failed";
        string body = _def != null ? _def.defeatBody : metadata.defeatText;
        bool allowRetry = _def != null ? _def.allowRetry : true;

        ShowResult(
            title: title,
            body: body,
            primary: allowRetry ? "Retry" : "Close",
            secondary: "Close",
            allowRetry: allowRetry
        );
    }

    private void ShowResult(string title, string body, string primary, string secondary, bool allowRetry)
    {
        BuildUiIfNeeded();
        if (_introRoot != null)
            _introRoot.SetActive(false);
        if (_resultRoot != null)
            _resultRoot.SetActive(true);

        if (_resultTitle != null) _resultTitle.text = title;
        if (_resultBody != null) _resultBody.text = body;

        if (_resultPrimaryLabel != null) _resultPrimaryLabel.text = primary;
        if (_resultSecondaryLabel != null) _resultSecondaryLabel.text = secondary;

        if (_resultPrimaryButton != null)
        {
            _resultPrimaryButton.onClick.RemoveAllListeners();
            if (primary == "Retry" && allowRetry) _resultPrimaryButton.onClick.AddListener(Retry);
            else if (primary == "Continue") _resultPrimaryButton.onClick.AddListener(ContinueToNextNode);
            else _resultPrimaryButton.onClick.AddListener(CloseResult);
        }

        if (_resultSecondaryButton != null)
        {
            _resultSecondaryButton.onClick.RemoveAllListeners();
            if (secondary == "Retry" && allowRetry) _resultSecondaryButton.onClick.AddListener(Retry);
            else _resultSecondaryButton.onClick.AddListener(CloseResult);
        }

        // Time is already 0 from existing win/lose; enforce in case external callers triggered without it.
        Time.timeScale = 0f;
    }

    private void CloseResult()
    {
        if (_resultRoot != null)
            _resultRoot.SetActive(false);
    }

    private void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }

    private void ContinueToNextNode()
    {
        Debug.Log("[FlowDupCheck] ContinueToNextNode.enter");
        string chapterId = _ctx != null ? _ctx.currentChapterId : (_def != null ? _def.chapterId : "Chapter1");
        string nextNodeId = _ctx != null ? _ctx.nextNodeId : (_def != null ? _def.nextNodeId : "Node2");

        if (string.IsNullOrEmpty(nextNodeId) || !NodeRegistry.TryGet(chapterId, nextNodeId, out var next))
        {
            ShowContinuePlaceholder("Next node is not registered yet.");
            return;
        }

        var ctx = _ctx != null ? _ctx : (RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : FindObjectOfType<RuntimeNodeContext>());
        if (ctx == null || !ctx.SetCurrentByKey(chapterId, nextNodeId))
        {
            ShowContinuePlaceholder("Failed to set next node.");
            return;
        }

        _isInvalidForReuse = true;
        Debug.Log("[FlowDupCheck] ContinueToNextNode.marked invalidForReuse");
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }

    private void ShowContinuePlaceholder(string message)
    {
        if (_resultBody != null)
            _resultBody.text = string.IsNullOrEmpty(message) ? (_resultBody.text ?? string.Empty) : $"{_resultBody.text}\n\n{message}";

        if (_resultPrimaryLabel != null) _resultPrimaryLabel.text = "Close";
        if (_resultPrimaryButton != null)
        {
            _resultPrimaryButton.onClick.RemoveAllListeners();
            _resultPrimaryButton.onClick.AddListener(CloseResult);
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
    }

    private GameObject BuildIntroPanel(Transform parent)
    {
        var root = BuildPanelRoot(parent, "Chapter1_Node1_IntroPanel", blocksRaycasts: true);

        var card = BuildCard(root.transform, new Vector2(640f, 320f));

        string introTitle = _def != null ? _def.introTitle : metadata.displayName;
        string objectiveText = _def != null ? _def.objectiveText : metadata.objectiveText;
        string introBody = _def != null ? _def.introBody : metadata.introText;

        var title = BuildText(card.transform, "Title", introTitle, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(600f, 48f));

        var objective = BuildText(card.transform, "Objective", objectiveText, 20, FontStyles.Normal, TextAlignmentOptions.Center);
        objective.color = new Color(1f, 0.95f, 0.7f, 1f);
        SetAnchored(objective.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 40f));

        var intro = BuildText(card.transform, "Body", introBody, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        intro.enableWordWrapping = true;
        SetAnchored(intro.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(600f, 140f));

        var startBtn = BuildButton(card.transform, "StartButton", "Start");
        SetAnchored(startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(220f, 44f));
        startBtn.onClick.AddListener(StartPlaying);

        root.SetActive(false);
        return root;
    }

    private GameObject BuildResultPanel(Transform parent)
    {
        var root = BuildPanelRoot(parent, "Chapter1_Node1_ResultPanel", blocksRaycasts: true);
        var card = BuildCard(root.transform, new Vector2(680f, 340f));

        _resultTitle = BuildText(card.transform, "ResultTitle", "Result", 34, FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchored(_resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(640f, 52f));

        _resultBody = BuildText(card.transform, "ResultBody", "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _resultBody.enableWordWrapping = true;
        SetAnchored(_resultBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(620f, 150f));

        _resultPrimaryButton = BuildButton(card.transform, "PrimaryButton", "Retry");
        _resultPrimaryLabel = _resultPrimaryButton.GetComponentInChildren<TMP_Text>(true);
        SetAnchored(_resultPrimaryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 22f), new Vector2(240f, 44f));

        _resultSecondaryButton = BuildButton(card.transform, "SecondaryButton", "Close");
        _resultSecondaryLabel = _resultSecondaryButton.GetComponentInChildren<TMP_Text>(true);
        SetAnchored(_resultSecondaryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 22f), new Vector2(240f, 44f));

        root.SetActive(false);
        return root;
    }

    private static GameObject BuildPanelRoot(Transform parent, string name, bool blocksRaycasts)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = blocksRaycasts;
        cg.interactable = blocksRaycasts;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root.transform, false);
        var dimRt = dim.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        var img = dim.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        return root;
    }

    private static GameObject BuildCard(Transform parent, Vector2 size)
    {
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(parent, false);

        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var img = card.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

        return card;
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
        txt.raycastTarget = false;
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
        {
            rt.sizeDelta = size;
        }
        else
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

