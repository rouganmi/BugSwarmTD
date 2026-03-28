using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{
    private const string HitEffectPoolKey = "Effect_Hit";
    const float PierceSweepRadius = 0.65f;
    public float speed = 10f;
    public float damage = 1f;

    [HideInInspector] public float splashRadius;
    [HideInInspector] public float splashDamage;
    [HideInInspector] public float controlSlowMultiplier = 1f;
    [HideInInspector] public float controlSlowDuration;

    Enemy target;
    bool isActiveBullet;

    bool _sniperPierceActive;
    Vector3 _sniperTowerPos;
    float _sniperEffectiveRange;
    float _sniperBaseDamage;
    int _sniperMaxUniqueHits;
    Vector3 _sniperDir;
    HashSet<int> _sniperHitIds;

    bool _aoeControlActive;
    float _aoeDotPerTick;
    float _aoeZoneDuration;
    float _aoeLingerSec;
    float _aoeZoneRadius;
    Transform _aoeSpinVisual;
    Vector3 _aoeSpinEulerPerSec;

    static bool _loggedAoeControlProjectile;
    static bool _loggedRestoredSharedVisuals;

    /// <summary>Must match <c>Bullet.prefab</c> root <c>localScale</c> (shared pool only).</summary>
    public static readonly Vector3 SharedProjectileVisualScale = new Vector3(0.3f, 0.3f, 0.3f);

    /// <summary>Scales pooled hit VFX (Blast route uses &gt;1).</summary>
    public float hitFxScale = 1f;

    internal static void LogAoeControlProjectileOnce()
    {
        if (_loggedAoeControlProjectile)
            return;
        _loggedAoeControlProjectile = true;
        Debug.Log("[AOEControl] Using exclusive barrel projectile visual");
    }

    /// <summary>Shared <c>Projectile_Bullet</c> uses prefab scale; Control prefab uses scale 1,1,1.</summary>
    void ApplyPoolDefaultRootScale()
    {
        var po = GetComponent<PoolObject>();
        if (po != null && po.poolKey == Tower.AoeControlProjectilePoolKey)
        {
            transform.localScale = Vector3.one;
            return;
        }

        transform.localScale = SharedProjectileVisualScale;
        if (!_loggedRestoredSharedVisuals)
        {
            _loggedRestoredSharedVisuals = true;
            Debug.Log("[AOEControl] Restored shared projectile visuals");
        }
    }

    void SetupAoeControlBarrelMaterials()
    {
        Transform root = transform.Find("BarrelVisual");
        if (root == null)
            return;
        var bodyR = root.GetComponent<MeshRenderer>();
        if (bodyR != null)
            bodyR.sharedMaterial = AoeControlProjectileMaterials.Body;

        Transform accent = root.Find("AccentBand");
        if (accent != null)
        {
            var ar = accent.GetComponent<MeshRenderer>();
            if (ar != null)
                ar.sharedMaterial = AoeControlProjectileMaterials.Accent;
        }
    }

    static class AoeControlProjectileMaterials
    {
        public static readonly Material Body = CreateBody();
        public static readonly Material Accent = CreateAccent();

        static Material CreateBody()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.name = "AOEControl_BarrelBody_Runtime";
            Color c = new Color(0x7A / 255f, 0x2e / 255f, 0x1f / 255f, 1f);
            Color edge = new Color(0x2b / 255f, 0x1a / 255f, 0x14 / 255f, 1f);
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", Color.Lerp(c, edge, 0.12f));
            else
                m.color = c;
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", 0.25f);
            return m;
        }

        static Material CreateAccent()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.name = "AOEControl_BarrelAccent_Runtime";
            Color c = new Color(0xd8 / 255f, 0xa2 / 255f, 0x27 / 255f, 1f);
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            else
                m.color = c;
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", 0.55f);
            return m;
        }
    }

    public void SetTarget(Enemy newTarget)
    {
        _sniperPierceActive = false;
        target = newTarget;
        isActiveBullet = true;
    }

    /// <summary>AOE Route B: homing shot using exclusive prefab; impact spawns a ground control zone (no splash).</summary>
    public void ConfigureAoeControl(float dotPerTick, float zoneDurationSec, float lingerAfterExitSec, float zoneRadius)
    {
        _sniperPierceActive = false;
        _aoeControlActive = true;
        _aoeDotPerTick = dotPerTick;
        _aoeZoneDuration = zoneDurationSec;
        _aoeLingerSec = lingerAfterExitSec;
        _aoeZoneRadius = zoneRadius;
        transform.localScale = Vector3.one;
        splashRadius = 0f;
        splashDamage = 0f;
        controlSlowMultiplier = 1f;
        controlSlowDuration = 0f;
        _aoeSpinVisual = transform.Find("BarrelVisual");
        if (_aoeSpinVisual == null && transform.childCount > 0)
            _aoeSpinVisual = transform.GetChild(0);
        _aoeSpinEulerPerSec = new Vector3(
            Random.Range(-160f, 160f),
            Random.Range(-160f, 160f),
            Random.Range(-160f, 160f));
        SetupAoeControlBarrelMaterials();
        isActiveBullet = true;
    }

    /// <summary>Linear pierce shot for Sniper Route B. Does not use <see cref="SetTarget"/> homing.</summary>
    public void ConfigureSniperPierce(Vector3 towerWorldPos, float effectiveRange, float baseDamage, int maxUniqueEnemies,
        Vector3 fireDirection, float moveSpeed)
    {
        target = null;
        _aoeControlActive = false;
        _sniperPierceActive = true;
        _sniperTowerPos = towerWorldPos;
        _sniperEffectiveRange = Mathf.Max(0.1f, effectiveRange);
        _sniperBaseDamage = baseDamage;
        _sniperMaxUniqueHits = Mathf.Max(1, maxUniqueEnemies);
        Vector3 d = fireDirection;
        d.y = 0f;
        if (d.sqrMagnitude < 1e-6f)
            d = Vector3.forward;
        _sniperDir = d.normalized;
        speed = moveSpeed;
        damage = baseDamage;
        if (_sniperHitIds == null)
            _sniperHitIds = new HashSet<int>(8);
        else
            _sniperHitIds.Clear();

        isActiveBullet = true;
    }

    void Update()
    {
        if (!isActiveBullet)
            return;

        if (_sniperPierceActive)
        {
            UpdateSniperPierce();
            return;
        }

        if (target == null)
        {
            ReturnToPool();
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.transform.position,
            speed * Time.deltaTime
        );

        if (_aoeControlActive && _aoeSpinVisual != null)
            _aoeSpinVisual.Rotate(_aoeSpinEulerPerSec * Time.deltaTime, Space.Self);

        if (Vector3.Distance(transform.position, target.transform.position) < 0.15f)
            HitTarget();
    }

    void UpdateSniperPierce()
    {
        float dt = Time.deltaTime;
        float stepLen = speed * dt;
        Vector3 nextPos = transform.position + _sniperDir * stepLen;

        float distNext = Vector3.Distance(_sniperTowerPos, nextPos);
        if (distNext > _sniperEffectiveRange * 1.2f + 0.02f)
        {
            Debug.Log("[SniperRoute] Pierce projectile expired reason=range");
            ReturnToPool();
            return;
        }

        Vector3 segFrom = transform.position;
        Vector3 segTo = nextPos;
        CheckPierceHitsAlongSegment(segFrom, segTo);

        transform.position = nextPos;

        if (_sniperHitIds != null && _sniperHitIds.Count >= _sniperMaxUniqueHits)
        {
            Debug.Log("[SniperRoute] Pierce projectile expired reason=pierce_limit");
            ReturnToPool();
        }
    }

    void CheckPierceHitsAlongSegment(Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float segLen = ab.magnitude;
        if (segLen < 1e-5f)
            return;

        Vector3 dir = ab / segLen;
        const int mask = ~0;

        // OverlapCapsule covers the full motion segment (handles start-inside-collider and thin frames
        // better than SphereCast-only). Same mask as splash (~0) so custom layers still register.
        Collider[] cols = Physics.OverlapCapsule(a, b, PierceSweepRadius, mask, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0)
            return;

        var bestAlong = new Dictionary<int, (Enemy e, float along)>(cols.Length);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null)
                continue;
            Enemy e = c.GetComponentInParent<Enemy>();
            if (e == null || !e.isActiveAndEnabled)
                continue;
            if (e.GetCurrentHealth() <= 0f)
                continue;

            Vector3 mid = a + dir * (segLen * 0.5f);
            Vector3 onCol = c.ClosestPoint(mid);
            Vector3 pOnSeg = ClosestPointOnSegment(a, b, onCol);
            float along = Vector3.Dot(pOnSeg - a, dir);
            if (along < -0.25f || along > segLen + 0.25f)
                continue;

            int id = e.GetInstanceID();
            if (!bestAlong.TryGetValue(id, out var cur) || along < cur.along)
                bestAlong[id] = (e, along);
        }

        if (bestAlong.Count == 0)
            return;

        var ordered = new List<(Enemy e, float along)>(bestAlong.Count);
        foreach (var kv in bestAlong)
            ordered.Add(kv.Value);
        ordered.Sort((x, y) => x.along.CompareTo(y.along));

        for (int i = 0; i < ordered.Count; i++)
        {
            TryApplyPierceToEnemy(ordered[i].e);
            if (_sniperHitIds != null && _sniperHitIds.Count >= _sniperMaxUniqueHits)
                return;
        }
    }

    static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    void TryApplyPierceToEnemy(Enemy e)
    {
        if (e == null || _sniperHitIds == null)
            return;

        int id = e.GetInstanceID();
        if (_sniperHitIds.Contains(id))
        {
            Debug.Log($"[SniperRoute] Pierce skip duplicate enemy={e.gameObject.name}");
            return;
        }

        float dTower = Vector3.Distance(_sniperTowerPos, e.transform.position);
        if (dTower > _sniperEffectiveRange * 1.2f + 0.05f)
            return;

        float mult = dTower <= _sniperEffectiveRange ? 1f : 0.2f;
        float amount = _sniperBaseDamage * mult;

        Debug.Log($"[SniperRoute] Pierce contact with enemy={e.gameObject.name}");
        Debug.Log($"[SniperRoute] Pierce apply damage enemy={e.gameObject.name} amount={amount:0.##}");

        e.TakeDamage(amount);
        _sniperHitIds.Add(id);

        Vector3 fxPos = e.transform.position + Vector3.up * 0.45f;
        Debug.Log($"[SniperRoute] Pierce spawn hit effect enemy={e.gameObject.name}");
        SpawnHitEffectAt(fxPos);
    }

    void HitTarget()
    {
        if (_aoeControlActive)
        {
            HitAoeControl();
            return;
        }

        Vector3 hitPos = transform.position;

        if (target != null)
        {
            hitPos = target.transform.position;
            target.TakeDamage(damage);
            if (controlSlowDuration > 0.01f && controlSlowMultiplier < 1f)
                target.ApplyControlSlow(controlSlowMultiplier, controlSlowDuration);
        }

        if (splashRadius > 0.001f && splashDamage > 0f)
            ApplySplashDamage(hitPos, target);

        SpawnHitEffectAt(hitPos + Vector3.up * 0.45f);
        ReturnToPool();
    }

    void HitAoeControl()
    {
        Vector3 aim = target != null ? target.transform.position : transform.position;
        Vector3 ground = ResolveGroundHitPoint(aim);
        AreaZone.Spawn(ground, _aoeZoneRadius, _aoeZoneDuration, _aoeDotPerTick, _aoeLingerSec);
        SpawnHitEffectAt(ground + Vector3.up * 0.12f);
        ReturnToPool();
    }

    static Vector3 ResolveGroundHitPoint(Vector3 reference)
    {
        Vector3 origin = reference + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        Vector3 p = reference;
        p.y = 0f;
        return p;
    }

    void SpawnHitEffectAt(Vector3 spawnPos)
    {
        if (PoolManager.Instance == null)
            return;
        GameObject fx = PoolManager.Instance.Spawn(HitEffectPoolKey, spawnPos, Quaternion.identity);
        if (fx == null)
        {
            Debug.LogWarning($"[Bullet] Hit effect spawn failed for key: {HitEffectPoolKey}");
            return;
        }

        fx.transform.localScale = Vector3.one * hitFxScale;

        var ps = fx.GetComponentInChildren<ParticleSystem>(true);
        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    void ApplySplashDamage(Vector3 center, Enemy primary)
    {
        Collider[] cols = Physics.OverlapSphere(center, splashRadius, ~0, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0)
            return;

        var applied = new HashSet<Enemy>();
        if (primary != null)
            applied.Add(primary);

        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null)
                continue;
            Enemy en = cols[i].GetComponentInParent<Enemy>();
            if (en == null || applied.Contains(en))
                continue;
            applied.Add(en);
            en.TakeDamage(splashDamage);
        }
    }

    void ReturnToPool()
    {
        isActiveBullet = false;
        target = null;
        _sniperPierceActive = false;
        _aoeControlActive = false;
        _aoeSpinVisual = null;
        hitFxScale = 1f;
        ApplyPoolDefaultRootScale();
        if (_sniperHitIds != null)
            _sniperHitIds.Clear();
        PoolManager.Instance.Despawn(gameObject);
    }

    public void OnSpawn()
    {
        isActiveBullet = false;
        target = null;
        splashRadius = 0f;
        splashDamage = 0f;
        controlSlowMultiplier = 1f;
        controlSlowDuration = 0f;
        _sniperPierceActive = false;
        _aoeControlActive = false;
        _aoeSpinVisual = null;
        hitFxScale = 1f;
        ApplyPoolDefaultRootScale();
        if (_sniperHitIds != null)
            _sniperHitIds.Clear();
    }

    public void OnDespawn()
    {
        isActiveBullet = false;
        target = null;
        splashRadius = 0f;
        splashDamage = 0f;
        controlSlowMultiplier = 1f;
        controlSlowDuration = 0f;
        _sniperPierceActive = false;
        _aoeControlActive = false;
        _aoeSpinVisual = null;
        hitFxScale = 1f;
        ApplyPoolDefaultRootScale();
        if (_sniperHitIds != null)
            _sniperHitIds.Clear();
    }
}
