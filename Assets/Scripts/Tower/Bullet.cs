using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{
    private const string HitEffectPoolKey = "Effect_Hit";
    public float speed = 10f;
    public float damage = 1f;

    private Enemy target;
    private bool isActiveBullet;

    public void SetTarget(Enemy newTarget)
    {
        target = newTarget;
        isActiveBullet = true;
    }

    private void Update()
    {
        if (!isActiveBullet) return;

        if (target == null)
        {
            ReturnToPool();
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.transform.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.transform.position) < 0.15f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        Vector3 hitPos = transform.position;

        if (target != null)
        {
            hitPos = target.transform.position;
            target.TakeDamage(damage);
        }

        if (PoolManager.Instance != null)
        {
            Vector3 spawnPos = hitPos + Vector3.up * 0.45f;
            GameObject fx = PoolManager.Instance.Spawn(HitEffectPoolKey, spawnPos, Quaternion.identity);
            if (fx == null)
            {
                Debug.LogWarning($"[Bullet] Hit effect spawn failed for key: {HitEffectPoolKey}");
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
        isActiveBullet = false;
        target = null;
        PoolManager.Instance.Despawn(gameObject);
    }

    public void OnSpawn()
    {
        isActiveBullet = false;
        target = null;
    }

    public void OnDespawn()
    {
        isActiveBullet = false;
        target = null;
    }
}
