using TMPro;
using UnityEngine;

public class TopBarUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI baseHpText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI nextWaveText;

    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private EconomyManager economyManager;
    [SerializeField] private BaseHealth baseHealth;

    private void Update()
    {
        UpdateGold();
        UpdateWave();
        UpdateBaseHp();
    }

    private void UpdateGold()
    {
        if (economyManager != null && goldText != null)
        {
            goldText.text = $"金币: {economyManager.CurrentGold}";
        }
    }

    private void UpdateWave()
    {
        if (enemySpawner == null) return;

        if (waveText != null)
        {
            waveText.text = $"波次: {enemySpawner.GetCurrentWave()}";
        }

        if (nextWaveText != null)
        {
            if (enemySpawner.IsWaitingForNextWave())
            {
                nextWaveText.text = $"下一波: {enemySpawner.GetWaveTimer():0.0}s";
            }
            else
            {
                nextWaveText.text = "下一波: 战斗中";
            }
        }
    }

    private void UpdateBaseHp()
    {
        if (baseHealth != null && baseHpText != null)
        {
            baseHpText.text = $"基地: {baseHealth.GetCurrentHealth()}";
        }
    }
}