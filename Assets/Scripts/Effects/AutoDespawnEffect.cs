using UnityEngine;

public class AutoDespawnEffect : MonoBehaviour, IPoolable
{
    [Header("Auto Despawn Settings")]
    [SerializeField] private float extraLifetime = 0.05f;

    private ParticleSystem[] _particleSystems;
    private float _despawnAt;
    private bool _armed;

    private void Awake()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void OnSpawn()
    {
        ArmAndPlay();
    }

    public void OnDespawn()
    {
        _armed = false;
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
            lifetime = 0f;
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                var ps = _particleSystems[i];
                if (ps == null) continue;

                ps.Clear(true);
                ps.Play(true);

                var main = ps.main;
                float dur = main.duration;
                float maxLife = main.startLifetime.constantMax;
                lifetime = Mathf.Max(lifetime, dur + maxLife);
            }
            if (lifetime <= 0.01f) lifetime = 0.2f;
        }

        _despawnAt = Time.time + lifetime + Mathf.Max(0f, extraLifetime);
        _armed = true;
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

