using UnityEngine;

public class BaseHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 20;

    private int currentHealth;
    private SimpleHitFeedback _hitFeedback;

    private void Start()
    {
        currentHealth = maxHealth;
        Debug.Log("Base HP: " + currentHealth);
    }

    public void TakeDamage(int damage)
    {
        int before = currentHealth;
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log("Base HP: " + currentHealth);

        if (damage > 0 && currentHealth < before)
        {
            if (_hitFeedback == null) _hitFeedback = GetComponent<SimpleHitFeedback>();
            if (_hitFeedback == null) _hitFeedback = gameObject.AddComponent<SimpleHitFeedback>();
            _hitFeedback.Play();
        }

        GameEvents.OnBaseHpChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
    Debug.Log("GAME OVER");
    Time.timeScale = 0f;
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }
}
