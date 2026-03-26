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

    private float timer = 0f;

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

        if (target != null && bulletPrefab != null && firePoint != null)
        {
            GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

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