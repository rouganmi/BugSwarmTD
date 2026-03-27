using TMPro;
using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;

    private void OnEnable()
    {
        GameEvents.OnGoldChanged += UpdateGold;
    }

    private void OnDisable()
    {
        GameEvents.OnGoldChanged -= UpdateGold;
    }

    private void UpdateGold(int value)
    {
        goldText.text = $"金币: {value}";
    }
}
