using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI References (TextMeshProUGUI)")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI baseHpText;

    [Header("Data Sources (optional; will auto-find if empty)")]
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private BaseHealth baseHealth;

    private void Awake()
    {
        if (currencyManager == null) currencyManager = CurrencyManager.Instance;
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (baseHealth == null) baseHealth = FindObjectOfType<BaseHealth>();
    }

    private void OnEnable()
    {
        GameEvents.OnGoldChanged += HandleGoldChanged;
        GameEvents.OnWaveChanged += HandleWaveChanged;
        GameEvents.OnBaseHpChanged += HandleBaseHpChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnGoldChanged -= HandleGoldChanged;
        GameEvents.OnWaveChanged -= HandleWaveChanged;
        GameEvents.OnBaseHpChanged -= HandleBaseHpChanged;
    }

    private void Start()
    {
        RefreshAll();
    }

    private void Update()
    {
        // Fallback: if the project doesn't fire events yet, we still keep UI correct.
        RefreshWave();
        RefreshBaseHp();
    }

    private void RefreshAll()
    {
        RefreshGold();
        RefreshWave();
        RefreshBaseHp();
    }

    private void RefreshGold()
    {
        if (goldText == null) return;

        int value = currencyManager != null ? currencyManager.CurrentGold :
            (EconomyManager.Instance != null ? EconomyManager.Instance.CurrentGold : 0);

        goldText.text = $"金币: {value}";
    }

    private void RefreshWave()
    {
        if (waveText == null) return;
        if (enemySpawner == null) return;
        waveText.text = $"波次: {enemySpawner.GetCurrentWave()}";
    }

    private void RefreshBaseHp()
    {
        if (baseHpText == null) return;
        if (baseHealth == null) return;
        baseHpText.text = $"基地: {baseHealth.GetCurrentHealth()}";
    }

    private void HandleGoldChanged(int value)
    {
        if (goldText == null) return;
        goldText.text = $"金币: {value}";
    }

    private void HandleWaveChanged(int value)
    {
        if (waveText == null) return;
        waveText.text = $"波次: {value}";
    }

    private void HandleBaseHpChanged(int current, int max)
    {
        if (baseHpText == null) return;
        baseHpText.text = $"基地: {current}";
    }
}
