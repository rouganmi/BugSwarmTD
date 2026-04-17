using System;
using UnityEngine;

[Serializable]
public sealed class MapRuntimeState
{
    [SerializeField] private bool retainTransitionBridgeSources = true;
    [SerializeField] private bool chapter1SpatialFactConsolidationReady;
    [SerializeField] private bool hasFormalExpansionBoundarySnapshot;
    [SerializeField] private int temporaryAllowedBuildRingRadius = 8;

    public bool RetainTransitionBridgeSources => retainTransitionBridgeSources;
    public bool Chapter1SpatialFactConsolidationReady => chapter1SpatialFactConsolidationReady;

    public void SetFormalExpansionBoundarySnapshot(bool hasSnapshot, int allowedBuildRingRadius)
    {
        hasFormalExpansionBoundarySnapshot = hasSnapshot;
        temporaryAllowedBuildRingRadius = Mathf.Max(0, allowedBuildRingRadius);
    }

    public bool TryResolveExpansionBoundaryFact(HexCell hexCell, out bool isWithinExpansionBoundary)
    {
        if (!hasFormalExpansionBoundarySnapshot)
        {
            isWithinExpansionBoundary = true;
            return false;
        }

        if (hexCell == null)
        {
            isWithinExpansionBoundary = true;
            return true;
        }

        int ring = CubeRing(hexCell.GridX, hexCell.GridY);
        isWithinExpansionBoundary = ring <= Mathf.Max(0, temporaryAllowedBuildRingRadius);
        return true;
    }

    static int CubeRing(int q, int r)
    {
        int s = -q - r;
        return Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(s)));
    }
}
