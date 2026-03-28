using System;

public static class GameEvents
{
    public static Action<int> OnGoldChanged;
    public static Action<int, int> OnBaseHpChanged;
    public static Action<int> OnWaveChanged;
    /// <summary>Fires before a night wave begins (after optional lead-in); argument is the upcoming wave index (7, 14, …).</summary>
    public static Action<int> OnNightWaveLeadIn;
    public static Action OnGameOver;
    public static Action OnEnemyKilled;
    public static Action OnTowerBuilt;
    public static Action OnTowerSold;
}