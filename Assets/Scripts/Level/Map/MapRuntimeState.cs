using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MapRuntimeState
{
    [SerializeField] private bool retainTransitionBridgeSources = true;
    [SerializeField] private bool chapter1SpatialFactConsolidationReady;
    [SerializeField] private bool hasAuthoredExpansionBoundaryDefinition;
    [SerializeField] private bool hasFormalExpansionBoundarySnapshot;
    [SerializeField] private int temporaryAllowedBuildRingRadius = 8;
    [SerializeField] private bool hasFormalSpecialBuildBlockSnapshot;
    [SerializeField] private List<Vector2Int> specialBuildBlockCellCoordinates = new List<Vector2Int>();
    [SerializeField] private bool hasFormalNestBufferSnapshot;
    [SerializeField] private List<Vector2Int> nestBufferCellCoordinates = new List<Vector2Int>();

    public bool RetainTransitionBridgeSources => retainTransitionBridgeSources;
    public bool Chapter1SpatialFactConsolidationReady => chapter1SpatialFactConsolidationReady;

    public void SetFormalExpansionBoundarySnapshot(bool hasSnapshot, int allowedBuildRingRadius)
    {
        hasAuthoredExpansionBoundaryDefinition = false;
        hasFormalExpansionBoundarySnapshot = hasSnapshot;
        temporaryAllowedBuildRingRadius = Mathf.Max(0, allowedBuildRingRadius);
    }

    public void SetFormalExpansionBoundarySnapshot(MapExpansionBoundaryDefinition definition)
    {
        hasAuthoredExpansionBoundaryDefinition = true;
        hasFormalExpansionBoundarySnapshot = definition.HasFormalExpansionBoundarySnapshot;
        temporaryAllowedBuildRingRadius = definition.AllowedBuildRingRadius;
    }

    public void SetFormalSpecialBuildBlockSnapshot(
        bool hasSnapshot,
        IReadOnlyList<Vector2Int> blockedCellCoordinates)
    {
        hasFormalSpecialBuildBlockSnapshot = hasSnapshot;

        if (specialBuildBlockCellCoordinates == null)
            specialBuildBlockCellCoordinates = new List<Vector2Int>();
        else
            specialBuildBlockCellCoordinates.Clear();

        if (blockedCellCoordinates == null)
            return;

        for (int i = 0; i < blockedCellCoordinates.Count; i++)
            specialBuildBlockCellCoordinates.Add(blockedCellCoordinates[i]);
    }

    public void SetFormalSpecialBuildBlockSnapshot(MapSpecialBuildBlockDefinition definition)
    {
        SetFormalSpecialBuildBlockSnapshot(
            definition.HasFormalSpecialBuildBlockSnapshot,
            definition.BlockedCellCoordinates
        );
    }

    public void SetFormalNestBufferSnapshot(
        bool hasSnapshot,
        IReadOnlyList<Vector2Int> bufferedCellCoordinates)
    {
        hasFormalNestBufferSnapshot = hasSnapshot;

        if (nestBufferCellCoordinates == null)
            nestBufferCellCoordinates = new List<Vector2Int>();
        else
            nestBufferCellCoordinates.Clear();

        if (bufferedCellCoordinates == null)
            return;

        for (int i = 0; i < bufferedCellCoordinates.Count; i++)
            nestBufferCellCoordinates.Add(bufferedCellCoordinates[i]);
    }

    public void SetFormalNestBufferSnapshot(MapNestBufferDefinition definition)
    {
        SetFormalNestBufferSnapshot(
            definition.HasFormalNestBufferSnapshot,
            definition.BufferedCellCoordinates
        );
    }

    public bool TryResolveExpansionBoundaryFact(HexCell hexCell, out bool isWithinExpansionBoundary)
    {
        if (!hasFormalExpansionBoundarySnapshot)
        {
            if (hasAuthoredExpansionBoundaryDefinition)
            {
                isWithinExpansionBoundary = false;
                return true;
            }

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

    public bool TryResolveSpecialBuildBlockFact(HexCell hexCell, out bool isInsideSpecialBuildBlockZone)
    {
        if (!hasFormalSpecialBuildBlockSnapshot)
        {
            isInsideSpecialBuildBlockZone = false;
            return false;
        }

        if (hexCell == null)
        {
            isInsideSpecialBuildBlockZone = false;
            return true;
        }

        var cellCoordinate = new Vector2Int(hexCell.GridX, hexCell.GridY);
        isInsideSpecialBuildBlockZone = specialBuildBlockCellCoordinates.Contains(cellCoordinate);
        return true;
    }

    public bool TryResolveNestBufferFact(HexCell hexCell, out bool isInsideNestBuffer)
    {
        if (!hasFormalNestBufferSnapshot)
        {
            isInsideNestBuffer = false;
            return false;
        }

        if (hexCell == null)
        {
            isInsideNestBuffer = false;
            return true;
        }

        var cellCoordinate = new Vector2Int(hexCell.GridX, hexCell.GridY);
        isInsideNestBuffer = nestBufferCellCoordinates.Contains(cellCoordinate);
        return true;
    }

    static int CubeRing(int q, int r)
    {
        int s = -q - r;
        return Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(s)));
    }
}
