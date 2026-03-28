using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// IPointer enter/exit on a build menu row to drive <see cref="BuildPreviewController"/>.
/// </summary>
public class BuildTowerRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public BuildPreviewController preview;
    public BuildTowerOption option;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (preview == null || option == null) return;
        preview.ShowHover(option);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (preview != null)
            preview.Hide();
    }
}
