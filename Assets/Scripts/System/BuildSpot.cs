using UnityEngine;

public class BuildSpot : MonoBehaviour
{
    [Header("Build State")]
    public bool isOccupied = false;

    private Tower currentTower;

    public bool CanBuild()
    {
        return !isOccupied;
    }

    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
    }

    public void SetCurrentTower(Tower tower)
    {
        currentTower = tower;
        isOccupied = tower != null;
    }

    public Tower GetCurrentTower()
    {
        return currentTower;
    }

    public void ClearTower()
    {
        currentTower = null;
        isOccupied = false;
    }
}