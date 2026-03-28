using UnityEngine;

/// <summary>
/// 旧敌人信息条入口；逻辑已合并到 <see cref="SelectionInfoPanel"/>。
/// 保留此类以便场景中未拆除的组件不报错；新代码请使用 <see cref="SelectionInfoPanel"/>。
/// </summary>
public class EnemyInfoPanelUI : MonoBehaviour
{
    public void Show(Enemy enemy)
    {
        SelectionInfoPanel.EnsureBuilt(FindObjectOfType<Canvas>());
        SelectionInfoPanel.Instance?.ShowEnemy(enemy);
    }

    public void Hide()
    {
        SelectionInfoPanel.Instance?.Hide();
    }

    public bool IsShowing => SelectionInfoPanel.Instance != null && SelectionInfoPanel.Instance.IsShowingEnemy;
}
