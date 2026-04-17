using System;
using UnityEngine;

[Serializable]
public sealed class MapRuntimeState
{
    [SerializeField] private bool retainTransitionBridgeSources = true;
    [SerializeField] private bool chapter1SpatialFactConsolidationReady;

    public bool RetainTransitionBridgeSources => retainTransitionBridgeSources;
    public bool Chapter1SpatialFactConsolidationReady => chapter1SpatialFactConsolidationReady;
}
