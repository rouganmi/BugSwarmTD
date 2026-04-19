using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattlefieldMapRuntimeHost : MonoBehaviour
{
    [SerializeField] private MapDefinition mapDefinition;
    [SerializeField] private MapRuntimeState runtimeState = new MapRuntimeState();
    [SerializeField] private MapPoiRegistry poiRegistry = new MapPoiRegistry();
    [SerializeField] private PathTopology pathTopology = new PathTopology();
    [Header("Expansion Boundary Fallback / Debug Feed")]
    [Tooltip("Explicitly allows the host fallback/debug expansion-boundary feed when the assigned MapDefinition has no authored expansion-boundary definition.")]
    [SerializeField] private bool allowFallbackDebugExpansionBoundaryFeed;
    [Tooltip("Explicitly allows SpatialRuleService to use HexGridExpansionBoundaryProvider as the no-authored expansion-boundary transition fallback.")]
    [SerializeField] private bool allowProviderExpansionBoundaryFallback;
    [Tooltip("Fallback/debug-only expansion-boundary source. Used only when the assigned MapDefinition does not author an expansion-boundary definition.")]
    [SerializeField] private bool fallbackDebugFeedFormalExpansionBoundarySnapshot;
    [Tooltip("Fallback/debug-only ring radius. Used only when the assigned MapDefinition does not author an expansion-boundary definition.")]
    [SerializeField] private int fallbackDebugFormalExpansionBoundaryAllowedBuildRingRadius = 8;

    public MapDefinition MapDefinition => mapDefinition;
    public bool AllowProviderExpansionBoundaryFallback => allowProviderExpansionBoundaryFallback;

    void Awake()
    {
        ApplyFormalExpansionBoundarySnapshotFeed();
        ApplyFormalSpecialBuildBlockSnapshotFeed();
        ApplyFormalNestBufferSnapshotFeed();
        ApplyPoiRegistryRuntimeBinding();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyFormalExpansionBoundarySnapshotFeed();
        ApplyFormalSpecialBuildBlockSnapshotFeed();
        ApplyFormalNestBufferSnapshotFeed();
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
            ApplyFormalNestBufferSnapshotFeed();
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

        // Preferred source hierarchy:
        // 1. MapDefinition-authored expansion-boundary definition
        // 2. Explicitly gated runtime-host inspector fallback/debug feed, only when no authored definition exists
        // 3. Explicitly gated HexGridExpansionBoundaryProvider transition fallback in SpatialRuleService
        if (TryApplyAuthoredExpansionBoundarySnapshot())
            return;

        ApplyFallbackDebugExpansionBoundarySnapshotFeed();
    }

    bool TryApplyAuthoredExpansionBoundarySnapshot()
    {
        if (mapDefinition == null ||
            !mapDefinition.TryGetExpansionBoundaryDefinition(out MapExpansionBoundaryDefinition definition))
        {
            return false;
        }

        runtimeState.SetFormalExpansionBoundarySnapshot(definition);
        return true;
    }

    void ApplyFallbackDebugExpansionBoundarySnapshotFeed()
    {
        if (!allowFallbackDebugExpansionBoundaryFeed)
        {
            runtimeState.SetFormalExpansionBoundarySnapshot(
                false,
                fallbackDebugFormalExpansionBoundaryAllowedBuildRingRadius
            );
            return;
        }

        runtimeState.SetFormalExpansionBoundarySnapshot(
            fallbackDebugFeedFormalExpansionBoundarySnapshot,
            fallbackDebugFormalExpansionBoundaryAllowedBuildRingRadius
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

    void ApplyFormalNestBufferSnapshotFeed()
    {
        if (runtimeState == null)
            runtimeState = new MapRuntimeState();

        if (mapDefinition == null ||
            !mapDefinition.TryGetNestBufferDefinition(out MapNestBufferDefinition definition))
        {
            return;
        }

        runtimeState.SetFormalNestBufferSnapshot(definition);
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
