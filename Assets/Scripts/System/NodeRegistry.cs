using System.Collections.Generic;

public static class NodeRegistry
{
    private static readonly Dictionary<string, NodeDefinition> Nodes = new Dictionary<string, NodeDefinition>();
    private static bool _initialized;

    public static string MakeKey(string chapterId, string nodeId) => $"{chapterId}:{nodeId}";

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        // Chapter 1 Node 1 (validated prototype scene; presentation-only metadata).
        Register(new NodeDefinition
        {
            chapterId = "Chapter1",
            nodeId = "Node1",
            displayName = "Chapter1_Node1",
            introTitle = "Chapter1_Node1",
            objectiveText = "Defend the base and survive all waves.",
            introBody =
                "Hold the line through 8 waves.\n\n" +
                "Hint: Basic handles standard pressure. Sniper is important against Shield enemies.",
            victoryTitle = "Chapter1_Node1 Complete",
            victoryBody = "Next: Node2",
            defeatTitle = "Mission Failed",
            defeatBody = "Try again.",
            runtimeSceneName = "Chapter1_Node1_Prototype",
            nextNodeId = "Node2",
            expectedFinalWave = 8,
            allowRetry = true,
            allowContinue = true
        });

        // Chapter 1 Node 2 (placeholder only; no gameplay content yet).
        Register(new NodeDefinition
        {
            chapterId = "Chapter1",
            nodeId = "Node2",
            displayName = "Chapter1_Node2",
            introTitle = "Chapter1_Node2",
            objectiveText = "Defend the base and survive all waves.",
            introBody =
                "Enemy pressure is increasing.\n\n" +
                "Prepare earlier and build a stronger defense. Runners and Shields will arrive sooner.",
            victoryTitle = "Chapter1_Node2 Complete",
            victoryBody =
                "The frontline is stabilizing.\n" +
                "Prepare for further escalation.",
            defeatTitle = "Mission Failed",
            defeatBody =
                "The defense line collapsed under pressure.\n" +
                "Rebuild and try again.",
            runtimeSceneName = "Chapter1_Node2",
            nextNodeId = string.Empty,
            expectedFinalWave = 8,
            allowRetry = true,
            allowContinue = false
        });
    }

    public static void Register(NodeDefinition def)
    {
        if (def == null) return;
        if (string.IsNullOrWhiteSpace(def.chapterId) || string.IsNullOrWhiteSpace(def.nodeId))
            return;
        Nodes[MakeKey(def.chapterId, def.nodeId)] = def;
    }

    public static bool TryGet(string chapterId, string nodeId, out NodeDefinition def)
    {
        EnsureInitialized();
        return Nodes.TryGetValue(MakeKey(chapterId, nodeId), out def);
    }

    public static bool TryGetByScene(string sceneName, out NodeDefinition def)
    {
        EnsureInitialized();
        def = null;
        if (string.IsNullOrEmpty(sceneName)) return false;

        foreach (var kv in Nodes)
        {
            var d = kv.Value;
            if (d == null) continue;
            if (string.Equals(d.runtimeSceneName, sceneName, System.StringComparison.Ordinal))
            {
                def = d;
                return true;
            }
        }
        return false;
    }
}

