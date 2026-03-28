using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>Ground control zone for AOE Tower Route B. Uses overlap queries (no enemy Rigidbody required).</summary>
public class AreaZone : MonoBehaviour
{
    const float TickInterval = 0.5f;

    [SerializeField] float zoneRadius = 1.28f;
    float _durationSec;
    float _lingerOnExitSec;
    float _dotPerTick;
    float _spawnTime;
    float _nextTickTime;

    readonly HashSet<Enemy> _inside = new HashSet<Enemy>();

    Mesh _meshOil;
    Mesh _meshFire;
    static Material _matOilBase;
    static Material _matFireAccent;
    static bool _loggedHexWindingFix;
    static bool _loggedMaterialCullFix;
    static bool _loggedOverlayRenderFix;
    static bool _loggedDepthWriteDisabled;
    static bool _loggedYOffsetAdjust;
    static bool _loggedZoneRootWorld;
    static bool _loggedVisibilityBoost;
    static bool _loggedRenderQueueTransparentRange;
    static bool _loggedOverlayMaterialFix;
    static bool _loggedStableTransparentUnlit;

    const int ZoneMaterialSetupVersion = 4;

    /// <summary>Slight lift above ground plane for the hex overlay (readability vs z-fighting).</summary>
    const float VisualRootYOffset = 0.055f;
    const float VisualRadiusFactor = 1.05f;

    /// <summary>Transparent pass so the stain draws after opaque ground; ZWrite off keeps units readable on top.</summary>
    const int RenderQueueZoneBase = 3000;
    const int RenderQueueZoneFire = 3001;

    public static AreaZone Spawn(Vector3 groundHitPoint, float radius, float durationSec, float dotPerTick, float lingerAfterExitSec)
    {
        var go = new GameObject("AreaZone_Control");
        go.layer = 0;
        go.transform.SetParent(null, true);

        var z = go.AddComponent<AreaZone>();
        z._durationSec = durationSec;
        z._lingerOnExitSec = lingerAfterExitSec;
        z._dotPerTick = dotPerTick;
        z.zoneRadius = radius;
        z._spawnTime = Time.time;
        z._nextTickTime = Time.time + TickInterval;

        if (!TrySnapToBattlefieldGround(groundHitPoint, out Vector3 groundedWorld, out float groundY))
        {
            groundedWorld = new Vector3(groundHitPoint.x, groundHitPoint.y, groundHitPoint.z);
            groundY = groundedWorld.y;
        }

        go.transform.position = groundedWorld;

        Debug.Log($"[AOEControl] Ground snap applied groundY={groundY:0.###} (hintY was {groundHitPoint.y:0.###})");

        if (!_loggedZoneRootWorld)
        {
            _loggedZoneRootWorld = true;
            Debug.Log("[AOEControl] Zone root unparented from tower (world root)");
        }

        var visualRoot = new GameObject("HexZoneVisualRoot");
        visualRoot.transform.SetParent(go.transform, false);
        visualRoot.transform.localPosition = new Vector3(0f, VisualRootYOffset, 0f);
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;

        if (!_loggedYOffsetAdjust)
        {
            _loggedYOffsetAdjust = true;
            Debug.Log($"[AOEControl] Zone visual offset set to {VisualRootYOffset:0.###} (local)");
        }

        if (!_loggedOverlayRenderFix)
        {
            _loggedOverlayRenderFix = true;
            Debug.Log("[AOEControl] Zone overlay render fix applied");
        }

        float visualHexRadius = radius * VisualRadiusFactor;
        z.BuildHexZoneVisuals(visualRoot.transform, visualHexRadius);

        Debug.Log($"[AOEControl] Spawn Hex zone visual at {groundedWorld.x:0.##},{groundedWorld.y:0.##},{groundedWorld.z:0.##} r={radius:0.##} dur={durationSec:0.##}");
        return z;
    }

    /// <summary>
    /// Uses impact X/Z only as the column; raycasts down from high above and skips enemy colliders so the zone sits on battlefield ground.
    /// </summary>
    static bool TrySnapToBattlefieldGround(Vector3 impactHint, out Vector3 groundedWorld, out float groundY)
    {
        groundedWorld = impactHint;
        groundY = impactHint.y;

        float x = impactHint.x;
        float z = impactHint.z;
        const float rayHeight = 500f;
        const float rayLen = 600f;
        Vector3 origin = new Vector3(x, rayHeight, z);

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayLen, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            groundedWorld = new Vector3(x, 0f, z);
            groundY = groundedWorld.y;
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null)
                continue;
            if (c.GetComponentInParent<Enemy>() != null)
                continue;

