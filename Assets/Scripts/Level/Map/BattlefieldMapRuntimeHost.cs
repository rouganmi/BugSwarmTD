using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattlefieldMapRuntimeHost : MonoBehaviour
{
    [SerializeField] private MapDefinition mapDefinition;
    [SerializeField] private MapRuntimeState runtimeState = new MapRuntimeState();
    [SerializeField] private MapPoiRegistry poiRegistry = new MapPoiRegistry();
    [SerializeField] private PathTopology pathTopology = new PathTopology();

    public MapDefinition MapDefinition => mapDefinition;

    public MapRuntimeState RuntimeState
    {
        get
        {
            if (runtimeState == null)
            {
                runtimeState =
                    mapDefinition != null && mapDefinition.RuntimeStateSkeleton != null ?
                    mapDefinition.RuntimeStateSkeleton :
                    new MapRuntimeState();
            }

            return runtimeState;
        }
    }

    public MapPoiRegistry PoiRegistry
    {
        get
        {
            if (poiRegistry == null)
            {
                poiRegistry =
                    mapDefinition != null && mapDefinition.PoiRegistry != null ?
                    mapDefinition.PoiRegistry :
                    new MapPoiRegistry();
            }

            return poiRegistry;
        }
    }

    public PathTopology PathTopology
    {
        get
        {
            if (pathTopology == null)
            {
                pathTopology =
                    mapDefinition != null && mapDefinition.PathTopology != null ?
                    mapDefinition.PathTopology :
                    new PathTopology();
            }

            return pathTopology;
        }
    }
}
