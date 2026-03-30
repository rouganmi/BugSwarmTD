using System.Text;
using UnityEngine;

/// <summary>
/// Diagnostics for non-unit quaternions that trigger Unity's QuaternionToEuler warnings (often via GUIUtility.ProcessEvent).
/// </summary>
public static class RotationDebug
{
    const float MagSqTolerance = 0.02f; // |s - 1| above this => log as suspicious incoming value

    static bool _sceneScanCompleted;

    public static bool IsFinite(Quaternion q)
    {
        return !(float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w)
                 || float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w));
    }

    /// <summary>True if quaternion is finite and length is ~1 (Unity expects unit quaternions on transforms).</summary>
    public static bool IsApproximatelyUnit(Quaternion q, float magSqTol = MagSqTolerance)
    {
        if (!IsFinite(q))
            return false;
        float s = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (float.IsInfinity(s) || s < 1e-20f)
            return false;
        return Mathf.Abs(s - 1f) <= magSqTol;
    }

    /// <summary>Finite quaternions are normalized to unit length; degenerate values become identity.</summary>
    public static Quaternion NormalizeOrIdentity(Quaternion q)
    {
        if (!IsFinite(q))
            return Quaternion.identity;

        float s = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (s < 1e-20f || float.IsInfinity(s))
            return Quaternion.identity;

        float inv = 1f / Mathf.Sqrt(s);
        return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
    }

    public static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "";
        var sb = new StringBuilder(128);
        Transform cur = t;
        while (cur != null)
        {
            if (sb.Length > 0)
                sb.Insert(0, '/');
            sb.Insert(0, string.IsNullOrEmpty(cur.name) ? "?" : cur.name);
            cur = cur.parent;
        }
        return sb.ToString();
    }

    /// <summary>Walk loaded objects once; log first transform whose localRotation is not approximately unit.</summary>
    public static void ScanSceneForBadLocalRotationsOnce()
    {
        if (_sceneScanCompleted)
            return;
        _sceneScanCompleted = true;

        var transforms = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null)
                continue;
            Quaternion q = t.localRotation;
            if (IsApproximatelyUnit(q, MagSqTolerance))
                continue;

            float s = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            Debug.LogError(
                $"[RotationDebug] First bad localRotation in scene. Object={t.name} Path={GetHierarchyPath(t)} " +
                $"Value=({q.x:F6},{q.y:F6},{q.z:F6},{q.w:F6}) magSq={s:F6} InstanceID={t.GetInstanceID()}",
                t.gameObject);
            return;
        }

        Debug.Log("[RotationDebug] One-shot scan: no non-unit localRotation found (within tolerance).");
    }
}