            groundedWorld = hits[i].point;
            groundY = groundedWorld.y;
            return true;
        }

        groundedWorld = new Vector3(x, impactHint.y, z);
        groundY = groundedWorld.y;
        return false;
    }

    void BuildHexZoneVisuals(Transform visualRoot, float visualHexRadius)
    {
        EnsureZoneMaterials();

        var baseGo = new GameObject("HexOilBase");
        baseGo.transform.SetParent(visualRoot, false);
        baseGo.transform.localPosition = Vector3.zero;
        baseGo.transform.localRotation = Quaternion.identity;
        baseGo.transform.localScale = Vector3.one;

        _meshOil = BuildFlatHexMesh(visualHexRadius);
        var mf = baseGo.AddComponent<MeshFilter>();
        mf.sharedMesh = _meshOil;
        var mr = baseGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _matOilBase;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mr.enabled = true;

        var fireGo = new GameObject("HexFireAccent");
        fireGo.transform.SetParent(visualRoot, false);
        fireGo.transform.localPosition = new Vector3(0f, 0.002f, 0f);
        fireGo.transform.localRotation = Quaternion.identity;
        fireGo.transform.localScale = new Vector3(0.88f, 1f, 0.88f);

        _meshFire = BuildFlatHexMesh(visualHexRadius * 0.88f);
        var mf2 = fireGo.AddComponent<MeshFilter>();
        mf2.sharedMesh = _meshFire;
        var mr2 = fireGo.AddComponent<MeshRenderer>();
        mr2.sharedMaterial = _matFireAccent;
        mr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr2.receiveShadows = false;
        mr2.enabled = true;
    }

    void OnDestroy()
    {
        if (_meshOil != null)
            Destroy(_meshOil);
        if (_meshFire != null)
            Destroy(_meshFire);
    }

    static Mesh BuildFlatHexMesh(float R)
    {
        var v = new Vector3[7];
        v[0] = Vector3.zero;
        for (int i = 0; i < 6; i++)
        {
            float ang = Mathf.Deg2Rad * (30f + 60f * i);
            v[i + 1] = new Vector3(R * Mathf.Cos(ang), 0f, R * Mathf.Sin(ang));
        }

        var tris = new int[18];
        int t = 0;
        for (int i = 0; i < 6; i++)
        {
            int i1 = 1 + i;
            int i2 = 1 + ((i + 1) % 6);
            // Winding must face +Y so the quad is front-facing for a top-down camera (was 0,i1,i2 -> -Y).
            tris[t++] = 0;
            tris[t++] = i2;
            tris[t++] = i1;
        }

        var uv = new Vector2[7];
        for (int i = 0; i < 7; i++)
            uv[i] = new Vector2(v[i].x * 0.12f + 0.5f, v[i].z * 0.12f + 0.5f);

        var mesh = new Mesh { name = "AOEControl_HexFlat_Up" };
        mesh.vertices = v;
        mesh.triangles = tris;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (!_loggedHexWindingFix)
        {
            _loggedHexWindingFix = true;
            Debug.Log("[AOEControl] Hex mesh built with corrected winding");
        }

        return mesh;
    }

    static int s_zoneMaterialSetupVersionApplied;

    static void EnsureZoneMaterials()
    {
        if (_matOilBase != null && _matFireAccent != null && s_zoneMaterialSetupVersionApplied == ZoneMaterialSetupVersion)
            return;

        if (_matOilBase != null)
        {
            UnityEngine.Object.Destroy(_matOilBase);
            _matOilBase = null;
        }

        if (_matFireAccent != null)
        {
            UnityEngine.Object.Destroy(_matFireAccent);
            _matFireAccent = null;
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        bool useUnlitTransparent = false;
        if (lit == null)
        {
            lit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit != null)
                useUnlitTransparent = true;
        }

        if (lit == null)
            lit = Shader.Find("Standard");
        if (lit == null)
            lit = Shader.Find("Unlit/Color");

        _matOilBase = new Material(lit);
        _matOilBase.name = "AOEControl_ZoneBase_Runtime";
        Color baseCol = new Color(0x6b / 255f, 0x3f / 255f, 0x2a / 255f, 0.82f);
        if (_matOilBase.HasProperty("_Surface"))
            _matOilBase.SetFloat("_Surface", 1f);
        if (_matOilBase.HasProperty("_Blend"))
            _matOilBase.SetFloat("_Blend", 0f);
        if (_matOilBase.HasProperty("_BaseColor"))
            _matOilBase.SetColor("_BaseColor", baseCol);
        else
            _matOilBase.color = baseCol;
        if (_matOilBase.HasProperty("_Smoothness"))
            _matOilBase.SetFloat("_Smoothness", 0.22f);
        ApplyUrpTransparentBlends(_matOilBase);
        TryDisableCull(_matOilBase);
        TrySetDepthWriteDisabled(_matOilBase);
        TrySetZTestLequal(_matOilBase);
        _matOilBase.renderQueue = RenderQueueZoneBase;

        _matFireAccent = new Material(lit);
        _matFireAccent.name = "AOEControl_ZoneFire_Runtime";
        Color fireRgb = new Color(0xe4 / 255f, 0x8a / 255f, 0x2a / 255f, 0.72f);
        if (_matFireAccent.HasProperty("_Surface"))
            _matFireAccent.SetFloat("_Surface", 1f);
        if (_matFireAccent.HasProperty("_Blend"))
            _matFireAccent.SetFloat("_Blend", 0f);
        if (_matFireAccent.HasProperty("_BaseColor"))
            _matFireAccent.SetColor("_BaseColor", fireRgb);
        else
            _matFireAccent.color = fireRgb;
        if (_matFireAccent.HasProperty("_EmissionColor"))
        {
            _matFireAccent.EnableKeyword("_EMISSION");
            _matFireAccent.SetColor("_EmissionColor", new Color(0xe4 / 255f, 0x8a / 255f, 0x2a / 255f) * 1.1f);
        }
        if (_matFireAccent.HasProperty("_Smoothness"))
            _matFireAccent.SetFloat("_Smoothness", 0.4f);
        ApplyUrpTransparentBlends(_matFireAccent);
        TryDisableCull(_matFireAccent);
        TrySetDepthWriteDisabled(_matFireAccent);
        TrySetZTestLequal(_matFireAccent);
        _matFireAccent.renderQueue = RenderQueueZoneFire;

        if (!_loggedMaterialCullFix)
        {
            _loggedMaterialCullFix = true;
            Debug.Log("[AOEControl] Zone material cull disabled / double-sided enabled");
        }

        if (!_loggedDepthWriteDisabled)
        {
            _loggedDepthWriteDisabled = true;
            Debug.Log("[AOEControl] Zone material depth write disabled");
        }

        s_zoneMaterialSetupVersionApplied = ZoneMaterialSetupVersion;

        if (!_loggedVisibilityBoost)
        {
            _loggedVisibilityBoost = true;
            Debug.Log("[AOEControl] Zone visual visibility boost applied");
        }

        if (!_loggedRenderQueueTransparentRange)
        {
            _loggedRenderQueueTransparentRange = true;
            Debug.Log("[AOEControl] Zone render queue moved to transparent range");
        }

        if (!_loggedOverlayMaterialFix)
        {
            _loggedOverlayMaterialFix = true;
            Debug.Log("[AOEControl] Zone overlay material fix applied");
        }

        if (useUnlitTransparent && !_loggedStableTransparentUnlit)
        {
            _loggedStableTransparentUnlit = true;
            Debug.Log("[AOEControl] Switched zone material to stable transparent mode");
        }
    }

    static void ApplyUrpTransparentBlends(Material mat)
    {
        if (mat.HasProperty("_SrcBlend"))
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);
    }

    static void TrySetDepthWriteDisabled(Material mat)
    {
        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);
    }

    static void TrySetZTestLequal(Material mat)
    {
        int le = (int)UnityEngine.Rendering.CompareFunction.LessEqual;
        if (mat.HasProperty("_ZTest"))
            mat.SetInt("_ZTest", le);
    }

    static void TryDisableCull(Material mat)
    {
        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
    }

    void Update()
    {
        float end = _spawnTime + _durationSec;
        if (Time.time >= end)
        {
            ExpireZone();
            return;
        }

        ReconcileOverlaps();

        if (Time.time >= _nextTickTime)
        {
            _nextTickTime += TickInterval;
            TickDamage();
        }
    }

    void ReconcileOverlaps()
    {
        var current = new HashSet<Enemy>();
        Collider[] hits = Physics.OverlapSphere(transform.position, zoneRadius, ~0, QueryTriggerInteraction.Collide);
        if (hits != null)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                Enemy e = hits[i].GetComponentInParent<Enemy>();
                if (e == null || !e.gameObject.activeInHierarchy)
                    continue;
                if (!e.IsGroundEnemy)
                    continue;
                current.Add(e);
            }
        }

        var oldInside = new List<Enemy>(_inside);
        for (int i = 0; i < oldInside.Count; i++)
        {
            Enemy e = oldInside[i];
            if (e == null)
                continue;
            if (!current.Contains(e))
                e.AoeControlZoneRemoved(_lingerOnExitSec, _dotPerTick);
        }

        foreach (Enemy e in current)
        {
            if (!_inside.Contains(e))
            {
                int ov = e.AoeControlZoneEnter();
                if (ov > 1)
                    Debug.Log($"[AOERoute] Refresh control effect enemy={e.gameObject.name}");
            }
        }

        _inside.Clear();
        foreach (Enemy e in current)
            _inside.Add(e);
    }

    void TickDamage()
    {
        foreach (Enemy e in _inside)
        {
            if (e == null || !e.gameObject.activeInHierarchy)
                continue;
            if (!e.IsGroundEnemy)
                continue;
            if (e.ApplyAoeControlDotFromZone(_dotPerTick))
                Debug.Log($"[AOERoute] Zone tick damage enemy={e.gameObject.name} amount={_dotPerTick:0.##}");
        }
    }

    void ExpireZone()
    {
        foreach (Enemy e in _inside)
        {
            if (e != null && e.gameObject.activeInHierarchy)
                e.AoeControlZoneRemoved(_lingerOnExitSec, _dotPerTick);
        }
        _inside.Clear();
        Destroy(gameObject);
    }
}
