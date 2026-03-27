using UnityEngine;
using TMPro;

/// <summary>
/// Game speed via Time.timeScale. Speed / Reset buttons must use Unity Button OnClick (scene) → OnSpeedCycleClicked / OnResetSpeedClicked.
/// </summary>
public class GameSpeedController : MonoBehaviour
{
    public static readonly float[] SpeedSteps = { 1f, 2f, 4f, 8f };
    private const float BaseFixedDelta = 0.02f;

    [SerializeField] private TMP_Text speedButtonLabel;

    private int _speedIndex;

    private void Awake()
    {
        _speedIndex = 0;
        ApplyCurrentSpeed();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = BaseFixedDelta;
    }

    /// <summary>Bind from SpeedButton.OnClick in the scene.</summary>
    public void OnSpeedCycleClicked()
    {
        Debug.Log("[GameSpeed] SpeedButton clicked");
        _speedIndex = (_speedIndex + 1) % SpeedSteps.Length;
        ApplyCurrentSpeed();
        float s = SpeedSteps[_speedIndex];
        Debug.Log($"[GameSpeed] CycleSpeed -> index={_speedIndex}, scale={s}");
    }

    /// <summary>Bind from ResetSpeedButton.OnClick in the scene.</summary>
    public void OnResetSpeedClicked()
    {
        Debug.Log("[GameSpeed] ResetButton clicked");
        _speedIndex = 0;
        ApplyCurrentSpeed();
        Debug.Log("[GameSpeed] ResetSpeed -> index=0, scale=1");
    }

    private void ApplyCurrentSpeed()
    {
        int i = Mathf.Clamp(_speedIndex, 0, SpeedSteps.Length - 1);
        float scale = SpeedSteps[i];
        Time.timeScale = Mathf.Max(0.0001f, scale);
        Time.fixedDeltaTime = BaseFixedDelta * Time.timeScale;

        if (speedButtonLabel != null)
            speedButtonLabel.text = $"Speed x{Mathf.RoundToInt(scale)}";
    }
}
