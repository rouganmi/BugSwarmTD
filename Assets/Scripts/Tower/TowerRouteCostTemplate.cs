using UnityEngine;

/// <summary>Shared cost progression for route upgrades (inspectable, not final balance).</summary>
public static class TowerRouteCostTemplate
{
    public static int FirstRouteUpgradeCost(int towerBaseBuildCost)
    {
        return Mathf.Max(25, towerBaseBuildCost);
    }

    public static int SecondRouteUpgradeCost(int firstRouteUpgradePaidGold)
    {
        return Mathf.RoundToInt(firstRouteUpgradePaidGold * 1.35f) + 10;
    }
}
