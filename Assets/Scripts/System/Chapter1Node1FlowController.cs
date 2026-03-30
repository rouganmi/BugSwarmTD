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

    private bool IsTargetScene(string sceneName) =>
        string.Equals(sceneName, PrototypeSceneName, System.StringComparison.Ordinal) ||
        (NodeRegistry.TryGetByScene(sceneName, out var d) && d != null);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePresent()
    {
        var s = SceneManager.GetActiveScene();
        if (!s.IsValid())
            return;
        if (!string.Equals(s.name, PrototypeSceneName, System.StringComparison.Ordinal) &&
            !NodeRegistry.TryGetByScene(s.name, out _))
            return;

        if (Object.FindObjectOfType<Chapter1Node1FlowController>() != null)
            return;

        var go = new GameObject("Chapter1_Node1_Flow");
        go.hideFlags = HideFlags.DontSave;
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Chapter1Node1FlowController>();
    }

    private void Awake()
    {
        var s = SceneManager.GetActiveScene();
        if (!IsTargetScene(s.name))
        {
            Destroy(gameObject);
            return;
        }

        NodeRegistry.EnsureInitialized();
        _ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        if (!NodeRegistry.TryGetByScene(s.name, out _def))
        {
            // Fallback to prior Node1 metadata defaults if registry lookup fails for any reason.
            if (metadata == null)
                metadata = new ChapterNodeMetadata();
        }
        else
        {
            if (_ctx != null) _ctx.SetCurrent(_def);
        }

        // Minimal formal start prompt: pause until player acknowledges (keeps gameplay chain untouched).
        Time.timeScale = 0f;
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
        if (!IsTargetScene(scene.name))
        {
            Destroy(gameObject);
            return;
        }

        // Rebind UI in case the canvas was recreated.
        _canvas = FindObjectOfType<Canvas>();
        _baseHealth = FindObjectOfType<BaseHealth>();
        _enemySpawner = FindObjectOfType<EnemySpawner>();
        if (_ctx == null) _ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : Object.FindObjectOfType<RuntimeNodeContext>();
        NodeRegistry.TryGetByScene(scene.name, out _def);
        if (_ctx != null && _def != null) _ctx.SetCurrent(_def);
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

        // Victory: observe the validated prototype condition without coupling to EnemySpawner events.
        // Use Time.timeScale == 0 as a guard to avoid false positives at the start of wave 8 before any spawns.
        int expectedFinalWave = _def != null ? _def.expectedFinalWave : 8;
        if (_enemySpawner != null &&
            _enemySpawner.GetCurrentWave() >= expectedFinalWave &&
            FindObjectsOfType<Enemy>().Length == 0 &&
            (Time.timeScale <= 0f || _enemySpawner.IsWaitingForNextWave()))
        {
            HandleVictory();
        }
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
        var s = SceneManager.GetActiveScene();
        SceneManager.LoadScene(s.name);
    }

    private void ContinueToNextNode()
    {
        string chapterId = _ctx != null ? _ctx.currentChapterId : (_def != null ? _def.chapterId : "Chapter1");
        string nextNodeId = _ctx != null ? _ctx.nextNodeId : (_def != null ? _def.nextNodeId : "Node2");

        if (string.IsNullOrEmpty(nextNodeId) || !NodeRegistry.TryGet(chapterId, nextNodeId, out var next))
        {
            ShowContinuePlaceholder("Next node is not registered yet.");
            return;
        }

        if (string.IsNullOrWhiteSpace(next.runtimeSceneName) || !Application.CanStreamedLevelBeLoaded(next.runtimeSceneName))
        {
            ShowContinuePlaceholder($"{next.displayName} is not playable yet.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(next.runtimeSceneName);
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

