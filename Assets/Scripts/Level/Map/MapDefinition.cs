using System;
using UnityEngine;

[Serializable]
public struct MapExpansionBoundaryDefinition
{
    [SerializeField] private bool hasDefinition;
    [SerializeField] private bool hasFormalExpansionBoundarySnapshot;
    [SerializeField] private int allowedBuildRingRadius;

    public bool HasDefinition => hasDefinition;
    public bool HasFormalExpansionBoundarySnapshot => hasFormalExpansionBoundarySnapshot;
    public int AllowedBuildRingRadius => Mathf.Max(0, allowedBuildRingRadius);
}

[CreateAssetMenu(menuName = "BugSwarmTD/Map/Map Definition", fileName = "MapDefinition")]
public sealed class MapDefinition : ScriptableObject
{
    [SerializeField] private string mapId = "chapter1_battlefield";
    [SerializeField] private MapExpansionBoundaryDefinition expansionBoundaryDefinition;
    [SerializeField] private MapRuntimeState runtimeStateSkeleton = new MapRuntimeState();
    [SerializeField] private MapPoiRegistry poiRegistry = new MapPoiRegistry();
    [SerializeField] private PathTopology pathTopology = new PathTopology();

    public string MapId => mapId;
    public MapExpansionBoundaryDefinition ExpansionBoundaryDefinition => expansionBoundaryDefinition;
    public MapRuntimeState RuntimeStateSkeleton => runtimeStateSkeleton;
    public MapPoiRegistry PoiRegistry => poiRegistry;
    public PathTopology PathTopology => pathTopology;

    public bool TryGetExpansionBoundaryDefinition(out MapExpansionBoundaryDefinition definition)
    {
        definition = expansionBoundaryDefinition;
        return definition.HasDefinition;
    }
}
