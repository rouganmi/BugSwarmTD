using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Thin Main-scene bootstrap host.
/// Responsibility: explicitly set the default runtime node key on startup.
/// </summary>
public class MainNodeBootstrapHost : MonoBehaviour
{
    [Header("Default node key (explicit truth source)")]
    [SerializeField] private string defaultChapterId = "Chapter1";
    [SerializeField] private string defaultNodeId = "Node1";

    private void Start()
    {
        // Only apply in Main scene to avoid unintended overrides elsewhere.
        var s = SceneManager.GetActiveScene();
        if (!s.IsValid() || !string.Equals(s.name, "Main", System.StringComparison.Ordinal))
            return;

        var ctx = RuntimeNodeContext.Instance != null ? RuntimeNodeContext.Instance : FindObjectOfType<RuntimeNodeContext>();
        if (ctx == null)
            return;

        bool ok = ctx.SetCurrentByKey(defaultChapterId, defaultNodeId);
        if (ok)
        {
            Debug.Log($"[NodeContext] Explicit default set: {defaultChapterId}:{defaultNodeId}", this);
            Chapter1Node1FlowController.EnsurePresentForCurrentContext();
        }
        else
            Debug.LogWarning($"[NodeContext] Failed to set default: {defaultChapterId}:{defaultNodeId}", this);
    }
}

