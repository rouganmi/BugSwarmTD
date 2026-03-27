using System;

public static class GameEvents
{
    public static Action<int> OnGoldChanged;
    public static Action<int, int> OnBaseHpChanged;
    public static Action<int> OnWaveChanged;
    public static Action OnGameOver;
    public static Action OnEnemyKilled;
    public static Action OnTowerBuilt;
    public static Action OnTowerSold;
}