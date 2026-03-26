using UnityEngine;

public class BaseHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 20;

    private int currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
        Debug.Log("Base HP: " + currentHealth);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log("Base HP: " + currentHealth);

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
