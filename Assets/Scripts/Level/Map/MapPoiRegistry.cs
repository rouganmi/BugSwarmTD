using System;
using System.Collections.Generic;
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
public struct MapPoiCoordinateEntry
{
    [SerializeField] private Vector2Int hexCoordinate;
    [SerializeField] private MapPoiType poiType;

    public Vector2Int HexCoordinate => hexCoordinate;
    public MapPoiType PoiType => poiType;
}

[Serializable]
public sealed class MapPoiRegistry
{
    [SerializeField] private bool hasFormalPoiSnapshot;
    [SerializeField] private List<MapPoiCoordinateEntry> formalPoiEntries = new List<MapPoiCoordinateEntry>();

    bool _allowTransitionBridgeFallback = true;

    public void SetTransitionBridgeFallbackEnabled(bool enabled)
    {
        _allowTransitionBridgeFallback = enabled;
    }

    public MapPoiFact ReadTransitionBridgePoi(HexCell hexCell)
    {
        if (TryReadFormalPoiFact(hexCell, out MapPoiFact poiFact))
            return poiFact;

        if (_allowTransitionBridgeFallback)
            return ReadTransitionBridgePoiFallback(hexCell);

        return default;
    }

    bool TryReadFormalPoiFact(HexCell hexCell, out MapPoiFact poiFact)
    {
        if (!hasFormalPoiSnapshot)
        {
            poiFact = default;
            return false;
        }

        if (hexCell == null)
        {
            poiFact = default;
            return true;
        }

        Vector2Int cellCoordinate = new Vector2Int(hexCell.GridX, hexCell.GridY);
        for (int i = 0; i < formalPoiEntries.Count; i++)
        {
            MapPoiCoordinateEntry entry = formalPoiEntries[i];
            if (entry.HexCoordinate != cellCoordinate)
                continue;

            poiFact =
                entry.PoiType == MapPoiType.None ?
                default :
                new MapPoiFact(true, entry.PoiType);
            return true;
        }

        poiFact = default;
        return true;
    }

    static MapPoiFact ReadTransitionBridgePoiFallback(HexCell hexCell)
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
