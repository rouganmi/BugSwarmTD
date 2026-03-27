using UnityEngine;

public class TowerBase : MonoBehaviour
{
    [SerializeField] protected TowerData towerData;

    protected Transform currentTarget;
    protected float attackTimer;

    protected virtual void Update()
    {
        if (currentTarget == null)
        {
            FindTarget();
            return;
        }

        attackTimer += Time.deltaTime;
        float attackInterval = 1f / towerData.fireRate;

        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            Attack();
        }

        if (Vector3.Distance(transform.position, currentTarget.position) > towerData.range)
        {
            currentTarget = null;
        }
    }

    protected virtual void FindTarget()
    {
        EnemyBase[] enemies = FindObjectsOfType<EnemyBase>();

        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (var enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance <= towerData.range && distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = enemy.transform;
            }
        }

        currentTarget = bestTarget;
    }

    protected virtual void Attack()
    {
        if (currentTarget == null) return;

        // Lightweight fire feedback without changing tower logic.
        var fireFeedback = GetComponent<SimpleHitFeedback>();
        if (fireFeedback == null) fireFeedback = gameObject.AddComponent<SimpleHitFeedback>();
        fireFeedback.Play();

        GameObject projectile = PoolManager.Instance.Spawn(
            towerData.projectilePoolKey,
            transform.position,
            Quaternion.identity
        );

        if (projectile == null) return;

        ProjectileBase projectileBase = projectile.GetComponent<ProjectileBase>();
        projectileBase.Setup(currentTarget, towerData.damage);
    }
}