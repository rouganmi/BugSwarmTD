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

        // Prefer an already-present explicit current node if it's valid in NodeRegistry.
        NodeRegistry.EnsureInitialized();
        string chapterId = defaultChapterId;
        string nodeId = defaultNodeId;

        if (!string.IsNullOrWhiteSpace(ctx.currentChapterId) &&
            !string.IsNullOrWhiteSpace(ctx.currentNodeId) &&
            NodeRegistry.TryGet(ctx.currentChapterId, ctx.currentNodeId, out var _))
        {
            chapterId = ctx.currentChapterId;
            nodeId = ctx.currentNodeId;
            Debug.Log($"[NodeContext] Bootstrap source=explicit current {chapterId}:{nodeId}", this);
        }
        else
        {
            Debug.Log($"[NodeContext] Bootstrap source=inspector default {chapterId}:{nodeId}", this);
        }

        bool ok = ctx.SetCurrentByKey(chapterId, nodeId);
        if (ok)
        {
            Debug.Log($"[NodeContext] Explicit current set: {chapterId}:{nodeId}", this);
            Chapter1Node1FlowController.EnsurePresentForCurrentContext();
            Chapter1Node2FlowController.EnsurePresentForCurrentContext();
        }
        else
            Debug.LogWarning($"[NodeContext] Failed to set current: {chapterId}:{nodeId}", this);
    }
}

