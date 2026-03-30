using UnityEngine;

namespace BugSwarmTD.Editor.Diagnostics
{
    /// <summary>
    /// Read-only quaternion checks using components and magnitude squared only (no euler conversion).
    /// </summary>
    public static class RotationDiagnosticsMath
    {
        public const float MinMagSq = 1e-12f;

        /// <summary>Default: ignore typical serialized float drift on transforms.</summary>
        public const float UnitMagSqToleranceDefault = 0.001f;

        /// <summary>Strict: flag any |magSq - 1| larger than this (very small deviations).</summary>
        public const float UnitMagSqToleranceStrict = 1e-7f;

        public static float MagnitudeSquared(in Quaternion q)
        {
            return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        }

        public static bool HasNaN(in Quaternion q)
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w);
        }

        public static bool HasInfinity(in Quaternion q)
        {
            return float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w);
        }

        /// <summary>Returns true if this quaternion should be reported for the given unit-length tolerance.</summary>
        public static bool IsSuspicious(
            in Quaternion q,
            float unitMagSqTolerance,
            out float magSq,
            out float absMagSqMinus1,
            out bool nan,
            out bool inf)
        {
            nan = HasNaN(q);
            inf = HasInfinity(q);
            magSq = MagnitudeSquared(q);
            absMagSqMinus1 = Mathf.Abs(magSq - 1f);

            if (float.IsNaN(magSq) || float.IsInfinity(magSq))
                return true;

            if (nan || inf)
                return true;

            if (magSq <= MinMagSq)
                return true;

            if (absMagSqMinus1 > unitMagSqTolerance)
                return true;

            return false;
        }
    }
}
