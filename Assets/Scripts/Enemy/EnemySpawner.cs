using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Pool Key")]
    public string normalEnemyKey = "Enemy_Basic";
    public string fastEnemyKey = "Enemy_Fast";
    public string tankEnemyKey = "Enemy_Tank";

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
    public float healthIncreasePerWave = 2f;
    public float speedIncreasePerWave = 0.2f;

    private int currentWave = 0;
    private int enemiesToSpawn = 0;
    private int enemiesSpawnedThisWave = 0;

    private float spawnTimer = 0f;
    private float waveTimer = 0f;
    private float waveDisplayCountdownRemaining = 0f;

    private bool isSpawningWave = false;
    private bool isWaitingForNextWave = true;

    private void Start()
    {
        StartNextWaveCountdown();
    }

    private void Update()
    {
        if (isWaitingForNextWave)
        {
            waveTimer -= Time.deltaTime;

            if (waveTimer <= 0f)
            {
                StartWave();
            }
            return;
        }

        if (waveDisplayCountdownSeconds > 0.001f)
        {
            waveDisplayCountdownRemaining -= Time.deltaTime;
            if (waveDisplayCountdownRemaining <= 0f)
            {
                waveDisplayCountdownRemaining = 0f;
                StartWave();
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
            float bonusHealth = (currentWave - 1) * healthIncreasePerWave;
            float bonusSpeed = (currentWave - 1) * speedIncreasePerWave;

            enemy.Initialize(pathPoints, bonusHealth, bonusSpeed);

            if (WaveManager.IsNightWave)
                enemy.ApplyNightBuff();
            else
                enemy.ResetNightBuff();
        }

        enemiesSpawnedThisWave++;
    }

    /// <summary>
    /// Deterministic spawn rhythm: basics early, fast enemies ramp in, tanks arrive later.
    /// </summary>
    private string GetEnemyKeyForSpawnIndex(int spawnIndexInWave)
    {
        int w = currentWave;

        if (w <= 4)
            return normalEnemyKey;

        if (w <= 7)
        {
            if (spawnIndexInWave % 5 == 4)
                return fastEnemyKey;
            return normalEnemyKey;
        }

        if (w <= 11)
        {
            switch (spawnIndexInWave % 7)
            {
                case 2:
                case 5:
                    return fastEnemyKey;
                case 6:
                    return tankEnemyKey;
                default:
                    return normalEnemyKey;
            }
        }

        int m = spawnIndexInWave % 9;
        if (m == 8)
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
    
}