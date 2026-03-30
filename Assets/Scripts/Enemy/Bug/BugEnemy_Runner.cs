using UnityEngine;

/// <summary>Chapter 1 Bug Runner: fast fragile bug unit (no special mechanics).</summary>
public class BugEnemy_Runner : BugEnemyBase
{
    [Header("Bug Runner")]
    public float hpMultiplier = 0.7f;
    public float speedMultiplier = 1.5f;

    void Reset()
    {
        bugType = "Runner";
    }

    protected override void Awake()
    {
        base.Awake();
        bugType = "Runner";
    }

    protected override void ApplyBugSpawnTuning()
    {
        bugType = "Runner";

        float hpMul = Mathf.Max(0.05f, hpMultiplier);
        float spdMul = Mathf.Max(0.05f, speedMultiplier);

        maxHealth *= hpMul;
        baseMoveSpeed *= spdMul;
    }
}
