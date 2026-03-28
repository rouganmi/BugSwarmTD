using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemySpawner : MonoBehaviour
{
    [Header("Pool Key")]
    public string normalEnemyKey = "Enemy_Basic";
    public string fastEnemyKey = "Enemy_Fast";
    public string tankEnemyKey = "Enemy_Tank";
    public string shieldEnemyKey = "Enemy_Shield";

    [Header("References")]
    public Transform spawnPoint;
    public Transform[] pathPoints;

    [Header("Wave Settings")]
    public float spawnInterval = 1f;
    public float timeBetweenWaves = 5f;
    public int baseEnemyCount = 5;
    public int enemyIncreasePerWave = 3;

    [Header("HUD + auto next wave")]
    [Tooltip("Countdown shown while a wave is active. When it hits 0, the next wave starts (previous enemies may remain).")]
    public float waveDisplayCountdownSeconds = 25f;

    [Header("Scaling Settings")]
    [Tooltip("Baseline HP bonus per past wave (Normal). Fast/Tank multiply this.")]
    [FormerlySerializedAs("healthIncreasePerWave")]
    public float normalHealthPerWave = 2f;

    [Range(0.2f, 1f)]
    [Tooltip("Multiplies Normal HP-per-wave bonus for Fast (low HP growth).")]
    public float fastHealthMultiplier = 0.48f;

    [Range(1f, 2.5f)]
    [Tooltip("Multiplies Normal HP-per-wave bonus for Tank (high HP growth).")]
    public float tankHealthMultiplier = 1.65f;

    [Tooltip("Bonus damage to base per past wave (Normal), before flooring.")]
    public float normalDamagePerWave = 0.35f;

    [Tooltip("Bonus damage to base per past wave (Fast); kept low so Fast stays fragile.")]
    public float fastDamagePerWave = 0.18f;

    [Tooltip("Bonus damage to base per past wave (Tank); medium-high.")]
    public float tankDamagePerWave = 0.42f;

    [Header("Night wave lead-in (optional)")]
    [Tooltip("Real-time seconds to wait before starting a night wave (7, 14, …). 0 = no delay.")]
    public float nightLeadInSeconds = 0f;

    [Tooltip("If true, a night wave waits until ConfirmNightWaveStart() is called (after lead-in). Wire a UI button to it.")]
    public bool requireConfirmationForNightWave = false;

    private int currentWave = 0;
    private int enemiesToSpawn = 0;
    private int enemiesSpawnedThisWave = 0;

    private float spawnTimer = 0f;
    private float waveTimer = 0f;
    private float waveDisplayCountdownRemaining = 0f;

    private bool isSpawningWave = false;
    private bool isWaitingForNextWave = true;

    private bool _nightSequenceActive;
    private bool _nightStartConfirmed;

    private void Start()
    {
        StartNextWaveCountdown();
    }

    private void Update()
    {
        if (_nightSequenceActive)
            return;

        if (isWaitingForNextWave)
        {
            waveTimer -= Time.deltaTime;

            if (waveTimer <= 0f)
            {
                TryScheduleNextWave();
            }
            return;
        }

        if (waveDisplayCountdownSeconds > 0.001f)
        {
            waveDisplayCountdownRemaining -= Time.deltaTime;
            if (waveDisplayCountdownRemaining <= 0f)
            {
                waveDisplayCountdownRemaining = 0f;
                TryScheduleNextWave();
            }
        }
        else
        {
            waveDisplayCountdownRemaining = 0f;
        }

        if (isSpawningWave)
        {
            spawnTimer += Time.deltaTime;

            if (enemiesSpawnedThisWave < enemiesToSpawn && spawnTimer >= spawnInterval)
            {
                SpawnEnemy();
                spawnTimer = 0f;
            }

            if (enemiesSpawnedThisWave >= enemiesToSpawn)
            {
                isSpawningWave = false;
            }
        }
        else
        {
            if (FindObjectsOfType<Enemy>().Length == 0)
            {
                StartNextWaveCountdown();
            }
        }
    }

    /// <summary>
    /// If the next wave is a night wave and lead-in or confirmation is enabled, runs that flow; otherwise starts the wave immediately.
    /// </summary>
    private void TryScheduleNextWave()
    {
        int nextWave = currentWave + 1;
        bool useGate = WaveManager.IsNightWaveIndex(nextWave) &&
                       (nightLeadInSeconds > 0.01f || requireConfirmationForNightWave);

        if (useGate)
        {
            StartCoroutine(NightLeadInThenStartWaveRoutine(nextWave));
            return;
        }

        StartWave();
    }

    private IEnumerator NightLeadInThenStartWaveRoutine(int upcomingWave)
    {
        _nightSequenceActive = true;
        GameEvents.OnNightWaveLeadIn?.Invoke(upcomingWave);

        if (nightLeadInSeconds > 0.01f)
            yield return new WaitForSecondsRealtime(nightLeadInSeconds);

        if (requireConfirmationForNightWave)
        {
            _nightStartConfirmed = false;
            while (!_nightStartConfirmed)
                yield return null;
        }

        _nightSequenceActive = false;
        StartWave();
    }

    /// <summary>Call from a UI button when <see cref="requireConfirmationForNightWave"/> is true.</summary>
    public void ConfirmNightWaveStart()
    {
        _nightStartConfirmed = true;
    }

    private void StartWave()
    {
        currentWave++;
        enemiesToSpawn = baseEnemyCount + (currentWave - 1) * enemyIncreasePerWave;
        enemiesSpawnedThisWave = 0;
        spawnTimer = 0f;

        isSpawningWave = true;
        isWaitingForNextWave = false;

        waveDisplayCountdownRemaining = Mathf.Max(0f, waveDisplayCountdownSeconds);

        WaveManager.NotifyWaveStarted(currentWave);

        GameEvents.OnWaveChanged?.Invoke(currentWave);

        Enemy.SyncNightBuffsWithWaveManager();

        Debug.Log("Wave " + currentWave + " started. Enemies: " + enemiesToSpawn);
    }

    private void StartNextWaveCountdown()
    {
        waveTimer = timeBetweenWaves;
        isWaitingForNextWave = true;

        Debug.Log("Next wave in " + timeBetweenWaves + " seconds...");
    }

    private void SpawnEnemy()
    {
        if (spawnPoint == null || pathPoints == null || pathPoints.Length == 0)
        {
            Debug.LogWarning("Spawner references missing.");
            return;
        }

        string key = GetEnemyKeyForSpawnIndex(enemiesSpawnedThisWave);

        GameObject enemyObj = PoolManager.Instance.Spawn(
            key,
            spawnPoint.position,
            Quaternion.identity
        );

        if (enemyObj == null)
        {
            Debug.LogError("对象池不存在: " + key);
            return;
        }

        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy != null)
        {
            GetWaveBonusesForKey(key, out float bonusHealth, out int bonusDamage);

            enemy.Initialize(pathPoints, bonusHealth, bonusDamage);

            if (WaveManager.IsNightWave)
                enemy.ApplyNightBuff();
            else
                enemy.ResetNightBuff();
        }

        enemiesSpawnedThisWave++;
    }

    /// <summary>
    /// Per-type HP and damage growth. Move speed does not scale with waves (only night buff may multiply speed).
    /// </summary>
    private void GetWaveBonusesForKey(string key, out float bonusHealth, out int bonusDamageToBase)
    {
        int wavesPassed = Mathf.Max(0, currentWave - 1);

        if (key == shieldEnemyKey)
        {
            bonusHealth = wavesPassed * normalHealthPerWave;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * normalDamagePerWave);
        }
        else if (key == fastEnemyKey)
        {
            bonusHealth = wavesPassed * normalHealthPerWave * fastHealthMultiplier;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * fastDamagePerWave);
        }
        else if (key == tankEnemyKey)
        {
            bonusHealth = wavesPassed * normalHealthPerWave * tankHealthMultiplier;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * tankDamagePerWave);
        }
        else
        {
            bonusHealth = wavesPassed * normalHealthPerWave;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * normalDamagePerWave);
        }
    }

    /// <summary>
    /// Deterministic spawn rhythm: basics early, fast enemies ramp in, tanks arrive later.
    /// </summary>
    private string GetEnemyKeyForSpawnIndex(int spawnIndexInWave)
    {
        int w = currentWave;

        if (w >= 5)
        {
            if (spawnIndexInWave == 0)
                return shieldEnemyKey;
            if (w % 2 == 0 && spawnIndexInWave == 4)
                return shieldEnemyKey;
        }

        if (w <= 4)
            return normalEnemyKey;

        if (w <= 7)
        {
            if (spawnIndexInWave % 5 == 4)
                return fastEnemyKey;
            return normalEnemyKey;
        }

        // Waves 8–11: fast mix-in; tanks only from wave 10 (later waves, still light).
        if (w <= 11)
        {
            switch (spawnIndexInWave % 7)
            {
                case 2:
                case 5:
                    return fastEnemyKey;
                case 6:
                    return w >= 10 ? tankEnemyKey : normalEnemyKey;
                default:
                    return normalEnemyKey;
            }
        }

        // Late waves: tanks stay a minority (~1 per 11 spawns); fast unchanged.
        int m = spawnIndexInWave % 11;
        if (m == 10)
            return tankEnemyKey;
        if (m == 1 || m == 4 || m == 6)
            return fastEnemyKey;
        return normalEnemyKey;
    }

    public int GetCurrentWave()
    {
        return currentWave;
    }

    public bool IsWaitingForNextWave()
    {
        return isWaitingForNextWave;
    }

    public float GetWaveTimer()
    {
        return waveTimer;
    }

    /// <summary>Per-wave countdown (HUD). While a wave is active it counts down; at 0, StartWave runs.</summary>
    public float GetWaveDisplayCountdownRemaining()
    {
        return waveDisplayCountdownRemaining;
    }

    /// <summary>True while lead-in wait or confirmation gate is running (night wave not started yet).</summary>
    public bool IsNightSequenceActive => _nightSequenceActive;
    
}