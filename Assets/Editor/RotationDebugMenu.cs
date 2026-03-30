using UnityEditor;
using UnityEngine;

/// <summary>
/// Temporary editor entry to invoke <see cref="RotationDebug.ScanSceneForBadLocalRotationsOnce"/>.
/// Note: <c>RotationDebug</c> uses a static one-shot flag; if a scan already ran this domain (e.g. after Play),
/// this call may return immediately without re-scanning.
/// </summary>
public static class RotationDebugMenu
{
    const string MenuPath = "Tools/Rotation Debug/Scan Scene For Bad Local Rotations";

    [MenuItem(MenuPath)]
    static void Scan()
    {
        RotationDebug.ScanSceneForBadLocalRotationsOnce();
    }
}
