using UnityEngine;

/// <summary>
/// 阶段1 实验地图：固定角度的正交战术相机（WASD / 中键平移 / 滚轮缩放），边界来自 CameraBoundsSource BoxCollider。
/// </summary>
public class CameraController : MonoBehaviour
{
    public const float FixedPitch = 55f;
    public const float FixedYaw = 45f;

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 22f;
    [SerializeField] private float moveSmoothTime = 0.06f;

    [Header("Pan (middle mouse)")]
    [SerializeField] private bool enableMiddleMousePan = true;
    [SerializeField] private float middleMousePanSensitivity = 0.1f;

    [Header("Orthographic zoom")]
    [SerializeField] private float defaultOrthographicSize = 18f;
    [SerializeField] private float minOrthographicSize = 12f;
    [SerializeField] private float maxOrthographicSize = 28f;
    [SerializeField] private float zoomSpeed = 3.5f;
    [SerializeField] private float zoomSmoothTime = 0.08f;

    [Header("Bounds")]
    [Tooltip("必须：CameraBoundsSource 上的 BoxCollider")]
    [SerializeField] private BoxCollider boundsSourceCollider;

    [SerializeField] private float edgePadding = 2f;

    [Tooltip("无边界源时的回退范围")]
    [SerializeField] private float minX = -50f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minZ = -50f;
    [SerializeField] private float maxZ = 50f;

    [Header("Initial view")]
    [SerializeField] private string baseCoreObjectName = "BaseCore";

    private Vector3 _moveVelocity;
    private float _targetOrthoSize;
    private float _orthoSizeVelocity;

    private bool _modeLoggedOnce;
    private bool _boundsLoggedOnce;

    private float _cachedUsableMinX;
    private float _cachedUsableMaxX;
    private float _cachedUsableMinZ;
    private float _cachedUsableMaxZ;

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool IsFiniteVec(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponentInChildren<Camera>();
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            targetCamera.orthographic = true;
            _targetOrthoSize = defaultOrthographicSize;
            targetCamera.orthographicSize = _targetOrthoSize;
            targetCamera.transform.localRotation = Quaternion.Euler(FixedPitch, FixedYaw, 0f);
        }

