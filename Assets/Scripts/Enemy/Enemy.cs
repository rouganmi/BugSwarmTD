using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Move Settings")]
    public float moveSpeed = 3f;

    [Header("Health Settings")]
    public float maxHealth = 10f;

    [Header("Damage Settings")]
    public int damageToBase = 1;

    [Header("Reward Settings")]
    public int goldReward = 10;

    private float currentHealth;
    private Transform target;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void InitializeStats(float newMaxHealth, float newMoveSpeed, int newGoldReward, int newDamageToBase)
    {
        maxHealth = newMaxHealth;
        moveSpeed = newMoveSpeed;
        goldReward = newGoldReward;
        damageToBase = newDamageToBase;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (target == null) return;

        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (direction != Vector3.zero)
        {
            transform.forward = direction;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance < 0.5f)
        {
            ReachBase();
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;

        Debug.Log(name + " HP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        CurrencySystem currencySystem = FindObjectOfType<CurrencySystem>();
        if (currencySystem != null)
        {
            currencySystem.AddGold(goldReward);
        }

        Debug.Log(name + " died");
        Destroy(gameObject);
    }

    private void ReachBase()
    {
        if (target != null)
        {
            BaseHealth baseHealth = target.GetComponent<BaseHealth>();
            if (baseHealth != null)
            {
                baseHealth.TakeDamage(damageToBase);
            }
        }

        Debug.Log(name + " reached the base!");
        Destroy(gameObject);
    }
}