#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public enum PoiDebugObservationSource
{
    FormalSnapshotHit,
    FormalSnapshotNoPoi,
    FallbackMarkerHit,
    NoPoi
}

public readonly struct PoiDebugObservation
{
    public Vector2Int HexCoordinates { get; }
    public bool HasAssignedMapDefinition { get; }
    public bool HasFormalPoiSnapshotAvailable { get; }
    public bool ResolvesToPoi { get; }
    public MapPoiType ResolvedPoiType { get; }
    public PoiDebugObservationSource Source { get; }

    public PoiDebugObservation(
        Vector2Int hexCoordinates,
        bool hasAssignedMapDefinition,
        bool hasFormalPoiSnapshotAvailable,
        bool resolvesToPoi,
        MapPoiType resolvedPoiType,
        PoiDebugObservationSource source)
    {
        HexCoordinates = hexCoordinates;
        HasAssignedMapDefinition = hasAssignedMapDefinition;
        HasFormalPoiSnapshotAvailable = hasFormalPoiSnapshotAvailable;
        ResolvesToPoi = resolvesToPoi;
        ResolvedPoiType = resolvedPoiType;
        Source = source;
    }

    public string ToMultilineString()
    {
        var builder = new StringBuilder(192);
        builder.Append("Hex: ").Append(HexCoordinates.x).Append(",").Append(HexCoordinates.y).AppendLine();
        builder.Append("MapDefinition assigned: ").Append(HasAssignedMapDefinition ? "yes" : "no").AppendLine();
        builder.Append("Formal POI snapshot available: ")
            .Append(HasFormalPoiSnapshotAvailable ? "yes" : "no")
            .AppendLine();
        builder.Append("Resolves to POI: ").Append(ResolvesToPoi ? "yes" : "no").AppendLine();
        builder.Append("Resolved POI type: ").Append(ResolvedPoiType).AppendLine();
        builder.Append("Source: ").Append(Source);
        return builder.ToString();
    }
}

[DisallowMultipleComponent]
public sealed class PoiDebugObservationHelper : MonoBehaviour
{
    const string LogPrefix = "[PoiDebug]";
    static readonly Rect OverlayRect = new Rect(16f, 16f, 360f, 132f);

    [Header("Raycast")]
    [SerializeField] Camera raycastCamera;
    [SerializeField] float raycastDistance = 500f;
    [SerializeField] LayerMask raycastMask = Physics.DefaultRaycastLayers;
    [SerializeField] bool ignorePointerOverUi = true;

    [Header("Observation")]
    [SerializeField] bool observeHoveredHex = true;
    [SerializeField] bool observeClickedHex = true;
    [SerializeField] int clickMouseButton = 0;

    [Header("Output")]
    [SerializeField] bool logToConsole = true;
    [SerializeField] bool drawOverlay = true;

    HexCell _hoveredCell;
    PoiDebugObservation _lastObservation;
    bool _hasObservation;
    string _lastTrigger = "none";

    void Update()
    {
        if (observeHoveredHex)
            ObserveHoveredHex();

        if (observeClickedHex && Input.GetMouseButtonDown(clickMouseButton))
            ObserveClickedHex();
    }

    void OnGUI()
    {
        if (!drawOverlay || !_hasObservation)
            return;

        GUI.TextArea(
            OverlayRect,
            $"POI Debug ({_lastTrigger})\n{_lastObservation.ToMultilineString()}"
        );
    }

    void ObserveHoveredHex()
    {
        HexCell hoveredCell = ResolveHexUnderPointer();
        if (_hoveredCell == hoveredCell)
            return;

        _hoveredCell = hoveredCell;
        if (_hoveredCell == null)
            return;

        CaptureObservation(_hoveredCell, "hover");
    }

    void ObserveClickedHex()
    {
        HexCell clickedCell = ResolveHexUnderPointer();
        if (clickedCell == null)
            return;

        CaptureObservation(clickedCell, "click");
    }

    void CaptureObservation(HexCell hexCell, string trigger)
    {
        _lastObservation = SpatialRuleService.ObservePoiResolution(hexCell);
        _hasObservation = true;
        _lastTrigger = trigger;

        if (!logToConsole)
            return;

        Debug.Log($"{LogPrefix} ({trigger})\n{_lastObservation.ToMultilineString()}", hexCell);
    }

    HexCell ResolveHexUnderPointer()
    {
        if (ignorePointerOverUi &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        Camera cameraToUse = raycastCamera != null ? raycastCamera : Camera.main;
        if (cameraToUse == null)
            return null;

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                raycastDistance,
                raycastMask,
                QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return hit.collider.GetComponent<HexCell>()
            ?? hit.collider.GetComponentInParent<HexCell>()
            ?? hit.collider.GetComponentInChildren<HexCell>();
    }
}
#endif
