using System;
using UnityEngine;

[Serializable]
public class ChapterNodeMetadata
{
    [Header("Identity")]
    public string chapterId = "Chapter1";
    public string nodeId = "Node1";
    public string displayName = "Chapter1_Node1";

    [Header("Presentation")]
    [TextArea(1, 3)] public string objectiveText = "Defend the base and survive all waves.";
    [TextArea(2, 6)] public string introText =
        "Hold the line through 8 waves.\n\n" +
        "Hint: Basic handles standard pressure. Sniper is important against Shield enemies.";
    [TextArea(2, 6)] public string victoryText = "Chapter1_Node1 Complete.\nNext: Node2";
    [TextArea(2, 6)] public string defeatText = "Mission failed.\nTry again.";

    [Header("Future hook (reserved)")]
    public string nextNodeId = "Node2";
}

