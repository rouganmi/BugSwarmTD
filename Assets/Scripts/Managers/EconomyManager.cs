using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [SerializeField] private int startGold = 100;
    private int currentGold;

    public int CurrentGold => currentGold;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        currentGold = startGold;
        GameEvents.OnGoldChanged?.Invoke(currentGold);
    }

    public bool CanAfford(int amount)
    {
        return currentGold >= amount;
    }

    public bool SpendGold(int amount)
    {
        if (!CanAfford(amount)) return false;

        currentGold -= amount;
        GameEvents.OnGoldChanged?.Invoke(currentGold);
        return true;
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        GameEvents.OnGoldChanged?.Invoke(currentGold);
    }
}
