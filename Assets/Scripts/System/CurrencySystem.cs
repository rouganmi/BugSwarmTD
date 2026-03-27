using UnityEngine;

public class CurrencySystem : MonoBehaviour
{
    [Header("Currency Settings")]
    public int startGold = 100;

    private int currentGold;
    private EconomyManager _economy;

    private void Start()
    {
        _economy = EconomyManager.Instance;

        if (_economy == null)
        {
            currentGold = startGold;
            Debug.Log("[CurrencySystem] Gold: " + currentGold);
        }
        else
        {
            Debug.Log("[CurrencySystem] Using EconomyManager as gold source. Gold: " + _economy.CurrentGold);
        }
    }

    public int GetCurrentGold()
    {
        if (_economy != null) return _economy.CurrentGold;
        return currentGold;
    }

    public bool HasEnoughGold(int amount)
    {
        if (_economy != null) return _economy.CanAfford(amount);
        return currentGold >= amount;
    }

    public bool SpendGold(int amount)
    {
        int before = GetCurrentGold();
        Debug.Log($"[CurrencySystem] Try spend gold: {amount}, Current gold: {before}");

        bool ok;
        if (_economy != null)
        {
            ok = _economy.SpendGold(amount);
        }
        else
        {
            if (currentGold < amount)
            {
                Debug.Log("[CurrencySystem] Not enough gold!");
                return false;
            }
            currentGold -= amount;
            ok = true;
        }

        Debug.Log($"[CurrencySystem] Spend result: {ok}, Gold after spend: {GetCurrentGold()}");
        return ok;
    }

    public void AddGold(int amount)
    {
        if (_economy != null)
        {
            _economy.AddGold(amount);
            Debug.Log("[CurrencySystem] Gold: " + _economy.CurrentGold);
            return;
        }

        currentGold += amount;
        Debug.Log("[CurrencySystem] Gold: " + currentGold);
    }
}
