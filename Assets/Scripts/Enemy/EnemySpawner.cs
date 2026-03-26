using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject normalEnemyPrefab;
    public GameObject fastEnemyPrefab;
    public GameObject tankEnemyPrefab;
    public Transform spawnPoint;
    public Transform targetBase;

    [Header("Wave Settings")]
    public float spawnInterval = 1f;
    public float timeBetweenWaves = 5f;
    public int baseEnemyCount = 5;
    public int enemyIncreasePerWave = 3;

    [Header("Scaling Settings")]
    public float baseHealth = 10f;
    public float healthIncreasePerWave = 2f;
    public float baseSpeed = 3f;
    public float speedIncreasePerWave = 0.2f;

    private int currentWave = 0;
    private int enemiesToSpawn = 0;
    private int enemiesSpawnedThisWave = 0;

    private float spawnTimer = 0f;
    private float waveTimer = 0f;

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
        if (spawnPoint == null || targetBase == null || normalEnemyPrefab == null)
        {
            Debug.LogWarning("Spawner references are missing.");
            return;
        }

        GameObject selectedPrefab = GetEnemyPrefabForWave();

        GameObject enemyObj = Instantiate(selectedPrefab, spawnPoint.position, Quaternion.identity);

        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.SetTarget(targetBase);

            ApplyScaledStats(enemy, selectedPrefab);
        }

        enemiesSpawnedThisWave++;
    }

    private GameObject GetEnemyPrefabForWave()
    {
        int randomValue = Random.Range(0, 100);

        // 前几波只刷普通敌人
        if (currentWave <= 2)
        {
            return normalEnemyPrefab;
        }

        // 第3波开始加入快敌人
        if (currentWave <= 4)
        {
            if (randomValue < 70) return normalEnemyPrefab;
            return fastEnemyPrefab != null ? fastEnemyPrefab : normalEnemyPrefab;
        }

        // 第5波开始加入坦克敌人
        if (randomValue < 50) return normalEnemyPrefab;
        if (randomValue < 80) return fastEnemyPrefab != null ? fastEnemyPrefab : normalEnemyPrefab;
        return tankEnemyPrefab != null ? tankEnemyPrefab : normalEnemyPrefab;
    }

    private void ApplyScaledStats(Enemy enemy, GameObject prefabUsed)
    {
        float scaledHealth = baseHealth + (currentWave - 1) * healthIncreasePerWave;
        float scaledSpeed = baseSpeed + (currentWave - 1) * speedIncreasePerWave;

        int reward = 10;
        int damageToBase = 1;

        if (prefabUsed == fastEnemyPrefab)
        {
            scaledHealth *= 0.7f;
            scaledSpeed *= 1.5f;
            reward = 12;
        }
        else if (prefabUsed == tankEnemyPrefab)
        {
            scaledHealth *= 2f;
            scaledSpeed *= 0.7f;
            reward = 20;
            damageToBase = 2;
        }

        enemy.InitializeStats(scaledHealth, scaledSpeed, reward, damageToBase);
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
}