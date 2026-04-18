using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct MapExpansionBoundaryDefinition
{
    [Tooltip("Preferred authored source for the formal expansion-boundary snapshot. When disabled, the runtime host may supply its fallback/debug-only feed instead.")]
    [SerializeField] private bool hasDefinition;
    [Tooltip("Enables the formal ring-based expansion-boundary snapshot authored on the MapDefinition asset.")]
    [SerializeField] private bool hasFormalExpansionBoundarySnapshot;
    [Tooltip("Ring radius used by the formal expansion-boundary snapshot when the MapDefinition-authored source is enabled.")]
    [SerializeField] private int allowedBuildRingRadius;

    public bool HasDefinition => hasDefinition;
    public bool HasFormalExpansionBoundarySnapshot => hasFormalExpansionBoundarySnapshot;
    public int AllowedBuildRingRadius => Mathf.Max(0, allowedBuildRingRadius);
}

[Serializable]
public struct MapSpecialBuildBlockDefinition
{
    [SerializeField] private bool hasDefinition;
    [SerializeField] private bool hasFormalSpecialBuildBlockSnapshot;
    [SerializeField] private Vector2Int[] blockedCellCoordinates;

    public bool HasDefinition => hasDefinition;
    public bool HasFormalSpecialBuildBlockSnapshot => hasFormalSpecialBuildBlockSnapshot;
    public IReadOnlyList<Vector2Int> BlockedCellCoordinates => blockedCellCoordinates ?? Array.Empty<Vector2Int>();
}

[Serializable]
public struct MapNestBufferDefinition
{
    [SerializeField] private bool hasDefinition;
    [SerializeField] private bool hasFormalNestBufferSnapshot;
    [SerializeField] private Vector2Int[] bufferedCellCoordinates;

    public bool HasDefinition => hasDefinition;
    public bool HasFormalNestBufferSnapshot => hasFormalNestBufferSnapshot;
    public IReadOnlyList<Vector2Int> BufferedCellCoordinates => bufferedCellCoordinates ?? Array.Empty<Vector2Int>();
}

[CreateAssetMenu(menuName = "BugSwarmTD/Map/Map Definition", fileName = "MapDefinition")]
public sealed class MapDefinition : ScriptableObject
{
    [SerializeField] private string mapId = "chapter1_battlefield";
    [Header("Preferred Authored Sources")]
    [SerializeField] private MapExpansionBoundaryDefinition expansionBoundaryDefinition;
    [SerializeField] private MapSpecialBuildBlockDefinition specialBuildBlockDefinition;
    [SerializeField] private MapNestBufferDefinition nestBufferDefinition;
    [SerializeField] private MapRuntimeState runtimeStateSkeleton = new MapRuntimeState();
    [SerializeField] private MapPoiRegistry poiRegistry = new MapPoiRegistry();
    [SerializeField] private PathTopology pathTopology = new PathTopology();

    public string MapId => mapId;
    public MapExpansionBoundaryDefinition ExpansionBoundaryDefinition => expansionBoundaryDefinition;
    public MapSpecialBuildBlockDefinition SpecialBuildBlockDefinition => specialBuildBlockDefinition;
    public MapNestBufferDefinition NestBufferDefinition => nestBufferDefinition;
    public MapRuntimeState RuntimeStateSkeleton => runtimeStateSkeleton;
    public MapPoiRegistry PoiRegistry => poiRegistry;
    public PathTopology PathTopology => pathTopology;

    public bool TryGetExpansionBoundaryDefinition(out MapExpansionBoundaryDefinition definition)
    {
        definition = expansionBoundaryDefinition;
        return definition.HasDefinition;
    }

    public bool TryGetSpecialBuildBlockDefinition(out MapSpecialBuildBlockDefinition definition)
    {
        definition = specialBuildBlockDefinition;
        return definition.HasDefinition;
    }

    public bool TryGetNestBufferDefinition(out MapNestBufferDefinition definition)
    {
        definition = nestBufferDefinition;
        return definition.HasDefinition;
    }
}
