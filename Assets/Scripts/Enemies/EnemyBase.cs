using UnityEngine;

public class EnemyBase : MonoBehaviour, IDamageable, IPoolable
{
    [SerializeField] private EnemyData enemyData;

    private int currentHp;
    private SimpleHitFeedback _hitFeedback;

    public int CurrentHp => currentHp;
    public int RewardGold => enemyData.rewardGold;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        currentHp = enemyData.maxHp;
    }

    public void TakeDamage(int damage)
    {
        if (damage > 0)
        {
            if (_hitFeedback == null) _hitFeedback = GetComponent<SimpleHitFeedback>();
            if (_hitFeedback == null) _hitFeedback = gameObject.AddComponent<SimpleHitFeedback>();
            _hitFeedback.SetStrength(2f);
            _hitFeedback.Play();
        }

        currentHp -= damage;

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        EconomyManager.Instance.AddGold(enemyData.rewardGold);
        GameEvents.OnEnemyKilled?.Invoke();
        PoolManager.Instance.Despawn(gameObject);
    }

    public void OnSpawn()
    {
        Initialize();

        // Ensure feedback exists for pooled enemies without editing prefabs.
        if (_hitFeedback == null) _hitFeedback = GetComponent<SimpleHitFeedback>();
        if (_hitFeedback == null) _hitFeedback = gameObject.AddComponent<SimpleHitFeedback>();
        _hitFeedback.SetStrength(2f);
    }

    public void OnDespawn()
    {
    }
}
