using UnityEngine;

public class Tower : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 5f;
    public float attackInterval = 1f;

    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletDamage = 2f;

    [Header("Upgrade Settings")]
    public int level = 1;
    public int upgradeCost = 50;
    public int sellValue = 20;

    [Tooltip("0 = no level cap (unlimited upgrades).")]
    [SerializeField] private int maxTowerLevel = 0;

    private float timer = 0f;

    /// <summary>When <see cref="maxTowerLevel"/> &gt; 0 and level reached, upgrades are disabled in UI.</summary>
    public bool IsAtMaxLevel() => maxTowerLevel > 0 && level >= maxTowerLevel;

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= attackInterval)
        {
            Attack();
            timer = 0f;
        }
    }

    void Attack()
    {
        Enemy target = FindNearestEnemy();

        if (target != null && firePoint != null)
        {
            GameObject bulletObj = PoolManager.Instance.Spawn(
                "Projectile_Bullet",
                firePoint.position,
                Quaternion.identity
            );

            if (bulletObj == null)
            {
                Debug.LogError("无法生成子弹：Projectile_Bullet 对象池不存在");
                return;
            }

            Bullet bullet = bulletObj.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.damage = bulletDamage;
                bullet.SetTarget(target);
            }
        }
    }

    Enemy FindNearestEnemy()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();

        Enemy nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < attackRange && distance < minDistance)
            {
                minDistance = distance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    public void UpgradeTower()
    {
        level++;
        attackRange += 1f;
        bulletDamage += 1f;
        attackInterval = Mathf.Max(0.2f, attackInterval - 0.1f);

        upgradeCost += 25;
        sellValue += 15;

        Debug.Log("Tower upgraded to level " + level);
    }

    public int GetUpgradeCost()
    {
        return upgradeCost;
    }

    public int GetSellValue()
    {
        return sellValue;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}