using UnityEngine;

/// <summary>
/// One-shot scan after play starts to print the first transform with non-unit localRotation (helps when stack is only GUIUtility).
/// </summary>
[DefaultExecutionOrder(32000)]
public sealed class RotationCorruptionProbe : MonoBehaviour
{
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject(nameof(RotationCorruptionProbe) + "_Bootstrap");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        go.AddComponent<RotationCorruptionProbe>();
    }
#endif

    [Tooltip("Later frame catches drift from long-running Rotate() etc.")]
    [SerializeField] int scanAtFrame = 30;

    int _frame;

    void LateUpdate()
    {
        _frame++;
        if (_frame != scanAtFrame)
            return;
        RotationDebug.ScanSceneForBadLocalRotationsOnce();
        Destroy(gameObject);
    }
}
