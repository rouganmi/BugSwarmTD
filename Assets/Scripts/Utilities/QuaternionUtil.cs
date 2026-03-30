using UnityEngine;

/// <summary>Thin wrapper so existing call sites keep compiling; logic lives in <see cref="RotationDebug"/>.</summary>
public static class QuaternionUtil
{
    public static bool IsFinite(Quaternion q) => RotationDebug.IsFinite(q);

    public static Quaternion NormalizeOrIdentity(Quaternion q) => RotationDebug.NormalizeOrIdentity(q);
}
