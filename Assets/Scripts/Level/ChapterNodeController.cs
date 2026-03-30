/*
 * WIP (not integrated): Node1 formalization scaffolding.
 * This script is not attached in any scene in the current baseline and is not part of active gameplay flow.
 * Reserved for future, explicit Chapter1_Node1 scene integration.
 */
using UnityEngine;

public class ChapterNodeController : MonoBehaviour
{
    public enum NodeState
    {
        NotStarted,
        Running,
        Completed
    }

    [Header("Identity")]
    public string chapterId = "Chapter1";
    public string nodeId = "Node1";
    public string nodeDisplayName = "Chapter1_Node1";

    [Header("Presentation")]
    [TextArea(1, 3)] public string shortDescription =
        "Defend the base. Basic towers handle standard enemies. Watch for fast enemies and patch weak lanes.";

    public bool autoShowStartIntel = true;

    [Header("Future-ready (reserved)")]
    public string nextNodeId = "";
    public bool isFormalNode = true;

    [Header("Runtime")]
    [SerializeField] private NodeState state = NodeState.NotStarted;
    public NodeState State => state;

    public void StartNode()
    {
        if (state != NodeState.NotStarted) return;
        state = NodeState.Running;
        Debug.Log($"[Node] Started {nodeDisplayName}");
    }

    public void CompleteNode()
    {
        if (state == NodeState.Completed) return;
        state = NodeState.Completed;
        Debug.Log($"[Node] Completed {nodeDisplayName}");
    }
}

