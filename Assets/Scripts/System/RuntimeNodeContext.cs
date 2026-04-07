using UnityEngine;
using UnityEngine.SceneManagement;

public class RuntimeNodeContext : MonoBehaviour
{
    public static RuntimeNodeContext Instance { get; private set; }

    [Header("Current (runtime-only; not saved)")]
    public string currentChapterId;
    public string currentNodeId;
    public string currentSceneName;
    public string nextNodeId;

    public NodeDefinition CurrentDefinition { get; private set; }

    // True when currentChapterId/currentNodeId was explicitly set via SetCurrentByKey.
    // When true, scene-loaded fallback should not overwrite the explicit truth source.
    [SerializeField] private bool hasExplicitCurrentNode;

    public void SetCurrent(NodeDefinition def)
    {
        CurrentDefinition = def;
        if (def == null)
        {
            currentChapterId = string.Empty;
            currentNodeId = string.Empty;
            nextNodeId = string.Empty;
            currentSceneName = SceneManager.GetActiveScene().name;
            return;
        }

        currentChapterId = def.chapterId;
        currentNodeId = def.nodeId;
        nextNodeId = def.nextNodeId;
        currentSceneName = SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// Canonical entry: explicitly set current node by chapterId/nodeId.
    /// This becomes the runtime truth source and will not be overwritten by scene-name fallback refresh.
    /// </summary>
    public bool SetCurrentByKey(string chapterId, string nodeId)
    {
        NodeRegistry.EnsureInitialized();
        if (!NodeRegistry.TryGet(chapterId, nodeId, out var def) || def == null)
            return false;

        hasExplicitCurrentNode = true;
        SetCurrent(def);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureContext()
    {
        if (Object.FindObjectOfType<RuntimeNodeContext>() != null)
            return;

        var go = new GameObject("RuntimeNodeContext");
        go.hideFlags = HideFlags.DontSave;
        Object.DontDestroyOnLoad(go);
        go.AddComponent<RuntimeNodeContext>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start()
    {
        RefreshFromScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshFromScene(scene);
    }

    private void RefreshFromScene(Scene scene)
    {
        currentSceneName = scene.name;
        // Compatibility fallback only: do not overwrite an explicit current node.
        if (hasExplicitCurrentNode)
            return;

        if (NodeRegistry.TryGetByScene(scene.name, out var def))
        {
            SetCurrent(def);
            return;
        }

        SetCurrent(null);
    }
}

