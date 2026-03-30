using UnityEngine;

/// <summary>
/// Chapter 1 Bug faction base. Minimal wrapper over <see cref="Enemy"/> to keep changes localized.
/// </summary>
public class BugEnemyBase : Enemy
{
    private const string PrototypeSceneName = "Chapter1_Node1_Prototype";

    [Header("Bug Base")]
    public string bugType;

    float _baseMaxHealth;
    float _baseMoveSpeed;
    int _baseDamageToBase;
    int _baseRewardGold;

    protected override void Awake()
    {
        base.Awake();

        // Cache inspector baselines so pooled spawns can re-apply multipliers safely.
        _baseMaxHealth = maxHealth;
        _baseMoveSpeed = baseMoveSpeed;
        _baseDamageToBase = damageToBase;
        _baseRewardGold = rewardGold;

        // Ensure Enemy's cached base stats are valid before any Initialize() usage.
        RefreshCachedBaseStatsFromInspector();
    }

    /// <summary>Called by pool before spawner <see cref="Enemy.Initialize"/>.</summary>
    public new void OnSpawn()
    {
        // Reset to prefab baselines each spawn (avoid multiplier compounding).
        maxHealth = _baseMaxHealth;
        baseMoveSpeed = _baseMoveSpeed;
        damageToBase = _baseDamageToBase;
        rewardGold = _baseRewardGold;

        ApplyBugSpawnTuning();
        ApplyPrototypeRewardTuning();
        RefreshCachedBaseStatsFromInspector();

        base.OnSpawn();
    }

    private void ApplyPrototypeRewardTuning()
    {
        if (!gameObject.scene.IsValid() ||
            !string.Equals(gameObject.scene.name, PrototypeSceneName, System.StringComparison.Ordinal))
            return;

        // Prototype-only kill reward curve (gentle reduction).
        // Prefab baselines (current): Grunt=10, Runner=11, Shield=13.
        switch (bugType)
        {
            case "Grunt":
                rewardGold = 6;  // ~40% reduction
                break;
            case "Runner":
                rewardGold = 8;  // ~27% reduction
                break;
            case "Shield":
                rewardGold = 12; // ~8% reduction
                break;
        }
    }

    /// <summary>Override in derived bug units to apply stat multipliers safely.</summary>
    protected virtual void ApplyBugSpawnTuning()
    {
    }
}

