using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattlefieldMapRuntimeHost : MonoBehaviour
{
    [SerializeField] private MapDefinition mapDefinition;
    [SerializeField] private MapRuntimeState runtimeState = new MapRuntimeState();
    [SerializeField] private MapPoiRegistry poiRegistry = new MapPoiRegistry();
    [SerializeField] private PathTopology pathTopology = new PathTopology();
    [SerializeField] private bool feedFormalExpansionBoundarySnapshot;
    [SerializeField] private int formalExpansionBoundaryAllowedBuildRingRadius = 8;

    public MapDefinition MapDefinition => mapDefinition;

    void Awake()
    {
        ApplyFormalExpansionBoundarySnapshotFeed();
        ApplyFormalSpecialBuildBlockSnapshotFeed();
        ApplyPoiRegistryRuntimeBinding();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyFormalExpansionBoundarySnapshotFeed();
        ApplyFormalSpecialBuildBlockSnapshotFeed();
        ApplyPoiRegistryRuntimeBinding();
    }
#endif

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

            ApplyFormalExpansionBoundarySnapshotFeed();
            ApplyFormalSpecialBuildBlockSnapshotFeed();
            return runtimeState;
        }
    }

    public MapPoiRegistry PoiRegistry
    {
        get
        {
            ApplyPoiRegistryRuntimeBinding();
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

    void ApplyFormalExpansionBoundarySnapshotFeed()
    {
        if (runtimeState == null)
            runtimeState = new MapRuntimeState();

        if (mapDefinition != null &&
            mapDefinition.TryGetExpansionBoundaryDefinition(out MapExpansionBoundaryDefinition definition))
        {
            runtimeState.SetFormalExpansionBoundarySnapshot(definition);
            return;
        }

        runtimeState.SetFormalExpansionBoundarySnapshot(
            feedFormalExpansionBoundarySnapshot,
            formalExpansionBoundaryAllowedBuildRingRadius
        );
    }

    void ApplyPoiRegistryRuntimeBinding()
    {
        if (mapDefinition != null && mapDefinition.PoiRegistry != null)
            poiRegistry = mapDefinition.PoiRegistry;
        else if (poiRegistry == null)
            poiRegistry = new MapPoiRegistry();

        if (poiRegistry == null)
            return;

        bool allowTransitionBridgeFallback = runtimeState == null || runtimeState.RetainTransitionBridgeSources;
        poiRegistry.SetTransitionBridgeFallbackEnabled(allowTransitionBridgeFallback);
    }

    void ApplyFormalSpecialBuildBlockSnapshotFeed()
    {
        if (runtimeState == null)
            runtimeState = new MapRuntimeState();

        if (mapDefinition == null ||
            !mapDefinition.TryGetSpecialBuildBlockDefinition(out MapSpecialBuildBlockDefinition definition))
        {
            return;
        }

        runtimeState.SetFormalSpecialBuildBlockSnapshot(definition);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public PoiDebugObservation ObservePoiResolution(HexCell hexCell)
    {
        ApplyPoiRegistryRuntimeBinding();
        if (poiRegistry == null)
        {
            return new PoiDebugObservation(
                hexCell != null ? new Vector2Int(hexCell.GridX, hexCell.GridY) : default,
                mapDefinition != null,
                false,
                false,
                MapPoiType.None,
                PoiDebugObservationSource.NoPoi
            );
        }

        return poiRegistry.ObservePoiResolution(hexCell, mapDefinition != null);
    }
#endif
}
