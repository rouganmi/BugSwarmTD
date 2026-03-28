using UnityEngine;

/// <summary>
/// Wave index and night state. Updated by EnemySpawner each time a wave starts.
/// </summary>
public class WaveManager : MonoBehaviour
{
    private const int NightWaveInterval = 7;

    /// <summary>1-based wave index from the last NotifyWaveStarted call.</summary>
    public static int CurrentWave { get; private set; }

    /// <summary>True when current wave is a night wave (7, 14, 21, ...).</summary>
    public static bool IsNightWave { get; private set; }

    /// <summary>
    /// Call once per wave when the wave index is finalized (after increment).
    /// Rule: waveIndex % 7 == 0 → isNight = true, else false.
    /// </summary>
    public static void NotifyWaveStarted(int waveIndex)
    {
        CurrentWave = waveIndex;
        IsNightWave = IsNightWaveIndex(waveIndex);

        Debug.Log(IsNightWave ? "[Wave] Night Mode ON" : "[Wave] Night Mode OFF");
    }

    /// <summary>True for waves 7, 14, 21, … (same rule as <see cref="IsNightWave"/> after that wave starts).</summary>
    public static bool IsNightWaveIndex(int waveIndex)
    {
        return waveIndex > 0 && waveIndex % NightWaveInterval == 0;
    }
}
