using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Content-only pressure variant for Chapter1_Node2.
/// Adds earlier Runner/Shield presence using existing prefabs/pools and Enemy.Initialize, without modifying EnemySpawner.
/// </summary>
public class Chapter1Node2PressureWaves : MonoBehaviour
{
    private const string Node2SceneName = "Chapter1_Node2";

    private EnemySpawner _spawner;
    private int _lastWaveInjected = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePresent()
    {
        var s = SceneManager.GetActiveScene();
        if (!s.IsValid() || !string.Equals(s.name, Node2SceneName, System.StringComparison.Ordinal))
            return;

        if (Object.FindObjectOfType<Chapter1Node2PressureWaves>() != null)
            return;

        var go = new GameObject("Chapter1_Node2_PressureWaves");
        go.hideFlags = HideFlags.DontSave;
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Chapter1Node2PressureWaves>();
    }

    private void Awake()
    {
        var s = SceneManager.GetActiveScene();
        if (!s.IsValid() || !string.Equals(s.name, Node2SceneName, System.StringComparison.Ordinal))
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        if (_spawner == null)
            _spawner = FindObjectOfType<EnemySpawner>();
        if (_spawner == null)
            return;

        int w = _spawner.GetCurrentWave();
        if (w <= 0 || w == _lastWaveInjected)
            return;

        InjectPressureForWave(w);
        _lastWaveInjected = w;
    }

    private void InjectPressureForWave(int wave)
    {
        // Target shape (pressure node):
        // 1: mostly Grunt + tiny Runner presence
        // 2-3: increasing Runner pressure
        // 4: introduce early Shield
        // 5-8: increasingly mixed Runner/Shield pressure
        int extraRunners = 0;
        int extraShields = 0;

        switch (wave)
        {
            case 1: extraRunners = 1; break;
            case 2: extraRunners = 2; break;
            case 3: extraRunners = 3; break;
            case 4: extraRunners = 2; extraShields = 1; break;
            case 5: extraRunners = 3; extraShields = 2; break;
            case 6: extraRunners = 4; extraShields = 3; break;
            case 7: extraRunners = 5; extraShields = 4; break;
            case 8: extraRunners = 6; extraShields = 5; break;
        }

        for (int i = 0; i < extraRunners; i++)
            SpawnEnemyLikeSpawner(GetRunnerKey(), wave);

        for (int i = 0; i < extraShields; i++)
            SpawnEnemyLikeSpawner(GetShieldKey(), wave);
    }

    private string GetRunnerKey()
    {
        if (_spawner != null && !string.IsNullOrWhiteSpace(_spawner.bugRunnerKey))
            return _spawner.bugRunnerKey;
        return "Enemy_Bug_Runner";
    }

    private string GetShieldKey()
    {
        if (_spawner != null && !string.IsNullOrWhiteSpace(_spawner.bugShieldKey))
            return _spawner.bugShieldKey;
        return "Enemy_Bug_Shield";
    }

    private void SpawnEnemyLikeSpawner(string key, int waveIndex)
    {
        if (_spawner == null || _spawner.spawnPoint == null || _spawner.pathPoints == null || _spawner.pathPoints.Length == 0)
            return;

        var obj = PoolManager.Instance != null
            ? PoolManager.Instance.Spawn(key, _spawner.spawnPoint.position, Quaternion.identity)
            : null;

        if (obj == null)
            return;

        var enemy = obj.GetComponent<Enemy>();
        if (enemy == null)
            enemy = obj.GetComponentInChildren<Enemy>(true);
        if (enemy == null)
            return;

        ComputeWaveBonusesForKey(key, waveIndex, out float bonusHealth, out int bonusDamage);
        enemy.Initialize(_spawner.pathPoints, bonusHealth, bonusDamage);

        if (WaveManager.IsNightWave)
            enemy.ApplyNightBuff();
        else
            enemy.ResetNightBuff();
    }

    private void ComputeWaveBonusesForKey(string key, int waveIndex, out float bonusHealth, out int bonusDamageToBase)
    {
        int wavesPassed = Mathf.Max(0, waveIndex - 1);

        bool isShield = _spawner != null && key == _spawner.shieldEnemyKey;
        bool isFast = _spawner != null && (key == _spawner.fastEnemyKey || key == _spawner.bugRunnerKey);
        bool isTank = _spawner != null && key == _spawner.tankEnemyKey;

        float normalHpPerWave = _spawner != null ? _spawner.normalHealthPerWave : 0f;
        float fastHpMul = _spawner != null ? _spawner.fastHealthMultiplier : 1f;
        float tankHpMul = _spawner != null ? _spawner.tankHealthMultiplier : 1f;

        float normalDmgPerWave = _spawner != null ? _spawner.normalDamagePerWave : 0f;
        float fastDmgPerWave = _spawner != null ? _spawner.fastDamagePerWave : 0f;
        float tankDmgPerWave = _spawner != null ? _spawner.tankDamagePerWave : 0f;

        if (isShield)
        {
            bonusHealth = wavesPassed * normalHpPerWave;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * normalDmgPerWave);
        }
        else if (isFast)
        {
            bonusHealth = wavesPassed * normalHpPerWave * fastHpMul;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * fastDmgPerWave);
        }
        else if (isTank)
        {
            bonusHealth = wavesPassed * normalHpPerWave * tankHpMul;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * tankDmgPerWave);
        }
        else
        {
            bonusHealth = wavesPassed * normalHpPerWave;
            bonusDamageToBase = Mathf.FloorToInt(wavesPassed * normalDmgPerWave);
        }
    }
}

