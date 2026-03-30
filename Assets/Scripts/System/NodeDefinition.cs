using System;

[Serializable]
public class NodeDefinition
{
    public string chapterId;
    public string nodeId;
    public string displayName;

    public string introTitle;
    public string introBody;
    public string objectiveText;

    public string victoryTitle;
    public string victoryBody;

    public string defeatTitle;
    public string defeatBody;

    public string runtimeSceneName;
    public string nextNodeId;

    /// <summary>Presentation-only expectation for end-state observation (does not drive spawns).</summary>
    public int expectedFinalWave;

    public bool allowRetry = true;
    public bool allowContinue = true;
}

