using System.Collections.Generic;
using UnityEngine;

public enum MapFactCategory
{
    SpecialBuildBlockZone,
    ExpansionBoundary,
    ResourceSiteOrCollectionPort,
    NestBufferZone
}

public enum MapFactFormalOwner
{
    SpatialRuleService,
    MapPoiRegistry
}

public enum MapFactTransitionRetentionMode
{
    RetainTemporarySourceUntilChapter1SpatialFactConsolidation
}

public readonly struct MapFactTransitionEntry
{
    public MapFactCategory Category { get; }
    public string CurrentSource { get; }
    public MapFactFormalOwner FormalOwner { get; }
    public MapFactTransitionRetentionMode TransitionRetentionMode { get; }

    public MapFactTransitionEntry(
        MapFactCategory category,
        string currentSource,
        MapFactFormalOwner formalOwner,
        MapFactTransitionRetentionMode transitionRetentionMode)
    {
        Category = category;
        CurrentSource = currentSource;
        FormalOwner = formalOwner;
        TransitionRetentionMode = transitionRetentionMode;
    }
}

public readonly struct BattlefieldMapBuildFacts
{
    public bool IsWithinExpansionBoundary { get; }
    public bool IsInsideNestBuffer { get; }
    public bool IsOnResourceSiteOrPort { get; }
    public bool IsInsideSpecialBuildBlockZone { get; }
    public bool HasMapRuleBlock { get; }
    public MapBlockingTag MapBlockingTag { get; }

    public BattlefieldMapBuildFacts(
        bool isWithinExpansionBoundary,
        bool isInsideNestBuffer,
        bool isOnResourceSiteOrPort,
        bool isInsideSpecialBuildBlockZone,
        bool hasMapRuleBlock,
        MapBlockingTag mapBlockingTag)
    {
        IsWithinExpansionBoundary = isWithinExpansionBoundary;
        IsInsideNestBuffer = isInsideNestBuffer;
        IsOnResourceSiteOrPort = isOnResourceSiteOrPort;
        IsInsideSpecialBuildBlockZone = isInsideSpecialBuildBlockZone;
        HasMapRuleBlock = hasMapRuleBlock;
        MapBlockingTag = mapBlockingTag;
    }
}

public static class SpatialRuleService
{
    static readonly MapFactTransitionEntry[] TransitionEntries =
    {
        new MapFactTransitionEntry(
            MapFactCategory.SpecialBuildBlockZone,
            "HexSpecialBuildBlockMarker on the HexCell GameObject",
            MapFactFormalOwner.SpatialRuleService,
            MapFactTransitionRetentionMode.RetainTemporarySourceUntilChapter1SpatialFactConsolidation
        ),
        new MapFactTransitionEntry(
            MapFactCategory.ExpansionBoundary,
            "HexGridExpansionBoundaryProvider on the HexGridGenerator-side parent chain",
            MapFactFormalOwner.SpatialRuleService,
            MapFactTransitionRetentionMode.RetainTemporarySourceUntilChapter1SpatialFactConsolidation
        ),
        new MapFactTransitionEntry(
            MapFactCategory.ResourceSiteOrCollectionPort,
            "HexResourceSiteOrPortMarker on the HexCell GameObject",
            MapFactFormalOwner.MapPoiRegistry,
            MapFactTransitionRetentionMode.RetainTemporarySourceUntilChapter1SpatialFactConsolidation
        ),
        new MapFactTransitionEntry(
            MapFactCategory.NestBufferZone,
            "HexNestBufferMarker on the HexCell GameObject",
            MapFactFormalOwner.SpatialRuleService,
            MapFactTransitionRetentionMode.RetainTemporarySourceUntilChapter1SpatialFactConsolidation
        )
    };

    public static IReadOnlyList<MapFactTransitionEntry> BridgeTransitionEntries => TransitionEntries;

    public static BattlefieldMapBuildFacts ResolveBuildFacts(HexCell hexCell)
    {
        BattlefieldMapRuntimeHost runtimeHost = ResolveRuntimeHost(hexCell);
        bool isWithinExpansionBoundary = ReadExpansionBoundaryFact(hexCell, runtimeHost);
        bool isInsideSpecialBuildBlockZone = ReadSpecialBuildBlockTransitionFact(hexCell);
        bool isInsideNestBuffer = ReadNestBufferTransitionFact(hexCell);
        bool isOnResourceSiteOrPort = ReadPoiTransitionFact(hexCell, runtimeHost);
        bool hasMapRuleBlock =
            !isWithinExpansionBoundary || isInsideSpecialBuildBlockZone || isInsideNestBuffer;
        MapBlockingTag mapBlockingTag =
            !isWithinExpansionBoundary ? MapBlockingTag.ExpansionBoundaryBlocked :
            isInsideSpecialBuildBlockZone ? MapBlockingTag.SpecialZoneBlocked :
            isInsideNestBuffer ? MapBlockingTag.NestBufferBlocked :
            MapBlockingTag.None;

        return new BattlefieldMapBuildFacts(
            isWithinExpansionBoundary,
            isInsideNestBuffer,
            isOnResourceSiteOrPort,
            isInsideSpecialBuildBlockZone,
            hasMapRuleBlock,
            mapBlockingTag
        );
    }

    static BattlefieldMapRuntimeHost ResolveRuntimeHost(HexCell hexCell)
    {
        return hexCell != null ? hexCell.GetComponentInParent<BattlefieldMapRuntimeHost>() : null;
    }

    static bool ReadExpansionBoundaryFact(HexCell hexCell, BattlefieldMapRuntimeHost runtimeHost)
    {
        if (runtimeHost != null &&
            runtimeHost.RuntimeState.TryResolveExpansionBoundaryFact(hexCell, out bool isWithinExpansionBoundary))
        {
            return isWithinExpansionBoundary;
        }

        return ReadTransitionFallbackExpansionBoundaryFact(hexCell);
    }

    static bool ReadTransitionFallbackExpansionBoundaryFact(HexCell hexCell)
    {
        var provider =
            hexCell != null ? hexCell.GetComponentInParent<HexGridExpansionBoundaryProvider>() : null;
        return provider == null || provider.IsWithinTemporaryAllowedBuildBoundary(hexCell);
    }

    static bool ReadSpecialBuildBlockTransitionFact(HexCell hexCell)
    {
        return hexCell != null && hexCell.GetComponent<HexSpecialBuildBlockMarker>() != null;
    }

    static bool ReadNestBufferTransitionFact(HexCell hexCell)
    {
        return hexCell != null && hexCell.GetComponent<HexNestBufferMarker>() != null;
    }

    static bool ReadPoiTransitionFact(HexCell hexCell, BattlefieldMapRuntimeHost runtimeHost)
    {
        if (runtimeHost != null)
            return runtimeHost.PoiRegistry.ReadTransitionBridgePoi(hexCell).HasPoi;

        return ReadTransitionFallbackPoiFact(hexCell);
    }

    static bool ReadTransitionFallbackPoiFact(HexCell hexCell)
    {
        return hexCell != null && hexCell.GetComponent<HexResourceSiteOrPortMarker>() != null;
    }
}
