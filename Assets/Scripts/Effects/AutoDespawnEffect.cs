using UnityEngine;

public class AutoDespawnEffect : MonoBehaviour, IPoolable
{
    [Header("Auto Despawn Settings")]
    [SerializeField] private float extraLifetime = 0.05f;

    private ParticleSystem[] _particleSystems;
    private ParticleSystem.MinMaxGradient[] _defaultStartColors;
    private float _despawnAt;
    private bool _armed;

    private void Awake()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        if (_particleSystems != null && _particleSystems.Length > 0)
        {
            _defaultStartColors = new ParticleSystem.MinMaxGradient[_particleSystems.Length];
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                if (_particleSystems[i] != null)
                    _defaultStartColors[i] = _particleSystems[i].main.startColor;
            }
        }
    }

    public void OnSpawn()
    {
        ArmAndPlay();
    }

    public void OnDespawn()
    {
        _armed = false;
        RestoreDefaultParticleColors();
    }

    private void OnEnable()
    {
        // In case this object is enabled without going through pool callbacks.
        ArmAndPlay();
    }

    private void Update()
    {
        if (!_armed) return;
        if (Time.time < _despawnAt) return;

        DespawnSafe();
    }

    private void ArmAndPlay()
    {
        float lifetime = 0.2f;

        if (_particleSystems != null && _particleSystems.Length > 0)
        {
            bool shieldTint = Enemy.PendingShieldHitTint;
            if (shieldTint)
                Enemy.PendingShieldHitTint = false;

            lifetime = 0f;
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var ps = _particleSystems[i];
                if (ps == null) continue;

                var main = ps.main;
                if (shieldTint)
                {
                    Color c0 = new Color(0xae / 255f, 0xeb / 255f, 0xff / 255f, 1f);
                    Color c1 = new Color(0xd8 / 255f, 0xf7 / 255f, 0xff / 255f, 1f);
                    main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
                }
                else if (_defaultStartColors != null && i < _defaultStartColors.Length)
                {
                    main.startColor = _defaultStartColors[i];
                }

                ps.Clear(true);
                ps.Play(true);

                float dur = main.duration;
                float maxLife = main.startLifetime.constantMax;
                lifetime = Mathf.Max(lifetime, dur + maxLife);
            }
            if (lifetime <= 0.01f) lifetime = 0.2f;
        }

        _despawnAt = Time.time + lifetime + Mathf.Max(0f, extraLifetime);
        _armed = true;
    }

    void RestoreDefaultParticleColors()
    {
        if (_particleSystems == null || _defaultStartColors == null) return;
        for (int i = 0; i < _particleSystems.Length && i < _defaultStartColors.Length; i++)
        {
            if (_particleSystems[i] == null) continue;
            var main = _particleSystems[i].main;
            main.startColor = _defaultStartColors[i];
        }
    }

    private void DespawnSafe()
    {
        _armed = false;

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

