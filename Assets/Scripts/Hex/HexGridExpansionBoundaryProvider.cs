using UnityEngine;

public sealed class HexGridExpansionBoundaryProvider : MonoBehaviour
{
    [SerializeField] private int allowedBuildRingRadius = 8;

    public bool IsWithinTemporaryAllowedBuildBoundary(HexCell hexCell)
    {
        if (hexCell == null)
            return true;

        int ring = CubeRing(hexCell.GridX, hexCell.GridY);
        return ring <= Mathf.Max(0, allowedBuildRingRadius);
    }

    private static int CubeRing(int q, int r)
    {
        int s = -q - r;
        return Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(s)));
    }
}
