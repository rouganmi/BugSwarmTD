using UnityEngine;

public class CurrencySystem : MonoBehaviour
{
    [Header("Currency Settings")]
    public int startGold = 100;

    private int currentGold;

    private void Start()
    {
        currentGold = startGold;
        Debug.Log("Gold: " + currentGold);
    }

    public int GetCurrentGold()
    {
        return currentGold;
    }

    public bool HasEnoughGold(int amount)
    {
        return currentGold >= amount;
    }

    public bool SpendGold(int amount)
{
    Debug.Log("Try spend gold: " + amount + ", Current gold: " + currentGold);

    if (currentGold < amount)
    {
        Debug.Log("Not enough gold!");
        return false;
    }

    currentGold -= amount;
    Debug.Log("Gold after spend: " + currentGold);
    return true;
}

    public void AddGold(int amount)
    {
        currentGold += amount;
        Debug.Log("Gold: " + currentGold);
    }
}
