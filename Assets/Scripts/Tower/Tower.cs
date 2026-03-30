using UnityEngine;

public class Tower : MonoBehaviour
{
    private const string PrototypeSceneName = "Chapter1_Node1_Prototype";
    private const float PrototypeBasicDamageMultiplier = 0.75f;  // ~25% nerf
    private const float PrototypeSniperDamageMultiplier = 1.4f;  // +40% buff
    private const float PrototypeBasicVsBugShieldDamageMultiplier = 0.7f; // targeted nerf vs BugEnemy_Shield
    private const float PrototypeSniperVsBugShieldDamageMultiplier = 1.15f; // small targeted bonus vs BugEnemy_Shield

    // Prototype-only upgrade value compression (keeps base tower feel unchanged).
    private const float PrototypeUpgradeDamageGainMultiplier = 0.65f;   // reduce damage growth ~35%
    private const float PrototypeUpgradeIntervalReductionMultiplier = 0.6f; // reduce ROF gain ~40%
    [Header("Attack Settings")]
    public float attackRange = 5f;
    public float attackInterval = 1f;

    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletDamage = 2f;

    [Header("AoE Splash (0 radius = single-target)")]
    [Tooltip("World radius at main hit point; 0 disables splash.")]
    public float splashRadius = 0f;

    [Range(0f, 1.5f)]
    [Tooltip("Splash damage per target = bulletDamage * ratio (primary still takes full bulletDamage).")]
    public float splashDamageRatio = 0.65f;

    [Header("Sniper (optional)")]
    [Tooltip("If true (and not splash tower): pick Tank in range first, else highest current HP. Ignored for splash towers.")]
    public bool preferTankThenHighHp = false;

    [Tooltip("If > 0, bullet uses this speed; else a default of 12.")]
    public float bulletSpeed = 0f;

    private const float DefaultBulletSpeed = 12f;
    public const string AoeControlProjectilePoolKey = "Projectile_AOE_Control";

    [Header("Economy (base build cost snapshot for route pricing)")]
    public int upgradeCost = 50;
    public int sellValue = 20;

    [Header("Route upgrades (runtime)")]
    [SerializeField] TowerRouteKind selectedRoute = TowerRouteKind.None;
    [SerializeField] [Range(0, 2)] int routeLevel = 0;

    int _lastPaidRouteUpgradeGold;
    int _initialBuildCostSnapshot;
    float timer = 0f;
    Enemy _lastExecuteLogTarget;

    public TowerRouteKind SelectedRoute => selectedRoute;
    public int RouteLevel => routeLevel;

    void Awake()
    {
        _initialBuildCostSnapshot = Mathf.Max(1, upgradeCost);

        // Prototype-only role separation tuning (keeps Main behavior unchanged).
        if (IsPrototypeScene())
        {
            if (IsBasicTower)
                bulletDamage *= PrototypeBasicDamageMultiplier;
            else if (IsSniperTower)
                bulletDamage *= PrototypeSniperDamageMultiplier;
        }
    }

