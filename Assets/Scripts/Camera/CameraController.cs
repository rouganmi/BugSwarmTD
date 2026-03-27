using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 35f;
    [SerializeField] private float moveSmoothTime = 0.04f;

    [Header("Pan (middle mouse)")]
    [SerializeField] private bool enableMiddleMousePan = true;
    [SerializeField] private float middleMousePanSensitivity = 0.12f;

    [Header("View (Tower Defense Top-Down)")]
    [SerializeField] private bool enforceTopDownView = true;
    [SerializeField, Range(35f, 80f)] private float pitch = 60f;
    [SerializeField, Range(0f, 360f)] private float yaw = 45f;

    [Header("Rotation")]
    [SerializeField] private bool enableKeyboardRotation = true;
    [SerializeField] private bool enableMouseRotation = true;
    [SerializeField] private float rotationSpeed = 120f; // degrees/sec
    [SerializeField] private float mouseRotationSpeed = 0.25f; // degrees per pixel
    [SerializeField] private int mouseRotateButton = 1; // right mouse

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 25f;
    [SerializeField] private float minCameraY = 12f;
    [SerializeField] private float maxCameraY = 35f;
    [SerializeField] private float zoomSmoothTime = 0.04f;
    [SerializeField] private float minDistance = 10f;
    [SerializeField] private float maxDistance = 45f;
    [SerializeField] private bool zoomToMousePosition = true;
    [SerializeField] private float zoomToMouseStrength = 11f;
    [SerializeField] private float zoomToMouseOutFactor = 0.7f;
    [SerializeField] private float zoomFocusSmoothTime = 0.06f;
    [SerializeField] private float groundPlaneY = 0f;

    [Header("Bounds")]
    [SerializeField] private float minX = -50f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minZ = -50f;
    [SerializeField] private float maxZ = 50f;

    private Vector3 _moveVelocity;
    private float _zoomVelocityY;
    private float _targetCameraLocalY;
    private float _zoomVelocityDistance;
    private float _targetDistance;
    private bool _isMouseRotating;
    private Vector3 _zoomFocusVelocity;
    private bool _warnedInvalidCameraWrite;

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            var lp = targetCamera.transform.localPosition;
            _targetCameraLocalY = lp.y;
            _targetDistance = Mathf.Abs(lp.z) > 0.001f ? Mathf.Abs(lp.z) : 25f;
        }

        NormalizeBounds();
    }

    private void Update()
    {
        HandleRotation();
        HandleMove();
        HandleMiddleMousePan();
        HandleZoom();
        ApplyViewAndZoom();
    }

    private void HandleRotation()
    {
        float yawDelta = 0f;

        if (enableKeyboardRotation)
        {
            if (Input.GetKey(KeyCode.Q)) yawDelta -= rotationSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) yawDelta += rotationSpeed * Time.deltaTime;
        }

        if (enableMouseRotation)
        {
            if (Input.GetMouseButtonDown(mouseRotateButton)) _isMouseRotating = true;
            if (Input.GetMouseButtonUp(mouseRotateButton)) _isMouseRotating = false;

            if (_isMouseRotating)
            {
                float mx = Input.GetAxis("Mouse X");
                yawDelta += mx * mouseRotationSpeed * 100f; // convert axis to a usable deg step
            }
        }

        if (Mathf.Abs(yawDelta) > 0.0001f)
        {
            yaw = Mathf.Repeat(yaw + yawDelta, 360f);
        }
    }

    private void HandleMove()
    {
        float inputX = 0f;
        float inputZ = 0f;

        inputX += Input.GetAxisRaw("Horizontal");
        inputZ += Input.GetAxisRaw("Vertical");

        Vector3 moveDir;
        if (enforceTopDownView)
        {
            // Always move on XZ plane using the camera's view direction (flattened),
            // so W/S remain "screen forward/back" even after rotating the rig.
            Vector3 forward = targetCamera != null ? targetCamera.transform.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            moveDir = right * inputX + forward * inputZ;
        }
        else
        {
            moveDir = new Vector3(inputX, 0f, inputZ);
        }

        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }

        Vector3 desired = transform.position + moveDir * moveSpeed * Time.deltaTime;
        desired.x = Mathf.Clamp(desired.x, minX, maxX);
        desired.z = Mathf.Clamp(desired.z, minZ, maxZ);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref _moveVelocity,
            Mathf.Max(0.0001f, moveSmoothTime)
        );
    }

    /// <summary>
    /// RTS-style pan on the ground plane: drag moves the rig so the map follows the cursor
    /// (camera shifts opposite to mouse delta on the flattened view axes).
    /// </summary>
    private void HandleMiddleMousePan()
    {
        if (!enableMiddleMousePan || targetCamera == null) return;
        if (!Input.GetMouseButton(2)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

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

        // Drag right → world appears to move right → rig moves -right (grab-the-map).
        // Drag up → world moves “up” on screen → rig moves -forward on XZ.
        Vector3 pan = (-right * mx - forward * my) * middleMousePanSensitivity;

        Vector3 p = transform.position;
        p += pan;
        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.z = Mathf.Clamp(p.z, minZ, maxZ);
        transform.position = p;
    }

    private void HandleZoom()
    {
        if (targetCamera == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        // Scroll forward -> zoom in, scroll backward -> zoom out.
        float delta = Mathf.Abs(scroll) * zoomSpeed;
        if (scroll > 0f) _targetCameraLocalY -= delta; // zoom in
        else _targetCameraLocalY += delta;            // zoom out
        _targetCameraLocalY = Mathf.Clamp(_targetCameraLocalY, minCameraY, maxCameraY);

        // Keep distance roughly in sync with height to avoid awkward perspective
        float t = Mathf.InverseLerp(minCameraY, maxCameraY, _targetCameraLocalY);
        _targetDistance = Mathf.Lerp(minDistance, maxDistance, t);

        // Only use mouse-position-based focus shift for zoom.
        if (zoomToMousePosition && TryGetMouseGroundPoint(out Vector3 mouseGround))
        {
            Vector3 toMouse = mouseGround - transform.position;
            toMouse.y = 0f;
            if (toMouse.sqrMagnitude > 0.0001f)
            {
                float zoomRange = Mathf.Max(0.001f, maxCameraY - minCameraY);
                float zoomRatio = delta / zoomRange;
                float dirFactor = scroll > 0f ? 1f : -Mathf.Max(0f, zoomToMouseOutFactor);
                float moveScale = zoomToMouseStrength * zoomRatio * dirFactor;

                Vector3 desiredRig = transform.position + toMouse * moveScale;
                desiredRig.x = Mathf.Clamp(desiredRig.x, minX, maxX);
                desiredRig.z = Mathf.Clamp(desiredRig.z, minZ, maxZ);

                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desiredRig,
                    ref _zoomFocusVelocity,
                    Mathf.Max(0.0001f, zoomFocusSmoothTime)
                );
            }
        }
        // If there is no valid ground hit, keep only height/distance zoom (safe fallback),
        // without any forward/back rig push behavior.
    }

    private bool TryGetMouseGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (targetCamera == null) return false;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
        if (!groundPlane.Raycast(ray, out float enter)) return false;

        point = ray.GetPoint(enter);
        return true;
    }

    private void ApplyViewAndZoom()
    {
        if (targetCamera == null) return;

        if (enforceTopDownView)
        {
            targetCamera.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        Vector3 localPos = targetCamera.transform.localPosition;

        // Safety: prevent NaN/Infinity from corrupting camera transform.
        if (!IsFinite(localPos) ||
            !IsFinite(_targetCameraLocalY) || !IsFinite(_targetDistance) ||
            !IsFinite(minCameraY) || !IsFinite(maxCameraY) || !IsFinite(minDistance) || !IsFinite(maxDistance))
        {
            if (!_warnedInvalidCameraWrite)
            {
                _warnedInvalidCameraWrite = true;
                Debug.LogWarning($"[Camera] Invalid camera zoom state detected. localPos={localPos}, targetY={_targetCameraLocalY}, targetDist={_targetDistance}");
            }
            return;
        }

        float desiredY = Mathf.Clamp(_targetCameraLocalY, minCameraY, maxCameraY);
        if (!IsFinite(desiredY))
        {
            if (!_warnedInvalidCameraWrite)
            {
                _warnedInvalidCameraWrite = true;
                Debug.LogWarning("[Camera] Invalid desiredY (NaN/Infinity). Skipping update this frame.");
            }
            return;
        }

        localPos.y = Mathf.SmoothDamp(localPos.y, desiredY, ref _zoomVelocityY, Mathf.Max(0.0001f, zoomSmoothTime));

        float desiredDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        float currentDistance = Mathf.Abs(localPos.z);
        if (!IsFinite(desiredDistance) || !IsFinite(currentDistance) || !IsFinite(_zoomVelocityDistance))
        {
            _zoomVelocityDistance = 0f;
            float safeDistance = IsFinite(desiredDistance) ? desiredDistance : (minDistance + maxDistance) * 0.5f;
            localPos.z = -safeDistance;

            if (!_warnedInvalidCameraWrite)
            {
                _warnedInvalidCameraWrite = true;
                Debug.LogWarning("[Camera] Invalid zoom smoothing state. Resetting distance this frame.");
            }

            targetCamera.transform.localPosition = localPos;
            return;
        }

        float smoothedDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref _zoomVelocityDistance, Mathf.Max(0.0001f, zoomSmoothTime));
        if (!IsFinite(smoothedDistance))
        {
            _zoomVelocityDistance = 0f;
            smoothedDistance = desiredDistance;
        }

        localPos.z = -smoothedDistance;
        targetCamera.transform.localPosition = localPos;
    }

    private void NormalizeBounds()
    {
        if (maxX < minX) (minX, maxX) = (maxX, minX);
        if (maxZ < minZ) (minZ, maxZ) = (maxZ, minZ);
        if (maxCameraY < minCameraY) (minCameraY, maxCameraY) = (maxCameraY, minCameraY);
        if (maxDistance < minDistance) (minDistance, maxDistance) = (maxDistance, minDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center = new Vector3((minX + maxX) * 0.5f, transform.position.y, (minZ + maxZ) * 0.5f);
        Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
        Gizmos.DrawWireCube(center, size);
    }
}
