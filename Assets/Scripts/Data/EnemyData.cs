using UnityEngine;

[CreateAssetMenu(menuName = "BugSwarmTD/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyId;
    public string enemyName;
    public float moveSpeed = 2f;
    public int maxHp = 10;
    public int rewardGold = 5;
    public GameObject prefab;
}