        RefreshRawBoundsFromCollider();
        ApplyInitialRigFocus();
    }

    private void Start()
    {
        if (!_modeLoggedOnce)
        {
            Debug.Log("[CameraMode] Orthographic tactical camera enabled", this);
            _modeLoggedOnce = true;
        }
    }

    private void ApplyInitialRigFocus()
    {
        Vector3 focusXZ;
        GameObject baseGo = GameObject.Find(baseCoreObjectName);
        if (baseGo != null)
        {
            Vector3 p = baseGo.transform.position;
            focusXZ = new Vector3(p.x, 0f, p.z);
        }
        else if (boundsSourceCollider != null)
        {
            Vector3 c = boundsSourceCollider.bounds.center;
            focusXZ = new Vector3(c.x, 0f, c.z);
        }
        else
        {
            return;
        }

        Vector3 pos = transform.position;
        pos.x = focusXZ.x;
        pos.z = focusXZ.z;
        pos.y = 0f;
        transform.position = pos;
    }

    private void RefreshRawBoundsFromCollider()
    {
        if (boundsSourceCollider != null)
        {
            Bounds b = boundsSourceCollider.bounds;
            minX = b.min.x;
            maxX = b.max.x;
            minZ = b.min.z;
            maxZ = b.max.z;
        }
        NormalizeBounds();
    }

    private void NormalizeBounds()
    {
        if (maxX < minX) (minX, maxX) = (maxX, minX);
        if (maxZ < minZ) (minZ, maxZ) = (maxZ, minZ);
        if (maxOrthographicSize < minOrthographicSize)
            (minOrthographicSize, maxOrthographicSize) = (maxOrthographicSize, minOrthographicSize);
    }

    private void UpdateUsableBoundsCache()
    {
        if (targetCamera == null) return;

        float ortho = targetCamera.orthographicSize;
        float halfH = ortho;
        float halfW = ortho * Mathf.Max(0.01f, targetCamera.aspect);
        float pad = edgePadding;

        float uMinX = minX + halfW + pad;
        float uMaxX = maxX - halfW - pad;
        float uMinZ = minZ + halfH + pad;
        float uMaxZ = maxZ - halfH - pad;

        if (uMaxX < uMinX)
        {
            float c = (minX + maxX) * 0.5f;
            uMinX = uMaxX = c;
        }
        if (uMaxZ < uMinZ)
        {
            float c = (minZ + maxZ) * 0.5f;
            uMinZ = uMaxZ = c;
        }

        _cachedUsableMinX = uMinX;
        _cachedUsableMaxX = uMaxX;
        _cachedUsableMinZ = uMinZ;
        _cachedUsableMaxZ = uMaxZ;

        if (!_boundsLoggedOnce && boundsSourceCollider != null)
        {
            Debug.Log(
                $"[CameraBounds] Source={boundsSourceCollider.gameObject.name} usableMinX={_cachedUsableMinX} usableMaxX={_cachedUsableMaxX} usableMinZ={_cachedUsableMinZ} usableMaxZ={_cachedUsableMaxZ} orthoSize={ortho}",
                boundsSourceCollider.gameObject);
            _boundsLoggedOnce = true;
        }
    }

    private void Update()
    {
        if (targetCamera != null)
            targetCamera.transform.localRotation = Quaternion.Euler(FixedPitch, FixedYaw, 0f);

        UpdateUsableBoundsCache();
        HandleMove();
        HandleMiddleMousePan();
        HandleZoom();
        ApplyOrthographicZoom();
    }

    private void HandleMove()
    {
        if (targetCamera == null) return;

        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        Vector3 forward = targetCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 moveDir = right * inputX + forward * inputZ;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 desired = transform.position + moveDir * moveSpeed * Time.deltaTime;
        desired.x = Mathf.Clamp(desired.x, _cachedUsableMinX, _cachedUsableMaxX);
        desired.z = Mathf.Clamp(desired.z, _cachedUsableMinZ, _cachedUsableMaxZ);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref _moveVelocity,
            Mathf.Max(0.0001f, moveSmoothTime));

        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, _cachedUsableMinX, _cachedUsableMaxX);
        p.z = Mathf.Clamp(p.z, _cachedUsableMinZ, _cachedUsableMaxZ);
        transform.position = p;
    }

    private void HandleMiddleMousePan()
    {
        if (!enableMiddleMousePan || targetCamera == null) return;
        if (!Input.GetMouseButton(2)) return;

        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");
        if (Mathf.Abs(mx) < 0.00001f && Mathf.Abs(my) < 0.00001f) return;

        Vector3 forward = targetCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 pan = (-right * mx - forward * my) * middleMousePanSensitivity;
        Vector3 p = transform.position + pan;
        p.x = Mathf.Clamp(p.x, _cachedUsableMinX, _cachedUsableMaxX);
        p.z = Mathf.Clamp(p.z, _cachedUsableMinZ, _cachedUsableMaxZ);
        transform.position = p;
    }

    private void HandleZoom()
    {
        if (targetCamera == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        _targetOrthoSize -= scroll * zoomSpeed;
        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, minOrthographicSize, maxOrthographicSize);
    }

    private void ApplyOrthographicZoom()
    {
        if (targetCamera == null) return;

        float current = targetCamera.orthographicSize;
        if (!IsFinite(current)) current = defaultOrthographicSize;
        if (!IsFinite(_targetOrthoSize)) _targetOrthoSize = defaultOrthographicSize;

        float smooth = Mathf.Max(0.0001f, zoomSmoothTime);
        float next = Mathf.SmoothDamp(current, _targetOrthoSize, ref _orthoSizeVelocity, smooth);
        if (!IsFinite(next)) next = _targetOrthoSize;
        next = Mathf.Clamp(next, minOrthographicSize, maxOrthographicSize);
        targetCamera.orthographicSize = next;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeBounds();
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
        Gizmos.DrawWireCube(center, size);
    }
}
