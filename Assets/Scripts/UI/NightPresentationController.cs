using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Night wave presentation: dim + cool directional light / ambient, lightweight HUD toast.
/// Subscribes to <see cref="GameEvents.OnWaveChanged"/> (after <see cref="WaveManager.NotifyWaveStarted"/> sets <see cref="WaveManager.IsNightWave"/>).
/// </summary>
public class NightPresentationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light directionalLight;

    [Header("Night tuning")]
    [SerializeField, Range(0.35f, 0.95f)] private float nightDirIntensityMul = 0.62f;
    [SerializeField, Range(0.5f, 0.95f)] private float nightAmbientIntensityMul = 0.78f;
    [SerializeField, Range(0f, 1f)] private float nightCoolTintAmount = 0.42f;
    [SerializeField] private Color nightCoolTint = new Color(0.78f, 0.86f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float nightAmbientFlatCoolMix = 0.22f;

    [Header("Toast")]
    [SerializeField] private string nightToastMessage = "Night Wave - Enemies Empowered";
    [SerializeField, Range(1.2f, 2.8f)] private float nightToastDuration = 2f;
    [SerializeField, Range(0.05f, 0.8f)] private float nightToastFade = 0.35f;

    private float _dayDirIntensity;
    private Color _dayDirColor;
    private AmbientMode _dayAmbientMode;
    private Color _dayAmbientLight;
    private float _dayAmbientIntensity;
    private Color _daySky;
    private Color _dayEquator;
    private Color _dayGround;

    private bool _cached;

    private int _lastWaveHandled = -1;
    private int _lastToastWave = -1;

    private TextMeshProUGUI _toastLabel;
    private CanvasGroup _toastGroup;
    private Coroutine _toastRoutine;

    private void Awake()
    {
        CacheDayDefaults();
    }

    private void OnEnable()
    {
        GameEvents.OnWaveChanged += OnWaveChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnWaveChanged -= OnWaveChanged;
        if (_toastRoutine != null)
        {
            StopCoroutine(_toastRoutine);
            _toastRoutine = null;
        }
    }

    private void Start()
    {
        if (directionalLight == null)
            AutoFindDirectionalLight();

        EnsureToastUi();

        if (_cached)
            ApplyDayNightVisual(WaveManager.IsNightWave);
    }

    private void CacheDayDefaults()
    {
        if (directionalLight != null)
        {
            _dayDirIntensity = directionalLight.intensity;
            _dayDirColor = directionalLight.color;
        }

        _dayAmbientMode = RenderSettings.ambientMode;
        _dayAmbientLight = RenderSettings.ambientLight;
        _dayAmbientIntensity = RenderSettings.ambientIntensity;
        _daySky = RenderSettings.ambientSkyColor;
        _dayEquator = RenderSettings.ambientEquatorColor;
        _dayGround = RenderSettings.ambientGroundColor;
        _cached = true;
    }

    private void AutoFindDirectionalLight()
    {
        var lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l != null && l.type == LightType.Directional)
            {
                directionalLight = l;
                return;
            }
        }
    }

    private void EnsureToastUi()
    {
        if (_toastLabel != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var root = new GameObject("NightWaveToast");
        root.layer = canvas.gameObject.layer;
        var rt = root.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.78f);
        rt.anchorMax = new Vector2(0.5f, 0.78f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(620f, 44f);

        _toastGroup = root.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable = false;

        _toastLabel = root.AddComponent<TextMeshProUGUI>();
        _toastLabel.alignment = TextAlignmentOptions.Center;
        _toastLabel.fontSize = 22f;
        _toastLabel.fontStyle = FontStyles.Bold;
        _toastLabel.enableWordWrapping = true;
        if (TMP_Settings.defaultFontAsset != null)
            _toastLabel.font = TMP_Settings.defaultFontAsset;
        _toastLabel.color = new Color(0.92f, 0.95f, 1f, 0.95f);
        _toastLabel.text = string.Empty;
        root.SetActive(false);
    }

    private void OnWaveChanged(int waveIndex)
    {
        if (_lastWaveHandled == waveIndex)
            return;
        _lastWaveHandled = waveIndex;

        ApplyDayNightVisual(WaveManager.IsNightWave);

        if (WaveManager.IsNightWave)
            ShowNightToast();
    }

    /// <summary>Apply scene lighting for day or night from cached daytime defaults.</summary>
    public void ApplyDayNightVisual(bool isNight)
    {
        if (!_cached)
            CacheDayDefaults();

        if (directionalLight != null)
        {
            if (isNight)
            {
                directionalLight.intensity = _dayDirIntensity * nightDirIntensityMul;
                directionalLight.color = Color.Lerp(_dayDirColor, nightCoolTint, nightCoolTintAmount);
            }
            else
            {
                directionalLight.intensity = _dayDirIntensity;
                directionalLight.color = _dayDirColor;
            }
        }

        RestoreAmbientToDayDefaults();
        if (isNight)
            ApplyNightAmbient();
    }

    private void RestoreAmbientToDayDefaults()
    {
        RenderSettings.ambientMode = _dayAmbientMode;
        RenderSettings.ambientLight = _dayAmbientLight;
        RenderSettings.ambientIntensity = _dayAmbientIntensity;
        RenderSettings.ambientSkyColor = _daySky;
        RenderSettings.ambientEquatorColor = _dayEquator;
        RenderSettings.ambientGroundColor = _dayGround;
    }

    private void ApplyNightAmbient()
    {
        RenderSettings.ambientIntensity = _dayAmbientIntensity * nightAmbientIntensityMul;

        var flatCool = new Color(0.68f, 0.74f, 0.86f, 1f);
        if (RenderSettings.ambientMode == AmbientMode.Flat)
        {
            RenderSettings.ambientLight = Color.Lerp(_dayAmbientLight, flatCool, nightAmbientFlatCoolMix);
        }
        else if (RenderSettings.ambientMode == AmbientMode.Trilight)
        {
            RenderSettings.ambientSkyColor = Color.Lerp(_daySky, new Color(0.52f, 0.58f, 0.72f), 0.2f);
            RenderSettings.ambientEquatorColor = Color.Lerp(_dayEquator, new Color(0.48f, 0.52f, 0.6f), 0.18f);
            RenderSettings.ambientGroundColor = Color.Lerp(_dayGround, new Color(0.32f, 0.34f, 0.38f), 0.14f);
        }
    }

    private void ShowNightToast()
    {
        int w = WaveManager.CurrentWave;
        if (_lastToastWave == w)
            return;
        _lastToastWave = w;

        EnsureToastUi();
        if (_toastLabel == null || _toastGroup == null) return;

        if (_toastRoutine != null)
        {
            StopCoroutine(_toastRoutine);
            _toastRoutine = null;
        }

        _toastRoutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        _toastLabel.gameObject.SetActive(true);
        _toastLabel.text = nightToastMessage;
        _toastGroup.alpha = 1f;

        float hold = Mathf.Clamp(nightToastDuration, 1.2f, 2.8f);
        float fade = Mathf.Clamp(nightToastFade, 0.05f, 0.8f);

        float t = 0f;
        while (t < hold)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            _toastGroup.alpha = 1f - Mathf.Clamp01(t / fade);
            yield return null;
        }

        _toastGroup.alpha = 0f;
        _toastLabel.text = string.Empty;
        _toastLabel.gameObject.SetActive(false);
        _toastRoutine = null;
    }
}
