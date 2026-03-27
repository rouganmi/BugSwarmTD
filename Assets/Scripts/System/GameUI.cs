using System.Collections;
using UnityEngine;
using TMPro;

public class GameUI : MonoBehaviour
{
    [Header("References")]
    public BaseHealth baseHealth;
    public EnemySpawner enemySpawner;
    public CurrencySystem currencySystem;

    [Header("UI Text")]
    public TMP_Text baseHealthText;
    public TMP_Text waveText;
    public TMP_Text nextWaveText;
    public TMP_Text goldText;

    [Header("UI Juice (optional)")]
    [SerializeField] private UIJuiceAnimator goldJuice;
    [SerializeField] private UIJuiceAnimator waveJuice;
    [SerializeField] private UIJuiceAnimator baseHpJuice;
    [Header("Wave start toast (optional; auto-created under HUD if empty)")]
    [SerializeField] private TMP_Text waveStartToastText;
    [SerializeField] private UIJuiceAnimator waveStartToastJuice;
    [SerializeField] private float waveToastVisibleSeconds = 1.35f;
    [SerializeField] private float waveToastFadeSeconds = 0.25f;
    [SerializeField] private bool enableBaseHitCameraShake = true;
    [SerializeField] private float baseHitShakeDuration = 0.1f;
    [SerializeField] private float baseHitShakeAmplitude = 0.08f;

    private int _lastGold = int.MinValue;
    private int _lastWave = int.MinValue;
    private int _lastBaseHp = int.MinValue;
    private Coroutine _baseHpFlashRoutine;
    private Coroutine _cameraShakeRoutine;
    private Coroutine _waveToastRoutine;

    private void Awake()
    {
        if (goldJuice == null && goldText != null)
        {
            goldJuice = goldText.GetComponent<UIJuiceAnimator>();
            if (goldJuice == null) goldJuice = goldText.gameObject.AddComponent<UIJuiceAnimator>();
        }

        if (waveJuice == null && waveText != null)
        {
            waveJuice = waveText.GetComponent<UIJuiceAnimator>();
            if (waveJuice == null) waveJuice = waveText.gameObject.AddComponent<UIJuiceAnimator>();
        }

        if (baseHpJuice == null && baseHealthText != null)
        {
            baseHpJuice = baseHealthText.GetComponent<UIJuiceAnimator>();
            if (baseHpJuice == null) baseHpJuice = baseHealthText.gameObject.AddComponent<UIJuiceAnimator>();
        }

        HideDebugVisuals();
        EnsureWaveStartToast();
    }

    private void OnEnable()
    {
        GameEvents.OnWaveChanged += HandleWaveChangedEvent;
    }

    private void OnDisable()
    {
        GameEvents.OnWaveChanged -= HandleWaveChangedEvent;
    }

    private void HandleWaveChangedEvent(int waveNumber)
    {
        ShowWaveStartedToast(waveNumber);
    }

    private void EnsureWaveStartToast()
    {
        if (waveStartToastText != null) return;
        if (waveText == null) return;

        Transform hud = waveText.transform.parent;
        if (hud == null) return;

        var existing = hud.Find("WaveStartToast");
        if (existing != null)
        {
            waveStartToastText = existing.GetComponent<TMP_Text>();
            if (waveStartToastText == null) return;
        }
        else
        {
            var go = new GameObject("WaveStartToast", typeof(RectTransform));
            go.transform.SetParent(hud, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -6f);
            rt.sizeDelta = new Vector2(520f, 36f);

            waveStartToastText = go.AddComponent<TextMeshProUGUI>();
            waveStartToastText.raycastTarget = false;
            waveStartToastText.alignment = TextAlignmentOptions.Center;
            waveStartToastText.fontSize = 17f;
            waveStartToastText.fontWeight = FontWeight.Medium;
            waveStartToastText.color = new Color(1f, 0.92f, 0.55f, 0f);
            waveStartToastText.text = string.Empty;
        }

        if (waveStartToastJuice == null)
        {
            waveStartToastJuice = waveStartToastText.GetComponent<UIJuiceAnimator>();
            if (waveStartToastJuice == null) waveStartToastJuice = waveStartToastText.gameObject.AddComponent<UIJuiceAnimator>();
        }

        waveStartToastText.gameObject.SetActive(false);
    }

    private void ShowWaveStartedToast(int waveNumber)
    {
        if (waveStartToastText == null) return;

        if (_waveToastRoutine != null)
        {
            StopCoroutine(_waveToastRoutine);
            _waveToastRoutine = null;
        }
        _waveToastRoutine = StartCoroutine(WaveToastRoutine(waveNumber));
    }