    private bool IsPrototypeScene()
    {
        return gameObject.scene.IsValid() &&
               string.Equals(gameObject.scene.name, PrototypeSceneName, System.StringComparison.Ordinal);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= attackInterval)
        {
            Attack();
            timer = 0f;
        }
    }

    void Attack()
    {
        if (firePoint == null)
            return;

        if (IsSplashTower)
        {
            if (selectedRoute == TowerRouteKind.B && routeLevel > 0)
                FireAoeControlShot();
            else
                FireStandardHomingBullet(FindNearestEnemy());
            return;
        }

        if (IsSniperTower && selectedRoute == TowerRouteKind.B && routeLevel > 0)
        {
            FireSniperPierceShot();
            return;
        }

        Enemy target = ResolveSniperSingleTarget();
        FireStandardHomingBullet(target);
    }

    Enemy ResolveSniperSingleTarget()
    {
        if (!IsSniperTower)
            return FindNearestEnemy();

        if (selectedRoute == TowerRouteKind.A)
            return FindExecutePriorityTarget();

        return PickSniperPriorityTargetLegacy();
    }

    void FireStandardHomingBullet(Enemy target)
    {
        if (target == null)
            return;

        GameObject bulletObj = PoolManager.Instance.Spawn(
            "Projectile_Bullet",
            firePoint.position,
            Quaternion.identity
        );

        if (bulletObj == null)
        {
            Debug.LogError("[Tower] Projectile_Bullet pool missing.");
            return;
        }

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
            return;

        bullet.damage = bulletDamage;
        bullet.splashRadius = splashRadius;
        bullet.splashDamage = splashRadius > 0.001f ? bulletDamage * splashDamageRatio : 0f;
        bullet.speed = bulletSpeed > 0.001f ? bulletSpeed : DefaultBulletSpeed;
        bullet.hitFxScale = 1f;
        bullet.prototypeBugShieldDamageMultiplier = 1f;
        if (IsPrototypeScene())
        {
            if (IsBasicTower) bullet.prototypeBugShieldDamageMultiplier = PrototypeBasicVsBugShieldDamageMultiplier;
            else if (IsSniperTower) bullet.prototypeBugShieldDamageMultiplier = PrototypeSniperVsBugShieldDamageMultiplier;
        }
        if (IsSplashTower && selectedRoute == TowerRouteKind.A && routeLevel > 0)
            bullet.hitFxScale = GetAoeBlastHitFxScale();
        ApplyControlToBullet(bullet);
        bullet.SetTarget(target);
    }

    void FireAoeControlShot()
    {
        Enemy target = FindNearestEnemy();
        if (target == null)
            return;

        GameObject bulletObj = PoolManager.Instance.Spawn(
            AoeControlProjectilePoolKey,
            firePoint.position,
            Quaternion.identity
        );

        if (bulletObj == null)
        {
            Debug.LogError("[Tower] Projectile_AOE_Control pool missing.");
            return;
        }

        Bullet.LogAoeControlProjectileOnce();

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
            return;

        float spd = bulletSpeed > 0.001f ? bulletSpeed : DefaultBulletSpeed;
        float dot = GetAoeControlDotPerTick();
        float zDur = GetAoeControlZoneDuration();
        float linger = GetAoeControlLingerAfterExit();
        float rad = GetAoeControlZoneRadius();

        bullet.ConfigureAoeControl(dot, zDur, linger, rad);
        bullet.damage = 0f;
        bullet.speed = spd;
        bullet.hitFxScale = 1f;
        bullet.SetTarget(target);
    }

    public float GetAoeBlastHitFxScale()
    {
        if (!IsSplashTower || selectedRoute != TowerRouteKind.A)
            return 1f;
        return routeLevel >= 2 ? 1.28f : 1.14f;
    }

    public float GetAoeControlDotPerTick()
    {
        if (!IsSplashTower || selectedRoute != TowerRouteKind.B || routeLevel < 1)
            return 0f;
        float coeff = routeLevel >= 2 ? 0.14f : 0.11f;
        return bulletDamage * coeff;
    }

    public float GetAoeControlZoneDuration()
    {
        if (!IsSplashTower || selectedRoute != TowerRouteKind.B || routeLevel < 1)
            return 0f;
        return routeLevel >= 2 ? 3f : 2f;
    }

    public float GetAoeControlLingerAfterExit()
    {
        if (!IsSplashTower || selectedRoute != TowerRouteKind.B || routeLevel < 1)
            return 0f;
        return routeLevel >= 2 ? 2f : 1.5f;
    }

    public float GetAoeControlZoneRadius()
    {
        return 1.28f;
    }

    void FireSniperPierceShot()
    {
        Enemy aim = FindNearestEnemy();
        if (aim == null)
            return;

        Vector3 dir = aim.transform.position - firePoint.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f)
            return;
        dir.Normalize();

        GameObject bulletObj = PoolManager.Instance.Spawn(
            "Projectile_Bullet",
            firePoint.position,
            Quaternion.identity
        );

        if (bulletObj == null)
        {
            Debug.LogError("[Tower] Projectile_Bullet pool missing.");
            return;
        }

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
            return;

        float spd = bulletSpeed > 0.001f ? bulletSpeed : DefaultBulletSpeed;
        int maxHits = GetSniperPierceMaxEnemyHits();
        bullet.prototypeBugShieldDamageMultiplier = 1f;
        if (IsPrototypeScene() && IsSniperTower)
            bullet.prototypeBugShieldDamageMultiplier = PrototypeSniperVsBugShieldDamageMultiplier;
        bullet.ConfigureSniperPierce(transform.position, attackRange, bulletDamage, maxHits, dir, spd);
    }

    public int GetSniperPierceMaxEnemyHits()
    {
        if (!IsSniperTower || selectedRoute != TowerRouteKind.B || routeLevel < 1)
            return 0;
        return routeLevel >= 2 ? 3 : 2;
    }

    void ApplyControlToBullet(Bullet bullet)
    {
        bullet.controlSlowMultiplier = 1f;
        bullet.controlSlowDuration = 0f;
        if (!IsBasicTower || selectedRoute != TowerRouteKind.B || routeLevel < 1)
            return;
        bullet.controlSlowMultiplier = routeLevel >= 2 ? 0.58f : 0.72f;
        bullet.controlSlowDuration = routeLevel >= 2 ? 2.5f : 2f;
    }

    public string GetRouteAButtonLabel()
    {
        if (IsSplashTower) return "Blast";
        if (IsSniperTower) return "Execute";
        return "Suppression";
    }

    public string GetRouteBButtonLabel()
    {
        if (IsSplashTower) return "Control";
        if (IsSniperTower) return "Pierce";
        return "Control";
    }

    public string GetRouteSummaryForUi()
    {
        if (selectedRoute == TowerRouteKind.None)
            return "None";
        string name = selectedRoute == TowerRouteKind.A ? GetRouteAButtonLabel() : GetRouteBButtonLabel();
        if (IsSplashTower)
            return $"{name} Lv{routeLevel}";
        return $"{name} L{routeLevel}";
    }

    public bool IsRouteFullyUpgraded()
    {
        return selectedRoute != TowerRouteKind.None && routeLevel >= 2;
    }

    public bool IsRouteButtonLocked(TowerRouteKind route)
    {
        if (selectedRoute == TowerRouteKind.None)
            return false;
        return selectedRoute != route;
    }

    public bool IsRouteButtonInteractable(TowerRouteKind route)
    {
        if (IsRouteFullyUpgraded())
            return false;
        if (selectedRoute == TowerRouteKind.None)
            return true;
        if (selectedRoute != route)
            return false;
        return routeLevel < 2;
    }

    public int GetNextRouteUpgradeCost()
    {
        if (IsRouteFullyUpgraded())
            return 0;
        if (selectedRoute == TowerRouteKind.None)
            return TowerRouteCostTemplate.FirstRouteUpgradeCost(_initialBuildCostSnapshot);
        if (routeLevel == 1)
            return TowerRouteCostTemplate.SecondRouteUpgradeCost(_lastPaidRouteUpgradeGold);
        return 0;
    }

    /// <summary>Legacy name used by UI; returns next route purchase cost.</summary>
    public int GetUpgradeCost() => GetNextRouteUpgradeCost();

    public int GetSellValue() => sellValue;

    public bool IsAtMaxLevel() => IsRouteFullyUpgraded();

    public void ApplyRouteUpgradeAfterPurchase(TowerRouteKind route, int goldPaid)
    {
        if (IsRouteFullyUpgraded())
            return;

        if (selectedRoute == TowerRouteKind.None)
        {
            selectedRoute = route;
            routeLevel = 1;
            if (!IsSniperTower && !IsSplashTower)
                LogRouteSelect(route);
        }
        else if (selectedRoute == route && routeLevel == 1)
        {
            routeLevel = 2;
            if (!IsSniperTower && !IsSplashTower)
                Debug.Log($"[TowerRoute] Upgrade route {route} to level 2");
        }

        _lastPaidRouteUpgradeGold = goldPaid;

        if (IsBasicTower)
            ApplyBasicRouteTier(route, routeLevel);
        else if (IsSniperTower)
            ApplySniperRouteTier(route, routeLevel);
        else if (IsSplashTower)
            ApplyAoeRouteTier(route, routeLevel);

        if (IsSniperTower)
            LogSniperRouteUpgrade(route, routeLevel);

        level++;
        sellValue += 14;
        upgradeCost = Mathf.Max(0, GetNextRouteUpgradeCost());
    }

    void LogSniperRouteUpgrade(TowerRouteKind route, int tier)
    {
        if (route == TowerRouteKind.A && tier == 1)
            Debug.Log("[SniperRoute] Select Execute A1");
        else if (route == TowerRouteKind.A && tier == 2)
            Debug.Log("[SniperRoute] Upgrade Execute to A2");
        else if (route == TowerRouteKind.B && tier == 1)
            Debug.Log("[SniperRoute] Select Pierce B1");
        else if (route == TowerRouteKind.B && tier == 2)
            Debug.Log("[SniperRoute] Upgrade Pierce to B2");
    }

    void LogRouteSelect(TowerRouteKind route)
    {
        string kind = IsBasicTower ? "Basic Tower" : IsSniperTower ? "Sniper Tower" : "AOE Tower";
        if (route == TowerRouteKind.A)
            Debug.Log($"[TowerRoute] Select route A for {kind}");
        else
            Debug.Log($"[TowerRoute] Select route B for {kind}");
    }

    void ApplyBasicRouteTier(TowerRouteKind route, int tier)
    {
        if (route == TowerRouteKind.A)
        {
            if (tier == 1)
            {
                bulletDamage += ProtoScaleDamageGain(1f);
                attackInterval = Mathf.Max(0.18f, attackInterval - ProtoScaleIntervalReduction(0.07f));
                attackRange += 0.35f;
            }
            else if (tier == 2)
            {
                bulletDamage += ProtoScaleDamageGain(1.25f);
                attackInterval = Mathf.Max(0.15f, attackInterval - ProtoScaleIntervalReduction(0.08f));
                attackRange += 0.45f;
            }
        }
        else
        {
            if (tier == 1)
            {
                bulletDamage += ProtoScaleDamageGain(0.4f);
                attackInterval = Mathf.Max(0.22f, attackInterval - ProtoScaleIntervalReduction(0.04f));
                attackRange += 0.2f;
            }
            else if (tier == 2)
            {
                bulletDamage += ProtoScaleDamageGain(0.5f);
                attackInterval = Mathf.Max(0.18f, attackInterval - ProtoScaleIntervalReduction(0.05f));
                attackRange += 0.25f;
            }
        }
    }

    void ApplySniperRouteTier(TowerRouteKind route, int tier)
    {
        if (route == TowerRouteKind.A)
        {
            if (tier == 1)
            {
                bulletDamage += ProtoScaleDamageGain(2.8f);
                attackRange += 1.25f;
                attackInterval = Mathf.Max(0.42f, attackInterval - ProtoScaleIntervalReduction(0.025f));
            }
            else if (tier == 2)
            {
                bulletDamage += ProtoScaleDamageGain(3.2f);
                attackRange += 1.45f;
                attackInterval = Mathf.Max(0.38f, attackInterval - ProtoScaleIntervalReduction(0.03f));
            }
        }
        else
        {
            if (tier == 1)
            {
                bulletDamage += ProtoScaleDamageGain(0.35f);
                attackRange += 0.35f;
            }
            else if (tier == 2)
            {
                bulletDamage += ProtoScaleDamageGain(0.45f);
                attackRange += 0.4f;
            }
        }
    }

    private float ProtoScaleDamageGain(float delta)
    {
        if (!IsPrototypeScene()) return delta;
        return delta * PrototypeUpgradeDamageGainMultiplier;
    }

    private float ProtoScaleIntervalReduction(float delta)
    {
        if (!IsPrototypeScene()) return delta;
        return delta * PrototypeUpgradeIntervalReductionMultiplier;
    }

    void ApplyAoeRouteTier(TowerRouteKind route, int tier)
    {
        if (route == TowerRouteKind.A)
        {
            if (tier == 1)
            {
                bulletDamage += 2f;
                splashRadius = Mathf.Min(splashRadius + 0.5f, 4.5f);
                splashDamageRatio = Mathf.Min(splashDamageRatio + 0.1f, 1.15f);
                Debug.Log("[AOERoute] Select Blast A1");
            }
            else if (tier == 2)
            {
                bulletDamage += 2.5f;
                splashRadius = Mathf.Min(splashRadius + 0.65f, 4.5f);
                splashDamageRatio = Mathf.Min(splashDamageRatio + 0.08f, 1.22f);
                Debug.Log("[AOERoute] Upgrade Blast to A2");
                Debug.Log("[AOERoute] Blast effect enhanced");
            }
        }
        else
        {
            if (tier == 1)
            {
                bulletDamage += 0.45f;
                Debug.Log("[AOERoute] Select Control B1");
            }
            else if (tier == 2)
            {
                bulletDamage += 0.55f;
                Debug.Log("[AOERoute] Upgrade Control to B2");
            }
        }
    }

    public bool IsSplashTower => splashRadius > 0.001f;

    public bool IsSniperTower => !IsSplashTower && preferTankThenHighHp;

    public bool IsBasicTower => !IsSplashTower && !IsSniperTower;

    [Header("Legacy display level (incremented on route tiers)")]
    public int level = 1;

    /// <summary>Route-none sniper: Tank &gt; highest HP &gt; nearest (no per-shot log).</summary>
    Enemy PickSniperPriorityTargetLegacy()
    {
        Enemy e = FindExecutePriorityTargetInternal(logChoice: false);
        return e;
    }

    /// <summary>Execute route: Tank &gt; highest current HP &gt; nearest in range.</summary>
    Enemy FindExecutePriorityTarget()
    {
        return FindExecutePriorityTargetInternal(logChoice: true);
    }

    Enemy FindExecutePriorityTargetInternal(bool logChoice)
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        Enemy bestTank = null;
        float bestTankHp = -1f;
        float bestTankDist = float.MaxValue;

        Enemy bestHighHpEnemy = null;
        float bestHighHpValue = -1f;
        float bestHighHpDist = float.MaxValue;

        Enemy nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
                continue;
            if (enemy.GetCurrentHealth() <= 0f)
                continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance >= attackRange)
                continue;

            float hp = enemy.GetCurrentHealth();

            if (distance < nearestDist)
            {
                nearestDist = distance;
                nearest = enemy;
            }

            if (enemy.GetInfoKind() == Enemy.InfoKind.Tank)
            {
                if (hp > bestTankHp || (Mathf.Approximately(hp, bestTankHp) && distance < bestTankDist))
                {
                    bestTankHp = hp;
                    bestTankDist = distance;
                    bestTank = enemy;
                }
            }

            if (hp > bestHighHpValue || (Mathf.Approximately(hp, bestHighHpValue) && distance < bestHighHpDist))
            {
                bestHighHpValue = hp;
                bestHighHpDist = distance;
                bestHighHpEnemy = enemy;
            }
        }

        Enemy chosen = bestTank != null ? bestTank : bestHighHpEnemy != null ? bestHighHpEnemy : nearest;
        if (logChoice && chosen != null && chosen != _lastExecuteLogTarget)
        {
            Debug.Log($"[SniperRoute] Execute target priority chose={chosen.gameObject.name}");
            _lastExecuteLogTarget = chosen;
        }

        return chosen;
    }

    Enemy FindNearestEnemy()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();

        Enemy nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Enemy enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < attackRange && distance < minDistance)
            {
                minDistance = distance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
