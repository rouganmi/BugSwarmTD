using UnityEngine;
using TMPro;

public class GameUI : MonoBehaviour
{
    [Header("References")]
    public BaseHealth baseHealth;
    public EnemySpawner enemySpawner;
    public CurrencySystem currencySystem;

    [Header("UI Text")]
    public TMP_Text baseHealthText;
    public TMP_Text waveText;
    public TMP_Text nextWaveText;
    public TMP_Text goldText;

    private void Update()
    {
        if (baseHealth != null && baseHealthText != null)
        {
            baseHealthText.text = "Base HP: " + baseHealth.GetCurrentHealth();
        }

        if (enemySpawner != null && waveText != null)
        {
            waveText.text = "Wave: " + enemySpawner.GetCurrentWave();
        }

        if (enemySpawner != null && nextWaveText != null)
        {
            if (enemySpawner.IsWaitingForNextWave())
            {
                float timeLeft = enemySpawner.GetWaveTimer();
                if (timeLeft < 0f) timeLeft = 0f;

                nextWaveText.text = "Next Wave In: " + timeLeft.ToString("F1");
            }
            else
            {
                nextWaveText.text = "Spawning Enemies...";
            }
        }

        if (currencySystem != null && goldText != null)
        {
            goldText.text = "Gold: " + currencySystem.GetCurrentGold();
        }
    }
}