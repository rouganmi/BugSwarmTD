using UnityEngine;

/// <summary>
/// One buildable tower entry for <see cref="BuildSelectionUI"/>. Extend the list in Inspector as new tower types are added.
/// </summary>
[System.Serializable]
public class BuildTowerOption
{
    [Tooltip("Stable id for save/analytics (optional).")]
    public string id = "basic";

    public string displayName = "基础塔";
    public GameObject towerPrefab;
    public int cost = 30;
}
