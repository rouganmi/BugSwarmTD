using UnityEngine;

public class ProjectileBase : MonoBehaviour, IPoolable
{
    private const string HitEffectPoolKey = "Effect_Hit";
    [SerializeField] private float speed = 10f;
    [SerializeField] private int damage = 1;

    private Transform target;
    private bool isActiveProjectile;

    public void Setup(Transform targetTransform, int projectileDamage)
    {
        target = targetTransform;
        damage = projectileDamage;
        isActiveProjectile = true;
    }

    private void Update()
    {
        if (!isActiveProjectile) return;

        if (target == null)
        {
            ReturnToPool();
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        Vector3 hitPos = transform.position;

        if (target != null)
        {
            hitPos = target.position;
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }

        if (PoolManager.Instance != null)
        {
            Vector3 spawnPos = hitPos + Vector3.up * 0.45f;
            GameObject fx = PoolManager.Instance.Spawn(HitEffectPoolKey, spawnPos, Quaternion.identity);
            if (fx == null)
            {
                Debug.LogWarning($"[ProjectileBase] Hit effect spawn failed for key: {HitEffectPoolKey}");
            }
            else
            {
                var ps = fx.GetComponentInChildren<ParticleSystem>(true);
                if (ps != null)
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
            }
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        isActiveProjectile = false;
        PoolManager.Instance.Despawn(gameObject);
    }

    public void OnSpawn()
    {
        isActiveProjectile = false;
        target = null;
    }

    public void OnDespawn()
    {
        isActiveProjectile = false;
        target = null;
    }
}
