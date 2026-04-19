using UnityEngine;

/// <summary>
/// 单个六边形格子的运行时状态与点击反馈（仅阶段1实验地图）。
/// 使用共享的平顶六棱柱网格，与 HexGridGenerator 的 hexSize（外接圆半径）一致以便拼接。
/// 子物体上挂 <see cref="BuildSpot"/>，复用现有 TowerBuilder / BuildSelectionUI 流程。
/// </summary>
public class HexCell : MonoBehaviour
{
    #region Visual helper constants and cached resources
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    /// <summary>单位外接圆半径、半高 0.5 的平顶六棱柱（Y 为高度）。</summary>
    static Mesh _unitHexPrismMesh;

    [SerializeField] MeshRenderer targetRenderer;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] MeshCollider meshCollider;

    [Header("Visual tweak")]
    [SerializeField] float selectedLift = 0.06f;

    int _gridX;
    int _gridY;
    bool _terrainBuildable;

    [SerializeField] Transform towerSocket;
    BuildSpot _buildSpot;

    MaterialPropertyBlock _mpb;
    float _baseLocalY;

    static readonly Color BuildableBase = new Color(0.93f, 0.94f, 0.95f, 1f);
    static readonly Color NonBuildableBase = new Color(0.20f, 0.24f, 0.30f, 1f);
    static readonly Color HighlightColor = new Color(0.05f, 0.92f, 0.55f, 1f);
    static readonly Color HighlightEmission = new Color(0.15f, 0.85f, 0.45f, 1f);
    #endregion

    #region Unity lifecycle
    void Awake()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();
        if (meshCollider == null)
            meshCollider = GetComponent<MeshCollider>();
        _mpb = new MaterialPropertyBlock();
    }
    #endregion

    /// <summary>轴向 q（与生成器一致，可为负）。</summary>
    #region Stable truth and lifecycle surface
    // Stable external truth/lifecycle contract for the current build flow.
    // Keep behavior here unchanged while ownership, bridge, and visual concerns evolve around it.
    public int GridX => _gridX;
    /// <summary>轴向 r（与生成器一致，可为负）。</summary>
    public int GridY => _gridY;

    #region Ownership and creation
    // Ownership/setup path: initialize the cell shell and create its runtime BuildSpot/socket relationship.
    public void Initialize(int x, int y, bool buildable, float circumRadius, float prismHeight)
    {
        _gridX = x;
        _gridY = y;
        _terrainBuildable = buildable;

        circumRadius = Mathf.Max(1e-4f, circumRadius);
        prismHeight = Mathf.Max(1e-4f, prismHeight);

        Mesh mesh = GetOrCreateUnitHexPrismMesh();
        if (meshFilter != null)
            meshFilter.sharedMesh = mesh;
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = true;
        }

        transform.localScale = new Vector3(circumRadius, prismHeight, circumRadius);
        Vector3 p = transform.localPosition;
        _baseLocalY = p.y;
        p.y = _baseLocalY;
        transform.localPosition = p;

        EnsureNormalizedLocalRotation(transform);

        EnsureTowerSocket(circumRadius, prismHeight);
        if (towerSocket != null)
            EnsureNormalizedLocalRotation(towerSocket);

        ApplyBuildSpotLayerFromTowerBuilder();

        ApplyBaseVisual();
    }

    /// <summary>避免非归一化四元数在访问 euler/矩阵时触发 QuaternionToEuler 警告。</summary>
    static void EnsureNormalizedLocalRotation(Transform t)
    {
        if (t == null)
            return;
        Quaternion q = t.localRotation;
        float s = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (float.IsNaN(s) || float.IsInfinity(s) || s < 1e-10f)
        {
            t.localRotation = Quaternion.identity;
            return;
        }
        float inv = 1f / Mathf.Sqrt(s);
        t.localRotation = new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
    }

    /// <summary>外圈禁建等地形规则：false 时不可建造。</summary>
    #endregion

    public bool IsBuildable() => _terrainBuildable;

    /// <summary>地形允许且格上尚未建塔（与 BuildSpot 一致）。</summary>
    public bool CanPlaceTower() => _terrainBuildable && _buildSpot != null && _buildSpot.CanBuild();

    public bool HasAvailableBuildSpot() => _buildSpot != null && _buildSpot.CanBuild();

    public BuildSpot GetBuildSpot() => _buildSpot;

    /// <summary>TowerBuilder 在成功放置后调用。</summary>
    public void NotifyTowerPlaced(Tower tower)
    {
        if (HexGridManager.Instance != null)
            HexGridManager.Instance.OnHexBuilt(this);
        SetHighlightState(false);
        Debug.Log($"[HexBuild] Built tower at {_gridX},{_gridY}");
    }

    /// <summary>塔被出售或外部移除后调用：清空引用、释放 BuildSpot，格子恢复可建。</summary>
    public void NotifyTowerSold()
    {
        if (_buildSpot != null)
            _buildSpot.ClearTower();
        Debug.Log($"[HexBuild] Cell released after sell {_gridX},{_gridY}");
    }

    #endregion

    #region Ownership and creation
    void EnsureTowerSocket(float circumRadius, float prismHeight)
    {
        if (_buildSpot != null)
            return;

        var go = new GameObject("HexTowerSocket");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, prismHeight * 0.5f, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        towerSocket = go.transform;

        // 不要在本子物体上加 Collider：否则射线命中子物体，OnMouseDown 只发给子物体，
        // 父物体上的 HexCell 收不到点击。建造射线仍打在父级 MeshCollider 上，由 TowerBuilder GetComponentInChildren 找到 BuildSpot。

        _buildSpot = go.AddComponent<BuildSpot>();
    }

    void ApplyBuildSpotLayerFromTowerBuilder()
    {
        var tb = Object.FindObjectOfType<TowerBuilder>();
        if (tb == null)
            return;
        int layer = LayerMaskToFirstLayer(tb.buildSpotLayer);
        if (layer < 0 || layer > 31)
            return;
        SetLayerRecursively(gameObject, layer);
    }

    static int LayerMaskToFirstLayer(LayerMask mask)
    {
        int v = mask.value;
        for (int i = 0; i < 32; i++)
        {
            if ((v & (1 << i)) != 0)
                return i;
        }
        return -1;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        var t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    #endregion

    #region Visual helper surface
    // Visual-only helpers. These should consume cell truth and socket state, not become new rule entry points.
    public void SetHighlightState(bool highlighted)
    {
        if (highlighted)
        {
            ApplyHighlightVisual();
            return;
        }

        ClearHighlightVisual();
    }

    void ApplyHighlightVisual()
    {
        if (!_terrainBuildable || targetRenderer == null)
            return;
        PushColor(HighlightColor, useEmission: true);
        Vector3 p = transform.localPosition;
        p.y = _baseLocalY + selectedLift;
        transform.localPosition = p;
    }

    void ClearHighlightVisual()
    {
        if (!_terrainBuildable || targetRenderer == null)
            return;
        ApplyBaseVisual();
        Vector3 p = transform.localPosition;
        p.y = _baseLocalY;
        transform.localPosition = p;
    }

    void ApplyBaseVisual()
    {
        PushColor(_terrainBuildable ? BuildableBase : NonBuildableBase, useEmission: false);
    }

    void PushColor(Color c, bool useEmission)
    {
        if (targetRenderer == null)
            return;
        var mat = targetRenderer.sharedMaterial;
        targetRenderer.GetPropertyBlock(_mpb);
        if (mat != null)
        {
            if (mat.HasProperty(BaseColorId))
                _mpb.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId))
                _mpb.SetColor(ColorId, c);
            if (!mat.HasProperty(BaseColorId) && !mat.HasProperty(ColorId))
            {
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);
            }
            if (mat.HasProperty(EmissionColorId))
                _mpb.SetColor(EmissionColorId, useEmission ? HighlightEmission : Color.black);
        }
        targetRenderer.SetPropertyBlock(_mpb);
    }

    #endregion

    #region Visual mesh cache
    static Mesh GetOrCreateUnitHexPrismMesh()
    {
        if (_unitHexPrismMesh != null)
            return _unitHexPrismMesh;

        const float rad = 1f;
        const float halfH = 0.5f;

        var ring = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float ang = Mathf.Deg2Rad * (30f + 60f * i);
            ring[i] = new Vector3(rad * Mathf.Cos(ang), 0f, rad * Mathf.Sin(ang));
        }

        const int Top = 0;
        const int Bot = 6;
        const int Side = 12;
        var verts = new Vector3[36];
        var normals = new Vector3[36];

        for (int i = 0; i < 6; i++)
        {
            verts[Top + i] = ring[i] + Vector3.up * halfH;
            normals[Top + i] = Vector3.up;
            verts[Bot + i] = ring[i] - Vector3.up * halfH;
            normals[Bot + i] = Vector3.down;
        }

        for (int i = 0; i < 6; i++)
        {
            int j = (i + 1) % 6;
            Vector3 edge = ring[j] - ring[i];
            Vector3 sideN = Vector3.Cross(Vector3.up, edge).normalized;
            int b = Side + i * 4;
            verts[b + 0] = verts[Top + i];
            verts[b + 1] = verts[Top + j];
            verts[b + 2] = verts[Bot + j];
            verts[b + 3] = verts[Bot + i];
            for (int k = 0; k < 4; k++)
                normals[b + k] = sideN;
        }

        var tris = new int[72];
        int t = 0;
        for (int i = 1; i <= 4; i++)
        {
            tris[t++] = Top;
            tris[t++] = Top + i;
            tris[t++] = Top + i + 1;
        }
        for (int i = 1; i <= 4; i++)
        {
            tris[t++] = Bot;
            tris[t++] = Bot + i + 1;
            tris[t++] = Bot + i;
        }
        for (int i = 0; i < 6; i++)
        {
            int b = Side + i * 4;
            tris[t++] = b + 0;
            tris[t++] = b + 1;
            tris[t++] = b + 3;
            tris[t++] = b + 1;
            tris[t++] = b + 2;
            tris[t++] = b + 3;
        }

        _unitHexPrismMesh = new Mesh { name = "HexPrism_Unit" };
        _unitHexPrismMesh.vertices = verts;
        _unitHexPrismMesh.normals = normals;
        _unitHexPrismMesh.triangles = tris;
        _unitHexPrismMesh.RecalculateBounds();
        return _unitHexPrismMesh;
    }

    /// <summary>由 <see cref="HexGridManager"/> 射线命中后调用（不依赖 OnMouseDown）。</summary>
    #endregion

}
