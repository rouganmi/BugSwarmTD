using UnityEngine;

/// <summary>
/// 运行时生成平顶六边形战场：仅在轴向「大六边形」区域内铺格，外沿一圈禁建。
/// 轴向 (q,r)，立方第三维 s = -q-r；格心世界坐标与 hexSize×spacingScale 一致。
/// </summary>
public class HexGridGenerator : MonoBehaviour
{
    [Header("Battlefield shape (axial / cube)")]
    [Tooltip("大六边形「环」半径：含中心为 0 环，最外一圈坐标满足 max(|q|,|r|,|s|)=mapRadius。")]
    [SerializeField] int mapRadius = 8;

    [Header("Cell size")]
    [Tooltip("平顶六边形外接圆半径（世界单位）。默认 √2≈1.414，相对旧默认 1.0 单格面积约 2 倍。")]
    [SerializeField] float hexSize = 1.41421356f;

    [Header("Layout tuning (flat-top)")]
    [Tooltip("与 hexSize 相乘后参与中心距；微调可消除重叠/缝隙。")]
    [SerializeField] float spacingScale = 1f;

    [Header("Cell visuals")]
    [Tooltip("六棱柱高度（世界单位），略随大地块略增厚。")]
    [SerializeField] float hexPrismHeight = 0.14f;
    [Tooltip("地块 mesh 半径相对布局半径的系数；略小于 1 可留细缝。")]
    [SerializeField] [Range(0.92f, 1f)] float cellVisualRadiusScale = 0.996f;

    [Header("Prefab")]
    [SerializeField] GameObject cellPrefab;

    [Header("Refs")]
    [SerializeField] HexGridManager gridManager;

    const string RootName = "HexGridRoot";

    void Start()
    {
        if (gridManager == null)
            gridManager = FindObjectOfType<HexGridManager>();

        GenerateGrid();
    }

    void GenerateGrid()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("[HexGrid] cellPrefab is not assigned.");
            return;
        }

        Transform existing = transform.Find(RootName);
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(transform, false);

        if (mapRadius < 0)
            mapRadius = 0;

        float layoutR = hexSize * spacingScale;
        float meshR = layoutR * cellVisualRadiusScale;

        int count = 0;
        for (int q = -mapRadius; q <= mapRadius; q++)
        {
            for (int r = -mapRadius; r <= mapRadius; r++)
            {
                int ring = CubeRing(q, r);
                if (ring > mapRadius)
                    continue;

                bool buildable = ring < mapRadius;

                Vector3 worldPos = AxialToWorldFlatTop(q, r);
                GameObject cellGo = Instantiate(cellPrefab, worldPos, Quaternion.identity, root.transform);
                cellGo.name = $"HexCell_{q}_{r}";

                HexCell cell = cellGo.GetComponent<HexCell>();
                if (cell == null)
                    cell = cellGo.AddComponent<HexCell>();

                cell.Initialize(q, r, buildable, meshR, hexPrismHeight);
                count++;
            }
        }

        Debug.Log($"[HexGrid] Generated {count} cells");
    }

    /// <summary>立方坐标下 max(|q|,|r|,|s|)，s = -q - r。</summary>
    static int CubeRing(int q, int r)
    {
        int s = -q - r;
        return Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(s)));
    }

    /// <summary>
    /// 平顶六边形轴向 (q,r) → 世界 XZ；(0,0) 落在本物体局部原点，整场关于战场中心对称。
    /// </summary>
    Vector3 AxialToWorldFlatTop(int q, int r)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        float R = hexSize * spacingScale;
        float x = R * (sqrt3 * q + sqrt3 * 0.5f * r);
        float z = R * (1.5f * r);
        return new Vector3(x, 0f, z) + transform.position;
    }
}
