using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour, IPoolable
{
    /// <summary>UI-facing archetype; derived from prefab/instance name (lightweight, no new spawn pipeline).</summary>
    public enum InfoKind
    {
        Normal,
        Fast,
        Tank,
        Shield
    }

    /// <summary>When true, next pooled <see cref="Effect_Hit"/> uses pale-blue tint (set immediately before spawn).</summary>
    public static bool PendingShieldHitTint;

    private const float NightMoveSpeedMultiplier = 1.5f;
    private const float NightMaxHealthMultiplier = 1.5f;
    private const float NightDamageToBaseMultiplier = 1.25f;

    [Header("Base Stats")]
    public float maxHealth = 10f;
    public float baseMoveSpeed = 2f;
    public int rewardGold = 10;
    public int damageToBase = 1;

    [Tooltip("Shield archetype only. Max shield at wave 0 before scaling; HP uses maxHealth.")]
    [SerializeField] float shieldCapacityBase;

    [Header("Runtime")]
    public float currentHealth;

    float _cachedBaseShield;
    float currentShield;
    float totalMaxShield;
    float _dayTotalMaxShield;
    Renderer _shieldRenderer;
    Material _shieldMatInstance;
    MeshRenderer _bodyRenderer;
    Material _bodyMaterialInstance;
    Coroutine _shieldBreakCo;
    Coroutine _shieldFlashCo;
    static bool _loggedShieldHitFlashOnce;
    static bool _loggedShieldDomeRebuiltOnce;
    static bool _loggedShieldBreakFadeOnce;
    static Mesh _sharedShieldDomeMesh;

    /// <summary>Prefab/inspector max health (cached in Awake, never night-scaled).</summary>
    private float _cachedBaseMaxHealth;

    /// <summary>Prefab/inspector move speed (cached in Awake, never night-scaled).</summary>
    private float _cachedBaseMoveSpeed;

    /// <summary>Prefab damage to base (cached in Awake).</summary>
    private int _cachedDamageToBase;

    /// <summary>Damage to base after wave bonuses, before night multiplier.</summary>
    private int _dayDamageToBase;

    /// <summary>Runtime damage to base (includes night when active).</summary>
    private int _finalDamageToBase;

    /// <summary>Max health after wave bonuses, before night multiplier.</summary>
    private float _dayTotalMaxHealth;

    /// <summary>Move speed after wave bonuses, before night multiplier.</summary>
    private float _dayMoveSpeed;

    private bool _nightBuffActive;

    private float currentMoveSpeed;
    float _controlSlowSpeedFactor = 1f;
    float _controlSlowEndTime;

    int _aoeZoneOverlap;
    float _aoeDotLingerEnd;
    float _aoeDotAmt;
    float _aoeLingerDotNext;
    float _aoeZoneDotGate = -999f;
    private float totalMaxHealth;
    private Transform[] pathPoints;
    private int currentPointIndex;
    private bool isActiveEnemy = false;
    private static BaseHealth _cachedBaseHealth;
    private Vector3 _baseScale;
    private Quaternion _baseRotation;
    private Coroutine _hitShakeRoutine;
    private Coroutine _hitFlashRoutine;
    private Coroutine _deathRoutine;
    private bool _isDying;

    private InfoKind _infoKind;

    [Header("Hit Shake")]
    [SerializeField] private float hitShakeScale = 1.2f;
    [SerializeField] private float hitShakeUpTime = 0.07f;
    [SerializeField] private float hitShakeDownTime = 0.11f;

    [Header("Hit Flash")]
    [SerializeField] private bool enableHitFlash = true;
    [SerializeField] private Color hitFlashColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float hitFlashTime = 0.06f;

    [Header("Death Feedback")]
    [SerializeField] private float deathDuration = 0.16f;
    [SerializeField] private float deathTiltAngle = 20f;
    [SerializeField] private float deathScale = 0.82f;

    private struct MaterialColorCache
    {
        public Material material;
        public string colorProperty;
        public Color originalColor;
    }

    private readonly System.Collections.Generic.List<MaterialColorCache> _flashMaterials =
        new System.Collections.Generic.List<MaterialColorCache>(8);

    private readonly System.Collections.Generic.List<MaterialColorCache> _shieldFlashMaterials =
        new System.Collections.Generic.List<MaterialColorCache>(4);

    private void Awake()
    {
        _cachedBaseMaxHealth = maxHealth;
        _cachedBaseMoveSpeed = baseMoveSpeed;
        _cachedDamageToBase = damageToBase;
        _cachedBaseShield = Mathf.Max(0f, shieldCapacityBase);
        _infoKind = ResolveInfoKind(gameObject.name);
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;
        if (_infoKind == InfoKind.Shield)
            BuildShieldEnemyVisuals();
        CacheFlashMaterials();
    }

    private static InfoKind ResolveInfoKind(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return InfoKind.Normal;
        string n = objectName.ToLowerInvariant();
        if (n.Contains("shield")) return InfoKind.Shield;
        if (n.Contains("fast")) return InfoKind.Fast;
        if (n.Contains("tank")) return InfoKind.Tank;
        return InfoKind.Normal;
    }

    /// <summary>
    /// Applies or clears night buff on every enemy currently marching so visuals/stats match <see cref="WaveManager.IsNightWave"/>.
    /// Call right after <see cref="WaveManager.NotifyWaveStarted"/> (new spawns still get buff in <see cref="EnemySpawner"/>).
    /// </summary>
    public static void SyncNightBuffsWithWaveManager()
    {
        var enemies = UnityEngine.Object.FindObjectsOfType<Enemy>();
        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy e = enemies[i];
            if (e == null || !e.isActiveEnemy) continue;
            if (WaveManager.IsNightWave)
                e.ApplyNightBuff();
            else
                e.ResetNightBuff();
        }
    }

    private void Update()
    {
        if (!isActiveEnemy) return;
        MoveAlongPath();
        TickAoeControlDotLinger();
    }

    /// <param name="bonusHealth">Wave-scaled HP bonus (type-specific from spawner).</param>
    /// <param name="bonusDamageToBase">Wave-scaled damage to base (type-specific from spawner).</param>
    public void Initialize(Transform[] points, float bonusHealth, int bonusDamageToBase)
    {
        pathPoints = points;
        currentPointIndex = 0;

        _nightBuffActive = false;

        totalMaxHealth = _cachedBaseMaxHealth + bonusHealth;
        currentHealth = totalMaxHealth;
        // Wave scaling does not increase move speed; only night buff may multiply speed.
        currentMoveSpeed = _cachedBaseMoveSpeed;

        _dayTotalMaxHealth = totalMaxHealth;
        _dayMoveSpeed = currentMoveSpeed;

        if (_infoKind == InfoKind.Shield && _cachedBaseShield > 0f)
        {
            float hpRatio = _cachedBaseMaxHealth > 1e-4f ? bonusHealth / _cachedBaseMaxHealth : 0f;
            _dayTotalMaxShield = _cachedBaseShield * (1f + hpRatio);
            totalMaxShield = _dayTotalMaxShield;
            currentShield = totalMaxShield;
        }
        else
        {
            totalMaxShield = 0f;
            currentShield = 0f;
            _dayTotalMaxShield = 0f;
        }

        _dayDamageToBase = _cachedDamageToBase + bonusDamageToBase;
        _finalDamageToBase = _dayDamageToBase;

        isActiveEnemy = true;
        _controlSlowSpeedFactor = 1f;
        _controlSlowEndTime = 0f;
        ResetAoeControlState();

        if (pathPoints != null && pathPoints.Length > 0)
        {
            transform.position = pathPoints[0].position;
        }

        if (_infoKind == InfoKind.Shield)
            Debug.Log("[ShieldEnemy] Spawned shield enemy");
    }

    /// <summary>Night-wave only: scales move speed and max health once (no stacking).</summary>
    public void ApplyNightBuff()
    {
        if (_nightBuffActive) return;

        _nightBuffActive = true;
        totalMaxHealth = _dayTotalMaxHealth * NightMaxHealthMultiplier;
        currentHealth = totalMaxHealth;
        currentMoveSpeed = _dayMoveSpeed * NightMoveSpeedMultiplier;
        _finalDamageToBase = Mathf.Max(1, Mathf.RoundToInt(_dayDamageToBase * NightDamageToBaseMultiplier));
        if (_infoKind == InfoKind.Shield && _dayTotalMaxShield > 0f)
        {
            totalMaxShield = _dayTotalMaxShield * NightMaxHealthMultiplier;
            currentShield = totalMaxShield;
        }
    }

    /// <summary>Restore day stats from last Initialize (safe if buff was not applied).</summary>
    public void ResetNightBuff()
    {
        if (!_nightBuffActive) return;

        _nightBuffActive = false;
        totalMaxHealth = _dayTotalMaxHealth;
        currentMoveSpeed = _dayMoveSpeed;
        currentHealth = totalMaxHealth;
        _finalDamageToBase = _dayDamageToBase;
        if (_infoKind == InfoKind.Shield && _dayTotalMaxShield > 0f)
        {
            totalMaxShield = _dayTotalMaxShield;
            currentShield = totalMaxShield;
        }
    }

    // ---- Read-only API for UI (no gameplay logic) ----

    public float GetCurrentHealth() => currentHealth;

    /// <summary>Current max HP cap (after wave + night modifiers).</summary>
    public float GetMaxHealth() => totalMaxHealth;

    /// <summary>Same as <see cref="GetMaxHealth"/> for UI clarity.</summary>
    public float GetFinalMaxHP() => totalMaxHealth;

    /// <summary>Max HP for current spawn after wave bonuses, before night multiplier.</summary>
    public float GetBaseMaxHP() => isActiveEnemy ? _dayTotalMaxHealth : _cachedBaseMaxHealth;

    /// <summary>Configured damage dealt to the base when this enemy reaches it (final, includes night if active).</summary>
    public int GetAttackDamageToBase() => isActiveEnemy ? _finalDamageToBase : _cachedDamageToBase;

    /// <summary>Damage to base after wave scaling, before night multiplier (for UI).</summary>
    public int GetBaseDamage() => isActiveEnemy ? _dayDamageToBase : _cachedDamageToBase;

    /// <summary>Damage applied to base on reach (includes night multiplier when active).</summary>
    public int GetFinalDamage() => isActiveEnemy ? _finalDamageToBase : _cachedDamageToBase;

    /// <summary>Move speed after wave bonuses, before night multiplier.</summary>
    public float GetBaseMoveSpeed() => isActiveEnemy ? _dayMoveSpeed : _cachedBaseMoveSpeed;

    /// <summary>Current move speed used in movement (includes night multiplier when active).</summary>
    public float GetFinalMoveSpeed() => currentMoveSpeed;

    public float GetMoveSpeed() => currentMoveSpeed;

    public float GetPrefabBaseMaxHealth() => _cachedBaseMaxHealth;

    public float GetPrefabBaseMoveSpeed() => _cachedBaseMoveSpeed;

    /// <summary>True while night HP/SPD scaling is active on this instance.</summary>
    public bool HasBuff() => _nightBuffActive;

    /// <summary>Short ASCII label for UI; empty when no night buff.</summary>
    public string GetBuffDescription() => _nightBuffActive ? "Night Buff" : "";

    /// <summary>False when dying, inactive, or not actively moving on the path.</summary>
    public bool IsAliveForInfoPanel()
    {
        if (!isActiveAndEnabled) return false;
        if (!gameObject.activeInHierarchy) return false;
        if (_isDying) return false;
        return isActiveEnemy;
    }

    public InfoKind GetInfoKind() => _infoKind;

    public string GetInfoKindDisplayName()
    {
        switch (_infoKind)
        {
            case InfoKind.Fast: return "Fast";
            case InfoKind.Tank: return "Tank";
            case InfoKind.Shield: return "Shield";
            default: return "Normal";
        }
    }

    public float GetCurrentShield() => _infoKind == InfoKind.Shield ? Mathf.Max(0f, currentShield) : 0f;

    public float GetMaxShield() => _infoKind == InfoKind.Shield ? Mathf.Max(0f, totalMaxShield) : 0f;

    /// <summary>True while night scaling is applied (read-only for UI).</summary>
    public bool HasNightBuffActive() => _nightBuffActive;

    /// <summary>Ground movers only; flying enemies should return false when added.</summary>
    public bool IsGroundEnemy => true;

    void ResetAoeControlState()
    {
        _aoeZoneOverlap = 0;
        _aoeDotLingerEnd = 0f;
        _aoeDotAmt = 0f;
        _aoeLingerDotNext = 0f;
        _aoeZoneDotGate = -999f;
    }

    /// <summary>AOE Control route: one shared DOT tick gate across all zones (no stacking).</summary>
    public bool ApplyAoeControlDotFromZone(float damage)
    {
        if (damage <= 0f) return false;
        if (Time.time < _aoeZoneDotGate + 0.499f) return false;
        _aoeZoneDotGate = Time.time;
        TakeDamage(damage);
        return true;
    }

    public int AoeControlZoneEnter()
    {
        _aoeZoneOverlap++;
        if (_aoeZoneOverlap == 1)
            _aoeDotLingerEnd = 0f;
        return _aoeZoneOverlap;
    }

    public void AoeControlZoneRemoved(float lingerSec, float dotPerTick)
    {
        _aoeDotAmt = dotPerTick;
        _aoeZoneOverlap = Mathf.Max(0, _aoeZoneOverlap - 1);
        if (_aoeZoneOverlap > 0)
            return;
        _aoeDotLingerEnd = Time.time + lingerSec;
        _aoeLingerDotNext = Time.time + 0.5f;
    }

    void TickAoeControlDotLinger()
    {
        if (_aoeZoneOverlap > 0) return;
        if (_aoeDotLingerEnd <= 0f) return;
        if (Time.time >= _aoeDotLingerEnd)
        {
            _aoeDotLingerEnd = 0f;
            return;
        }

        if (Time.time < _aoeLingerDotNext) return;
        TakeDamage(_aoeDotAmt);
        _aoeLingerDotNext += 0.5f;
    }

    /// <summary>Applies a non-stacking move slow; re-hit refreshes duration only.</summary>
    public void ApplyControlSlow(float speedMultiplier, float durationSec)
    {
        if (speedMultiplier >= 1f || durationSec <= 0f) return;
        _controlSlowSpeedFactor = Mathf.Clamp(speedMultiplier, 0.15f, 1f);
        _controlSlowEndTime = Time.time + durationSec;
    }

    private void MoveAlongPath()
    {
        if (pathPoints == null || pathPoints.Length == 0) return;

        float spd = currentMoveSpeed;
        if (Time.time < _controlSlowEndTime)
            spd *= _controlSlowSpeedFactor;
        if (_aoeZoneOverlap > 0)
            spd *= 0.75f;

        Transform targetPoint = pathPoints[currentPointIndex];
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            spd * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPoint.position) < 0.1f)
        {
            currentPointIndex++;

            if (currentPointIndex >= pathPoints.Length)
            {
                ReachBase();
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (_isDying) return;
        // Pool.Return disables the object before IPoolable.OnDespawn; late hits must not start coroutines.
        if (!gameObject.activeInHierarchy) return;
        if (damage <= 0f) return;

        if (_infoKind == InfoKind.Shield && currentShield > 0f)
        {
            float remaining = damage;
            float toShield = Mathf.Min(remaining, currentShield);
            currentShield -= toShield;
            remaining -= toShield;

            if (toShield > 0f)
            {
                Debug.Log($"[ShieldEnemy] Shield took damage amount={toShield:0.##}");
                PlayShieldHitFlash();
                SpawnShieldHitParticlesAt(transform.position + Vector3.up * 0.5f);
            }

            if (currentShield <= 0f && toShield > 0f)
            {
                Debug.Log("[ShieldEnemy] Shield broken");
                if (_shieldBreakCo != null)
                    StopCoroutine(_shieldBreakCo);
                _shieldBreakCo = StartCoroutine(ShieldBreakFadeRoutine());
            }

            if (remaining > 0f)
            {
                Debug.Log($"[ShieldEnemy] Overflow damage to HP amount={remaining:0.##}");
                Debug.Log($"[ShieldEnemy] HP took damage amount={remaining:0.##}");
                currentHealth -= remaining;
                PlayHitShake();
                PlayBodyHitFlash();
            }
        }
        else
        {
            currentHealth -= damage;
            if (damage > 0f)
            {
                PlayHitShake();
                PlayBodyHitFlash();
            }

            if (_infoKind == InfoKind.Shield && currentShield <= 0f)
                Debug.Log($"[ShieldEnemy] HP took damage amount={damage:0.##}");
        }

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (_isDying) return;
        if (!gameObject.activeInHierarchy) return;

        _isDying = true;

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddGold(rewardGold);
        }
        else
        {
            Debug.LogError("EconomyManager.Instance 为空！");
        }

        isActiveEnemy = false;

        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }
        _deathRoutine = StartCoroutine(DeathRoutine());
    }

    private void ReachBase()
    {
        isActiveEnemy = false;

        if (_cachedBaseHealth == null)
        {
            _cachedBaseHealth = FindObjectOfType<BaseHealth>();
        }

        if (_cachedBaseHealth != null && _finalDamageToBase > 0)
        {
            _cachedBaseHealth.TakeDamage(_finalDamageToBase);
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Despawn(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void OnSpawn()
    {
        isActiveEnemy = false;
        _isDying = false;
        _nightBuffActive = false;
        currentPointIndex = 0;
        pathPoints = null;
        currentMoveSpeed = _cachedBaseMoveSpeed;
        totalMaxHealth = _cachedBaseMaxHealth;
        ResetAoeControlState();
        if (_baseScale == Vector3.zero) _baseScale = transform.localScale;
        if (_baseRotation == Quaternion.identity) _baseRotation = transform.localRotation;
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
        RestoreBodyFlashColors();
        RestoreShieldFlashColors();
        ResetShieldShellAfterPool();
    }

    public void OnDespawn()
    {
        isActiveEnemy = false;
        _nightBuffActive = false;
        currentPointIndex = 0;
        pathPoints = null;
        if (_hitShakeRoutine != null)
        {
            StopCoroutine(_hitShakeRoutine);
            _hitShakeRoutine = null;
        }
        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = null;
        }
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }
        if (_shieldBreakCo != null)
        {
            StopCoroutine(_shieldBreakCo);
            _shieldBreakCo = null;
        }
        if (_shieldFlashCo != null)
        {
            StopCoroutine(_shieldFlashCo);
            _shieldFlashCo = null;
        }
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
        RestoreBodyFlashColors();
        RestoreShieldFlashColors();
    }

    private void PlayHitShake()
    {
        if (!isActiveAndEnabled) return;
        if (_baseScale == Vector3.zero) _baseScale = transform.localScale;

        if (_hitShakeRoutine != null)
        {
            StopCoroutine(_hitShakeRoutine);
            _hitShakeRoutine = null;
            transform.localScale = _baseScale;
        }

        _hitShakeRoutine = StartCoroutine(HitShakeRoutine());
    }

    private System.Collections.IEnumerator HitShakeRoutine()
    {
        float up = Mathf.Max(0.001f, hitShakeUpTime);
        float down = Mathf.Max(0.001f, hitShakeDownTime);
        Vector3 peak = _baseScale * Mathf.Max(1f, hitShakeScale);

        float t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / up);
            transform.localScale = Vector3.LerpUnclamped(_baseScale, peak, 1f - Mathf.Pow(1f - a, 3f));
            yield return null;
        }

        t = 0f;
        while (t < down)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / down);
            transform.localScale = Vector3.LerpUnclamped(peak, _baseScale, a * a * a);
            yield return null;
        }

        transform.localScale = _baseScale;
        _hitShakeRoutine = null;
    }

    void BuildShieldEnemyVisuals()
    {
        const float bodyVisualHeight = 1f;
        const float bodyMaxWidth = 0.8f;
        const float rimYWorld = 0.7f * bodyVisualHeight;
        const float apexYWorld = bodyVisualHeight + 0.15f * bodyVisualHeight;
        const float domeHeight = apexYWorld - rimYWorld;
        const float rimRadius = 0.5f * 1.35f * bodyMaxWidth;

        var mf = GetComponent<MeshFilter>();
        if (mf != null) UnityEngine.Object.Destroy(mf);
        var mrRoot = GetComponent<MeshRenderer>();
        if (mrRoot != null) UnityEngine.Object.Destroy(mrRoot);

        Transform bodyT = transform.Find("ShieldBody");
        if (bodyT == null)
        {
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bodyGo.name = "ShieldBody";
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            bodyGo.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
            var col = bodyGo.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            bodyT = bodyGo.transform;
            var bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            bodyMat.name = "ShieldEnemy_Body_Runtime";
            Color bodyCol = new Color(0xd9 / 255f, 0xdd / 255f, 0xe2 / 255f, 1f);
            if (bodyMat.HasProperty("_BaseColor")) bodyMat.SetColor("_BaseColor", bodyCol);
            else bodyMat.color = bodyCol;
            _bodyRenderer = bodyGo.GetComponent<MeshRenderer>();
            if (_bodyRenderer != null)
                _bodyRenderer.sharedMaterial = bodyMat;
            _bodyMaterialInstance = bodyMat;
        }
        else
        {
            _bodyRenderer = bodyT.GetComponent<MeshRenderer>();
            if (_bodyRenderer != null && _bodyMaterialInstance == null)
                _bodyMaterialInstance = _bodyRenderer.sharedMaterial;
        }

        Transform domeT = transform.Find("ShieldShell");
        if (domeT == null)
        {
            var domeGo = new GameObject("ShieldShell");
            domeGo.transform.SetParent(transform, false);
            domeGo.AddComponent<MeshFilter>();
            domeGo.AddComponent<MeshRenderer>();
            domeT = domeGo.transform;
        }

        domeT.localPosition = new Vector3(0f, rimYWorld, 0f);
        domeT.localRotation = Quaternion.identity;
        domeT.localScale = Vector3.one;

        var domeMf = domeT.GetComponent<MeshFilter>();
        var domeMr = domeT.GetComponent<MeshRenderer>();
        if (domeMf == null) domeMf = domeT.gameObject.AddComponent<MeshFilter>();
        if (domeMr == null) domeMr = domeT.gameObject.AddComponent<MeshRenderer>();

        foreach (var c in domeT.GetComponents<Collider>())
            UnityEngine.Object.Destroy(c);

        Mesh domeMesh = GetOrBuildSharedShieldDomeMesh(rimRadius, domeHeight);
        domeMf.sharedMesh = domeMesh;

        _shieldRenderer = domeMr;
        if (_shieldRenderer == null) return;

        if (_shieldMatInstance != null)
            return;

        var hexTex = CreateShieldHexTexture(256);
        hexTex.name = "ShieldHexPattern";
        hexTex.wrapMode = TextureWrapMode.Repeat;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _shieldMatInstance = new Material(lit);
        _shieldMatInstance.name = "ShieldEnemy_Dome_Runtime";
        Color fill = new Color(0x59 / 255f, 0xd0 / 255f, 0xff / 255f, 0.32f);
        if (_shieldMatInstance.HasProperty("_Surface")) _shieldMatInstance.SetFloat("_Surface", 1f);
        if (_shieldMatInstance.HasProperty("_Blend")) _shieldMatInstance.SetFloat("_Blend", 0f);
        if (_shieldMatInstance.HasProperty("_BaseColor")) _shieldMatInstance.SetColor("_BaseColor", fill);
        if (_shieldMatInstance.HasProperty("_BaseMap")) _shieldMatInstance.SetTexture("_BaseMap", hexTex);
        if (_shieldMatInstance.HasProperty("_SrcBlend"))
            _shieldMatInstance.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (_shieldMatInstance.HasProperty("_DstBlend"))
            _shieldMatInstance.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (_shieldMatInstance.HasProperty("_ZWrite")) _shieldMatInstance.SetFloat("_ZWrite", 0f);
        if (_shieldMatInstance.HasProperty("_Cull")) _shieldMatInstance.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _shieldMatInstance.renderQueue = 3000;
        _shieldRenderer.sharedMaterial = _shieldMatInstance;
    }

    static Mesh GetOrBuildSharedShieldDomeMesh(float rimRadius, float domeHeight)
    {
        if (_sharedShieldDomeMesh != null)
            return _sharedShieldDomeMesh;

        _sharedShieldDomeMesh = BuildShieldDomeCapMesh(rimRadius, domeHeight, 28, 10);
        _sharedShieldDomeMesh.name = "ShieldEnemy_DomeCap";

        if (!_loggedShieldDomeRebuiltOnce)
        {
            _loggedShieldDomeRebuiltOnce = true;
            Debug.Log("[ShieldEnemy] Shield dome visual rebuilt");
        }

        return _sharedShieldDomeMesh;
    }

    /// <summary>
    /// Open-bottom spherical cap: rim at local y=0, apex at local y=domeHeight (flat opening faces down toward feet).
    /// </summary>
    static Mesh BuildShieldDomeCapMesh(float rimRadius, float domeHeight, int meridians, int stacks)
    {
        if (meridians < 3) meridians = 3;
        if (stacks < 2) stacks = 2;

        float sphereR = (rimRadius * rimRadius + domeHeight * domeHeight) / (2f * Mathf.Max(1e-4f, domeHeight));
        float centerY = domeHeight - sphereR;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        const float hexColumns = 6.5f;
        const float hexRows = 4.5f;

        verts.Add(new Vector3(0f, domeHeight, 0f));
        uvs.Add(new Vector2(0.5f * hexColumns, hexRows));

        for (int i = 1; i <= stacks; i++)
        {
            float t = i / (float)stacks;
            float y = domeHeight * (1f - t);
            float dy = y - centerY;
            float rr = sphereR * sphereR - dy * dy;
            float ringR = rr > 1e-6f ? Mathf.Sqrt(rr) : 0f;

            for (int j = 0; j < meridians; j++)
            {
                float ang = j * Mathf.PI * 2f / meridians;
                float x = ringR * Mathf.Cos(ang);
                float z = ringR * Mathf.Sin(ang);
                var p = new Vector3(x, y, z);
                verts.Add(p);
                float u = (j / (float)meridians) * hexColumns;
                float v = (i / (float)stacks) * hexRows;
                uvs.Add(new Vector2(u, v));
            }
        }

        int apex = 0;
        int firstRing = 1;
        for (int j = 0; j < meridians; j++)
        {
            int jn = (j + 1) % meridians;
            tris.Add(apex);
            tris.Add(firstRing + j);
            tris.Add(firstRing + jn);
        }

        for (int i = 0; i < stacks - 1; i++)
        {
            int r0 = 1 + i * meridians;
            int r1 = 1 + (i + 1) * meridians;
            for (int j = 0; j < meridians; j++)
            {
                int jn = (j + 1) % meridians;
                int a = r0 + j;
                int b = r0 + jn;
                int c = r1 + j;
                int d = r1 + jn;
                tris.Add(a);
                tris.Add(c);
                tris.Add(d);
                tris.Add(a);
                tris.Add(d);
                tris.Add(b);
            }
        }

        var mesh = new Mesh { name = "ShieldEnemy_DomeCap" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Texture2D CreateShieldHexTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color fill = new Color(0x59 / 255f, 0xd0 / 255f, 0xff / 255f, 0.32f);
        Color line = new Color(0xb8 / 255f, 0xf2 / 255f, 0xff / 255f, 0.62f);
        float inv = 1f / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x * inv * 14f;
                float v = y * inv * 14f;
                float c = HexGridLineFactor(u, v);
                float tline = 1f - Mathf.SmoothStep(0.035f, 0.095f, c);
                tex.SetPixel(x, y, Color.Lerp(fill, line, tline));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    static float HexGridLineFactor(float u, float v)
    {
        const float s = 1.154700538f;
        float q = (2f / 3f * u) / s;
        float r = (-1f / 3f * u + 0.577350269f * v) / s;
        float s3 = -q - r;
        float dq = Mathf.Abs(q - Mathf.Round(q));
        float dr = Mathf.Abs(r - Mathf.Round(r));
        float ds = Mathf.Abs(s3 - Mathf.Round(s3));
        return Mathf.Min(dq, Mathf.Min(dr, ds));
    }

    void SpawnShieldHitParticlesAt(Vector3 worldPos)
    {
        if (PoolManager.Instance == null) return;
        PendingShieldHitTint = true;
        PoolManager.Instance.Spawn("Effect_Hit", worldPos, Quaternion.identity);
    }

    void PlayShieldHitFlash()
    {
        if (_shieldFlashMaterials == null || _shieldFlashMaterials.Count == 0) return;
        if (_shieldFlashCo != null)
        {
            StopCoroutine(_shieldFlashCo);
            _shieldFlashCo = null;
            RestoreShieldFlashColors();
        }

        _shieldFlashCo = StartCoroutine(ShieldHitFlashRoutine());
    }

    IEnumerator ShieldHitFlashRoutine()
    {
        if (!_loggedShieldHitFlashOnce)
        {
            _loggedShieldHitFlashOnce = true;
            Debug.Log("[ShieldEnemy] Shield hit flash triggered");
        }

        Color flash = new Color(0xe8 / 255f, 0xfd / 255f, 0xff / 255f, 1f);
        float up = 0.04f;
        float down = 0.06f;
        float t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / up);
            for (int i = 0; i < _shieldFlashMaterials.Count; i++)
            {
                var e = _shieldFlashMaterials[i];
                if (e.material == null || string.IsNullOrEmpty(e.colorProperty)) continue;
                e.material.SetColor(e.colorProperty, Color.LerpUnclamped(e.originalColor, flash, a));
            }

            yield return null;
        }

        t = 0f;
        while (t < down)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / down);
            for (int i = 0; i < _shieldFlashMaterials.Count; i++)
            {
                var e = _shieldFlashMaterials[i];
                if (e.material == null || string.IsNullOrEmpty(e.colorProperty)) continue;
                e.material.SetColor(e.colorProperty, Color.LerpUnclamped(e.originalColor, flash, a));
            }

            yield return null;
        }

        RestoreShieldFlashColors();
        _shieldFlashCo = null;
    }

    void RestoreShieldFlashColors()
    {
        for (int i = 0; i < _shieldFlashMaterials.Count; i++)
        {
            var e = _shieldFlashMaterials[i];
            if (e.material == null || string.IsNullOrEmpty(e.colorProperty)) continue;
            e.material.SetColor(e.colorProperty, e.originalColor);
        }
    }

    IEnumerator ShieldBreakFadeRoutine()
    {
        if (!_loggedShieldBreakFadeOnce)
        {
            _loggedShieldBreakFadeOnce = true;
            Debug.Log("[ShieldEnemy] Shield break fade triggered");
        }

        PlayShieldHitFlash();
        yield return new WaitForSeconds(0.06f);
        float dur = 0.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / dur);
            for (int i = 0; i < _shieldFlashMaterials.Count; i++)
            {
                var e = _shieldFlashMaterials[i];
                if (e.material == null || !e.material.HasProperty("_BaseColor")) continue;
                Color o = e.originalColor;
                e.material.SetColor("_BaseColor", new Color(o.r, o.g, o.b, o.a * a));
            }

            yield return null;
        }

        if (_shieldRenderer != null)
            _shieldRenderer.enabled = false;
        _shieldBreakCo = null;
    }

    void ResetShieldShellAfterPool()
    {
        if (_shieldRenderer != null)
        {
            _shieldRenderer.enabled = true;
            RestoreShieldFlashColors();
        }

        if (_shieldMatInstance != null && _shieldMatInstance.HasProperty("_BaseColor"))
        {
            Color o = new Color(0x59 / 255f, 0xd0 / 255f, 0xff / 255f, 0.32f);
            _shieldMatInstance.SetColor("_BaseColor", o);
        }
    }

    private void CacheFlashMaterials()
    {
        _flashMaterials.Clear();
        _shieldFlashMaterials.Clear();
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            bool isShieldShell = r.gameObject.name.IndexOf("ShieldShell", StringComparison.OrdinalIgnoreCase) >= 0;

            Material[] mats;
            try
            {
                mats = r.materials;
            }
            catch
            {
                continue;
            }

            if (mats == null) continue;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;

                if (mat.HasProperty("_BaseColor"))
                {
                    var entry = new MaterialColorCache
                    {
                        material = mat,
                        colorProperty = "_BaseColor",
                        originalColor = mat.GetColor("_BaseColor")
                    };
                    if (isShieldShell) _shieldFlashMaterials.Add(entry);
                    else _flashMaterials.Add(entry);
                }
                else if (mat.HasProperty("_Color"))
                {
                    var entry = new MaterialColorCache
                    {
                        material = mat,
                        colorProperty = "_Color",
                        originalColor = mat.GetColor("_Color")
                    };
                    if (isShieldShell) _shieldFlashMaterials.Add(entry);
                    else _flashMaterials.Add(entry);
                }
            }
        }
    }

    private void PlayBodyHitFlash()
    {
        if (!enableHitFlash || !isActiveAndEnabled) return;
        if (_flashMaterials == null || _flashMaterials.Count == 0) return;

        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = null;
            RestoreBodyFlashColors();
        }
        _hitFlashRoutine = StartCoroutine(BodyHitFlashRoutine());
    }

    private System.Collections.IEnumerator BodyHitFlashRoutine()
    {
        float total = Mathf.Max(0.02f, hitFlashTime);
        float up = total * 0.35f;
        float down = total * 0.65f;

        float t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / up);
            ApplyBodyFlashColor(a);
            yield return null;
        }

        t = 0f;
        while (t < down)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / down);
            ApplyBodyFlashColor(a);
            yield return null;
        }

        RestoreBodyFlashColors();
        _hitFlashRoutine = null;
    }

    private void ApplyBodyFlashColor(float amount01)
    {
        float a = Mathf.Clamp01(amount01);
        for (int i = 0; i < _flashMaterials.Count; i++)
        {
            var e = _flashMaterials[i];
            if (e.material == null || string.IsNullOrEmpty(e.colorProperty)) continue;
            e.material.SetColor(e.colorProperty, Color.LerpUnclamped(e.originalColor, hitFlashColor, a));
        }
    }

    private void RestoreBodyFlashColors()
    {
        if (_flashMaterials == null || _flashMaterials.Count == 0) return;
        for (int i = 0; i < _flashMaterials.Count; i++)
        {
            var e = _flashMaterials[i];
            if (e.material == null || string.IsNullOrEmpty(e.colorProperty)) continue;
            e.material.SetColor(e.colorProperty, e.originalColor);
        }
    }

    private System.Collections.IEnumerator DeathRoutine()
    {
        float duration = Mathf.Max(0.06f, deathDuration);
        float t = 0f;
        Vector3 fromScale = transform.localScale;
        Vector3 toScale = _baseScale * Mathf.Clamp(deathScale, 0.6f, 1f);
        Quaternion fromRot = transform.localRotation;
        Quaternion toRot = Quaternion.Euler(deathTiltAngle, 0f, 0f) * _baseRotation;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            // Ease out for a snappy, short feedback.
            float eased = 1f - Mathf.Pow(1f - a, 3f);

            transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            transform.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, eased);
            ApplyBodyFlashColor(1f - eased * 0.85f); // quick visual fade-out
            yield return null;
        }

        RestoreBodyFlashColors();

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Despawn(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}