using UnityEngine;

/// <summary>Chapter 1 Bug Shield: has a local shield layer that absorbs damage before HP.</summary>
public class BugEnemy_Shield : BugEnemyBase
{
    private const string PrototypeSceneName = "Chapter1_Node1_Prototype";
    private const float PrototypeDurabilityMultiplier = 1.4f; // +40% durability pressure

    [Header("Bug Shield")]
    [SerializeField] float shieldMax = 50f;
    [SerializeField] float shieldCurrent;
    [SerializeField] bool shieldActive = true;

    float _baseShieldMax;

    void Reset()
    {
        bugType = "Shield";
    }

    protected override void Awake()
    {
        base.Awake();
        bugType = "Shield";
        _baseShieldMax = shieldMax;
    }

    protected override void ApplyBugSpawnTuning()
    {
        bugType = "Shield";

        // Reset to prefab baseline each spawn to avoid compounding.
        shieldMax = _baseShieldMax;

        // Prototype-only durability bump (does not affect Main scene).
        if (IsPrototypeScene())
        {
            maxHealth *= PrototypeDurabilityMultiplier;
            shieldMax *= PrototypeDurabilityMultiplier;
        }

        shieldActive = true;
        shieldCurrent = Mathf.Max(0f, shieldMax);
    }

    bool IsPrototypeScene()
    {
        return gameObject.scene.IsValid() &&
               string.Equals(gameObject.scene.name, PrototypeSceneName, System.StringComparison.Ordinal);
    }

    public override void TakeDamage(float damage)
    {
        if (damage <= 0f) return;

        if (shieldActive && shieldCurrent > 0f)
        {
            float toShield = Mathf.Min(damage, shieldCurrent);
            shieldCurrent -= toShield;
            if (toShield > 0f)
            {
                Debug.Log($"[Shield] [BugEnemy] Shield took damage amount={toShield:0.##}");
                OnShieldHit();
            }

            if (shieldCurrent <= 0f)
            {
                shieldActive = false;
                Debug.Log("[ShieldBreak] [BugEnemy] Shield broken");
                OnShieldBreak();
            }

            float remaining = damage - toShield;
            if (remaining > 0f)
            {
                Debug.Log($"[BugEnemy] Overflow to HP amount={remaining:0.##}");
                base.TakeDamage(remaining);
            }

            return;
        }

        base.TakeDamage(damage);
    }

    // Reserved hooks for future VFX integration.
    protected virtual void OnShieldHit() { }
    protected virtual void OnShieldBreak() { }
}

