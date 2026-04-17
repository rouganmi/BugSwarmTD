using UnityEngine;

[CreateAssetMenu(menuName = "BugSwarmTD/Map/Map Definition", fileName = "MapDefinition")]
public sealed class MapDefinition : ScriptableObject
{
    [SerializeField] private string mapId = "chapter1_battlefield";
    [SerializeField] private MapRuntimeState runtimeStateSkeleton = new MapRuntimeState();
    [SerializeField] private MapPoiRegistry poiRegistry = new MapPoiRegistry();
    [SerializeField] private PathTopology pathTopology = new PathTopology();

    public string MapId => mapId;
    public MapRuntimeState RuntimeStateSkeleton => runtimeStateSkeleton;
    public MapPoiRegistry PoiRegistry => poiRegistry;
    public PathTopology PathTopology => pathTopology;
}
