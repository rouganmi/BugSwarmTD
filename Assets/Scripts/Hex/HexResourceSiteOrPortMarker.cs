using UnityEngine;

public enum HexResourceSiteOrPortType
{
    ResourceSite,
    ResourcePort
}

public sealed class HexResourceSiteOrPortMarker : MonoBehaviour
{
    [SerializeField] private HexResourceSiteOrPortType type = HexResourceSiteOrPortType.ResourceSite;

    public HexResourceSiteOrPortType Type => type;
}