    private IEnumerator WaveToastRoutine(int waveNumber)
    {
        Color c = waveStartToastText.color;
        c.a = 1f;
        waveStartToastText.color = c;
        waveStartToastText.text = $"Wave {waveNumber} Started";
        waveStartToastText.gameObject.SetActive(true);
        waveStartToastJuice?.Pulse();

        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, waveToastVisibleSeconds));

        float fade = Mathf.Max(0.02f, waveToastFadeSeconds);
        float t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fade);
            c = waveStartToastText.color;
            c.a = a;
            waveStartToastText.color = c;
            yield return null;
        }

        waveStartToastText.gameObject.SetActive(false);
        _waveToastRoutine = null;
    }

    private void HideDebugVisuals()
    {
        // Hide editor-only helper visuals at runtime (keep colliders/transforms).
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r == null) continue;
            string n = r.gameObject.name;
            if (n.StartsWith("PathPoint_") || n == "CameraRig")
            {
                r.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (baseHealth != null && baseHealthText != null)
        {
            int hp = baseHealth.GetCurrentHealth();
            baseHealthText.text = "Base HP: " + hp;

            if (_lastBaseHp != int.MinValue && hp < _lastBaseHp)
            {
                baseHpJuice?.Pulse();
                FlashBaseHp();
                TriggerBaseHitCameraShake();
            }
            _lastBaseHp = hp;
        }

        if (enemySpawner != null && waveText != null)
        {
            int w = enemySpawner.GetCurrentWave();
            waveText.text = w <= 0 ? "Wave —" : "Wave " + w;

            if (_lastWave != int.MinValue && w != _lastWave)
            {
                waveJuice?.Pulse();
            }
            _lastWave = w;
        }

        if (enemySpawner != null && nextWaveText != null)
        {
            if (enemySpawner.IsWaitingForNextWave())
            {
                float timeLeft = enemySpawner.GetWaveTimer();
                if (timeLeft < 0f) timeLeft = 0f;

                nextWaveText.text = "Next Wave: " + timeLeft.ToString("0.0") + "s";
            }
            else
            {
                float displayLeft = enemySpawner.GetWaveDisplayCountdownRemaining();
                if (displayLeft < 0f) displayLeft = 0f;
                nextWaveText.text = "Next Wave: " + displayLeft.ToString("0.0") + "s";
            }
        }

        if (currencySystem != null && goldText != null)
        {
            int g = currencySystem.GetCurrentGold();
            goldText.text = "Gold: " + g;

            if (_lastGold != int.MinValue && g != _lastGold)
            {
                goldJuice?.Pulse();
            }
            _lastGold = g;
        }
    }

    private void FlashBaseHp()
    {
        if (!isActiveAndEnabled || baseHealthText == null) return;

        if (_baseHpFlashRoutine != null)
        {
            StopCoroutine(_baseHpFlashRoutine);
            _baseHpFlashRoutine = null;
        }
        _baseHpFlashRoutine = StartCoroutine(FlashBaseHpRoutine());
    }

    private System.Collections.IEnumerator FlashBaseHpRoutine()
    {
        Color original = baseHealthText.color;
        Color flash = new Color(1f, 0.25f, 0.25f, original.a);

        float up = 0.05f;
        float down = 0.12f;
        float t = 0f;

        while (t < up)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / up);
            baseHealthText.color = Color.LerpUnclamped(original, flash, a);
            yield return null;
        }

        t = 0f;
        while (t < down)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / down);
            baseHealthText.color = Color.LerpUnclamped(flash, original, a);
            yield return null;
        }

        baseHealthText.color = original;
        _baseHpFlashRoutine = null;
    }

    private void TriggerBaseHitCameraShake()
    {
        if (!enableBaseHitCameraShake) return;

        Camera cam = Camera.main;
        if (cam == null) return;
        Transform shakeTarget = cam.transform.parent != null ? cam.transform.parent : cam.transform;

        if (_cameraShakeRoutine != null)
        {
            StopCoroutine(_cameraShakeRoutine);
            _cameraShakeRoutine = null;
        }
        _cameraShakeRoutine = StartCoroutine(BaseHitCameraShakeRoutine(shakeTarget));
    }

    private System.Collections.IEnumerator BaseHitCameraShakeRoutine(Transform target)
    {
        if (target == null) yield break;

        Vector3 originalPos = target.position;
        float duration = Mathf.Max(0.03f, baseHitShakeDuration);
        float amp = Mathf.Max(0f, baseHitShakeAmplitude);
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float fade = 1f - p;
            Vector2 rnd = Random.insideUnitCircle * amp * fade;
            target.position = originalPos + new Vector3(rnd.x, 0f, rnd.y);
            yield return null;
        }

        target.position = originalPos;
        _cameraShakeRoutine = null;
    }
}