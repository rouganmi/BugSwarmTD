using UnityEngine;

public class Enemy : MonoBehaviour, IPoolable
{
    /// <summary>UI-facing archetype; derived from prefab/instance name (lightweight, no new spawn pipeline).</summary>
    public enum InfoKind
    {
        Normal,
        Fast,
        Tank
    }

    private const float NightMoveSpeedMultiplier = 1.5f;
    private const float NightMaxHealthMultiplier = 1.5f;

    [Header("Base Stats")]
    public float maxHealth = 10f;
    public float baseMoveSpeed = 2f;
    public int rewardGold = 10;
    public int damageToBase = 1;

    [Header("Runtime")]
    public float currentHealth;

    /// <summary>Prefab/inspector max health (cached in Awake, never night-scaled).</summary>
    private float _cachedBaseMaxHealth;

    /// <summary>Prefab/inspector move speed (cached in Awake, never night-scaled).</summary>
    private float _cachedBaseMoveSpeed;

    /// <summary>Max health after wave bonuses, before night multiplier.</summary>
    private float _dayTotalMaxHealth;

    /// <summary>Move speed after wave bonuses, before night multiplier.</summary>
    private float _dayMoveSpeed;

    private bool _nightBuffActive;

    private float currentMoveSpeed;
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

    private void Awake()
    {
        _cachedBaseMaxHealth = maxHealth;
        _cachedBaseMoveSpeed = baseMoveSpeed;
        _infoKind = ResolveInfoKind(gameObject.name);
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;
        CacheFlashMaterials();
    }

    private static InfoKind ResolveInfoKind(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return InfoKind.Normal;
        string n = objectName.ToLowerInvariant();
        if (n.Contains("fast")) return InfoKind.Fast;
        if (n.Contains("tank")) return InfoKind.Tank;
        return InfoKind.Normal;
    }

    private void Update()
    {
        if (!isActiveEnemy) return;
        MoveAlongPath();
    }

    public void Initialize(Transform[] points, float bonusHealth = 0f, float bonusSpeed = 0f)
    {
        pathPoints = points;
        currentPointIndex = 0;

        _nightBuffActive = false;

        totalMaxHealth = _cachedBaseMaxHealth + bonusHealth;
        currentHealth = totalMaxHealth;
        currentMoveSpeed = _cachedBaseMoveSpeed + bonusSpeed;

        _dayTotalMaxHealth = totalMaxHealth;
        _dayMoveSpeed = currentMoveSpeed;

        isActiveEnemy = true;

        if (pathPoints != null && pathPoints.Length > 0)
        {
            transform.position = pathPoints[0].position;
        }
    }

    /// <summary>Night-wave only: scales move speed and max health once (no stacking).</summary>
    public void ApplyNightBuff()
    {
        if (_nightBuffActive) return;

        _nightBuffActive = true;
        totalMaxHealth = _dayTotalMaxHealth * NightMaxHealthMultiplier;
        currentHealth = totalMaxHealth;
        currentMoveSpeed = _dayMoveSpeed * NightMoveSpeedMultiplier;
    }

    /// <summary>Restore day stats from last Initialize (safe if buff was not applied).</summary>
    public void ResetNightBuff()
    {
        if (!_nightBuffActive) return;

        _nightBuffActive = false;
        totalMaxHealth = _dayTotalMaxHealth;
        currentMoveSpeed = _dayMoveSpeed;
        currentHealth = totalMaxHealth;
    }

    // ---- Read-only API for UI (no gameplay logic) ----

    public float GetCurrentHealth() => currentHealth;

    /// <summary>Current max HP cap (after wave + night modifiers).</summary>
    public float GetMaxHealth() => totalMaxHealth;

    /// <summary>Same as <see cref="GetMaxHealth"/> for UI clarity.</summary>
    public float GetFinalMaxHP() => totalMaxHealth;

    /// <summary>Max HP for current spawn after wave bonuses, before night multiplier.</summary>
    public float GetBaseMaxHP() => isActiveEnemy ? _dayTotalMaxHealth : _cachedBaseMaxHealth;

    /// <summary>Configured damage dealt to the base when this enemy reaches it (night does not scale this).</summary>
    public int GetAttackDamageToBase() => damageToBase;

    /// <summary>Inspector/base damage to base; identical to runtime final today.</summary>
    public int GetBaseDamage() => damageToBase;

    /// <summary>Damage applied to base on reach; same as <see cref="GetBaseDamage"/> unless future systems scale it.</summary>
    public int GetFinalDamage() => damageToBase;

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
            default: return "Normal";
        }
    }

    /// <summary>True while night scaling is applied (read-only for UI).</summary>
    public bool HasNightBuffActive() => _nightBuffActive;

    private void MoveAlongPath()
    {
        if (pathPoints == null || pathPoints.Length == 0) return;

        Transform targetPoint = pathPoints[currentPointIndex];
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            currentMoveSpeed * Time.deltaTime
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

        currentHealth -= damage;

        if (damage > 0f)
        {
            PlayHitShake();
            PlayHitFlash();
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
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

        if (_cachedBaseHealth != null && damageToBase > 0)
        {
            _cachedBaseHealth.TakeDamage(damageToBase);
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
        if (_baseScale == Vector3.zero) _baseScale = transform.localScale;
        if (_baseRotation == Quaternion.identity) _baseRotation = transform.localRotation;
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
        RestoreFlashColors();
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
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
        RestoreFlashColors();
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

    private void CacheFlashMaterials()
    {
        _flashMaterials.Clear();
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

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
                    _flashMaterials.Add(new MaterialColorCache
                    {
                        material = mat,
                        colorProperty = "_BaseColor",
                        originalColor = mat.GetColor("_BaseColor")
                    });
                }
                else if (mat.HasProperty("_Color"))
                {
                    _flashMaterials.Add(new MaterialColorCache
                    {
                        material = mat,
                        colorProperty = "_Color",
                        originalColor = mat.GetColor("_Color")
                    });
                }
            }
        }
    }

    private void PlayHitFlash()
    {
        if (!enableHitFlash || !isActiveAndEnabled) return;
        if (_flashMaterials == null || _flashMaterials.Count == 0) return;

        if (_hitFlashRoutine != null)
        {
            StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = null;
            RestoreFlashColors();
        }
        _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private System.Collections.IEnumerator HitFlashRoutine()
    {
        float total = Mathf.Max(0.02f, hitFlashTime);
        float up = total * 0.35f;
        float down = total * 0.65f;

        float t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / up);
            ApplyFlashColor(a);
            yield return null;
        }

        t = 0f;
        while (t < down)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / down);
            ApplyFlashColor(a);
            yield return null;
        }

        RestoreFlashColors();
        _hitFlashRoutine = null;
    }

    private void ApplyFlashColor(float amount01)
    {
        float a = Mathf.Clamp01(amount01);
        for (int i = 0; i < _flashMaterials.Count; i++)
        {
            var e = _flashMaterials[i];
            if (e.material == null || string.IsNullOrEmpty(e.colorProperty)) continue;
            e.material.SetColor(e.colorProperty, Color.LerpUnclamped(e.originalColor, hitFlashColor, a));
        }
    }

    private void RestoreFlashColors()
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
            ApplyFlashColor(1f - eased * 0.85f); // quick visual fade-out
            yield return null;
        }

        RestoreFlashColors();

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