using System.Collections.Generic;
using UnityEngine;

public class DebugVisualHider : MonoBehaviour
{
    [Header("Name filters (prefix match)")]
    [SerializeField] private List<string> hideNamePrefixes = new List<string>
    {
        "PathPoint_",
        "CameraRig",
    };

    [Header("Components")]
    [SerializeField] private bool disableMeshRenderers = true;
    [SerializeField] private bool disableSpriteRenderers = true;
    [SerializeField] private bool disableLineRenderers = true;

    [Header("Scope")]
    [SerializeField] private bool includeInactive = true;

    private void Awake()
    {
        HideDebugVisuals();
    }

    public void HideDebugVisuals()
    {
        if (disableMeshRenderers)
        {
            foreach (var r in FindObjectsByType<MeshRenderer>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r == null) continue;
                if (ShouldHide(r.gameObject.name)) r.enabled = false;
            }
        }

        if (disableSpriteRenderers)
        {
            foreach (var r in FindObjectsByType<SpriteRenderer>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r == null) continue;
                if (ShouldHide(r.gameObject.name)) r.enabled = false;
            }
        }

        if (disableLineRenderers)
        {
            foreach (var r in FindObjectsByType<LineRenderer>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r == null) continue;
                if (ShouldHide(r.gameObject.name)) r.enabled = false;
            }
        }
    }

    private bool ShouldHide(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return false;
        for (int i = 0; i < hideNamePrefixes.Count; i++)
        {
            var p = hideNamePrefixes[i];
            if (string.IsNullOrEmpty(p)) continue;
            if (objectName.StartsWith(p)) return true;
        }
        return false;
    }
}

