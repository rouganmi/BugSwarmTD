using UnityEngine;

/// <summary>Chapter 1 Bug Grunt: baseline bug unit (no special behavior).</summary>
public class BugEnemy_Grunt : BugEnemyBase
{
    [Header("Bug Grunt")]
    [SerializeField] float hpMultiplier = 1f;
    [SerializeField] float speedMultiplier = 1f;

    void Reset()
    {
        bugType = "Grunt";
    }

    protected override void Awake()
    {
        base.Awake();
        bugType = "Grunt";
    }

    protected override void ApplyBugSpawnTuning()
    {
        bugType = "Grunt";

        float hpMul = Mathf.Max(0.05f, hpMultiplier);
        float spdMul = Mathf.Max(0.05f, speedMultiplier);

        maxHealth *= hpMul;
        baseMoveSpeed *= spdMul;
    }
}

