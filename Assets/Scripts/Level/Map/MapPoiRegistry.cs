using System;
using UnityEngine;

public enum MapPoiType
{
    None,
    ResourceSite,
    ResourcePort
}

public readonly struct MapPoiFact
{
    public bool HasPoi { get; }
    public MapPoiType PoiType { get; }

    public MapPoiFact(bool hasPoi, MapPoiType poiType)
    {
        HasPoi = hasPoi;
        PoiType = poiType;
    }
}

[Serializable]
public sealed class MapPoiRegistry
{
    public MapPoiFact ReadTransitionBridgePoi(HexCell hexCell)
    {
        if (hexCell == null)
            return default;

        var marker = hexCell.GetComponent<HexResourceSiteOrPortMarker>();
        if (marker == null)
            return default;

        MapPoiType poiType =
            marker.Type == HexResourceSiteOrPortType.ResourcePort ?
            MapPoiType.ResourcePort :
            MapPoiType.ResourceSite;

        return new MapPoiFact(true, poiType);
    }
}
