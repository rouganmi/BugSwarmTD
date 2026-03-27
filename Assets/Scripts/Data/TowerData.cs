using UnityEngine;

[CreateAssetMenu(menuName = "BugSwarmTD/Tower Data")]
public class TowerData : ScriptableObject
{
    public string towerId;
    public string towerName;
    public int buildCost;
    public int sellValue;
    public float range;
    public float fireRate;
    public int damage;
    public GameObject towerPrefab;
    public string projectilePoolKey;
}