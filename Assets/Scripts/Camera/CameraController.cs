using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 阶段1 实验地图：固定角度的正交战术相机（WASD / 中键平移 / 滚轮缩放），边界来自 CameraBoundsSource BoxCollider。
/// </summary>
[DefaultExecutionOrder(100)]
public class CameraController : MonoBehaviour
{
    public const float FixedPitch = 60f;
    public const float FixedYaw = 45f;

    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 24f;
    [SerializeField] private float moveSmoothTime = 0.052f;

    [Header("Pan (middle mouse)")]
    [SerializeField] private bool enableMiddleMousePan = true;
    [SerializeField] private float middleMousePanSensitivity = 0.14f;
    [Tooltip("Dynamic acceleration based on mouse delta magnitude (small drags stay precise, large drags speed up).")]
    [SerializeField] private float middleMousePanAccelK = 3.0f;
    [SerializeField] private float middleMousePanAccelMaxBoost = 2.5f;
    [Tooltip("SmoothDamp time for middle-mouse pan (position only); clears jitter at drag start/stop.")]
    [SerializeField] private float middleMousePanSmoothTime = 0.055f;

    [Header("Orthographic zoom")]
    [SerializeField] private float defaultOrthographicSize = 13.5f;
    [SerializeField] private float minOrthographicSize = 11.5f;
    [SerializeField] private float maxOrthographicSize = 24f;
    [SerializeField] private float zoomSpeed = 2.75f;
    [SerializeField] private float zoomSmoothTime = 0.125f;

    [Header("Bounds")]
    [Tooltip("必须：CameraBoundsSource 上的 BoxCollider")]
    [SerializeField] private BoxCollider boundsSourceCollider;

    [SerializeField] private float edgePadding = 2.5f;
    [Tooltip("Scales edgePadding with zoom: zoomed out => smaller padding (slightly looser pan); zoomed in => larger padding.")]
    [SerializeField] [Range(0f, 0.6f)] private float edgePaddingZoomInfluence = 0.18f;

    [Tooltip("无边界源时的回退范围")]
    [SerializeField] private float minX = -50f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minZ = -50f;
    [SerializeField] private float maxZ = 50f;

    [Header("Initial view")]
    [Tooltip("优先：轴向战场原点 / 六边形中心（与 HexGridGenerator 的 (0,0) 格一致）。")]
    [SerializeField] private string battlefieldFocusObjectName = "HexGrid";

    [Tooltip("在 Start 中在 Hex 生成后，用 HexGridRoot 下所有 Renderer 的合并 bounds 中心细化 battlefieldCenter（否则仅用 HexGrid 世界坐标）。")]
    [SerializeField] private bool refineBattlefieldCenterFromHexRenderers = true;

    [SerializeField] private string baseCoreObjectName = "BaseCore";

    [Tooltip("相对 battlefieldCenter 的平移（世界 X / 世界 Z），不随 BaseCore 绝对坐标漂移。")]
    [SerializeField] private Vector2 initialFocusOffsetXZ = new Vector2(0f, -6f);

    [Header("Gameplay framing (BaseCore bias)")]
    [Tooltip("0 = only battlefield + initialFocusOffsetXZ; 1 = full blend toward BaseCore + framing offset. Keeps map visible.")]
    [SerializeField] [Range(0f, 1f)] private float baseCoreFramingBlend = 0.22f;

    [Tooltip("Added to BaseCore XZ when blending; nudges base slightly below screen center for TD readability.")]
    [SerializeField] private Vector2 baseCoreFramingOffsetXZ = new Vector2(0f, -0.45f);

    [Header("Quick focus (Space → BaseCore)")]
    [SerializeField] private bool enableQuickFocusBaseCore = true;
    [SerializeField] private KeyCode quickFocusKey = KeyCode.Space;
    [SerializeField] private float quickFocusSmoothTime = 0.38f;
    [Tooltip("Rig target = BaseCore XZ + this (same space as initialFocusOffsetXZ).")]
    [SerializeField] private Vector2 quickFocusOffsetXZ = new Vector2(0f, -0.65f);

    [Header("Zoom-to-mouse (safe)")]
    [SerializeField] private bool enableZoomTowardMouse = true;
    [Tooltip("How strongly zoom nudges rig toward mouse ray hit (subtle). scroll>0 (zoom in) moves toward mouse; scroll<0 moves away.")]
    [SerializeField] [Range(0f, 0.35f)] private float zoomTowardMouseStrength = 0.14f;
    [SerializeField] private float zoomTowardMouseSmoothTime = 0.08f;

    [Header("Camera stickiness (idle auto-center)")]
    [Tooltip("When idle, gently drifts toward battlefield center + initialFocusOffsetXZ. 0 disables.")]
    [SerializeField] [Range(0f, 0.25f)] private float idleCenteringStrength = 0.05f;
    [SerializeField] private float idleCenteringSmoothTime = 3.0f;

    [Header("UI / interaction priority")]
    [Tooltip("If pointer is over UI, block camera input (WASD/MMB/zoom) so UI has priority.")]
    [SerializeField] private bool blockCameraInputWhenPointerOverUI = true;
    [Tooltip("If TowerMenu (panel or radial) is open, block camera input (WASD/MMB/zoom).")]
    [SerializeField] private bool blockCameraInputWhenTowerMenuOpen = true;
    [Tooltip("If SelectionInfoPanel is showing, block camera input (prevents accidental drift while reading).")]
    [SerializeField] private bool blockCameraInputWhenSelectionPanelShowing = false;

    [Header("Selection focus (soft bias)")]
    [Tooltip("When a tower is selected (via TowerMenu), gently bias rig toward it (position only). 0 disables.")]
    [SerializeField] [Range(0f, 1f)] private float selectedTowerBiasBlend = 0.10f;
    [Tooltip("Offset from selected tower XZ when biasing (world X / world Z).")]
    [SerializeField] private Vector2 selectedTowerBiasOffsetXZ = new Vector2(0.15f, -0.35f);
    [SerializeField] private float selectedTowerBiasSmoothTime = 0.28f;

    private Vector3 _moveVelocity;
    private float _targetOrthoSize;
    private float _orthoSizeVelocity;

    private Vector3 _panSmoothVelocity;
    private bool _wasMiddleMousePanLastFrame;

    private bool _quickFocusActive;
    private Vector3 _quickFocusVelocity;
    private Vector3 _quickFocusTarget;

    private Vector3 _selectedTowerBiasVelocity;

    private Vector3 _zoomMouseNudgePending;
    private Vector3 _zoomMouseNudgeVelocity;

    private Vector3 _idleCenterVelocity;

    private bool _modeLoggedOnce;
    private bool _boundsLoggedOnce;

    private float _cachedUsableMinX;
    private float _cachedUsableMaxX;
    private float _cachedUsableMinZ;
    private float _cachedUsableMaxZ;

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool IsFiniteVec(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

    /// <summary>Max |magSq−1| allowed before forcing normalize (catches serialized drift that triggers QuaternionToEuler in editor).</summary>
    const float CameraLocalRotationUnitMagSqSlop = 1e-4f;

    private void EnsureValidRotation(Transform t)
    {
        Quaternion q = t.localRotation;

        float mag = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;

        if (float.IsNaN(mag) || float.IsInfinity(mag) || Mathf.Abs(mag - 1f) > 0.0001f)
        {
            t.localRotation = Quaternion.Euler(t.localEulerAngles);
        }
    }

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
            EnsureValidRotation(targetCamera.transform);
            SanitizeTargetCameraLocalRotation();
        }

        RefreshRawBoundsFromCollider();
        ApplyInitialRigFocus(tryHexRendererBounds: false);
    }

    private void Start()
    {
        if (refineBattlefieldCenterFromHexRenderers)
            ApplyInitialRigFocus(tryHexRendererBounds: true);

        if (!_modeLoggedOnce)
        {
            Debug.Log("[CameraMode] Orthographic tactical camera enabled", this);
            _modeLoggedOnce = true;
        }
    }

    /// <summary>
    /// battlefieldCenter：HexGrid 世界 XZ（轴向原点），可选为 HexGridRoot 下 Renderer 合并 bounds 中心。
    /// 机位：rig.x = center.x + offset.x，rig.z = center.z + offset.y（相对战场中心，非绝对世界偏移）。
    /// </summary>
    private void ApplyInitialRigFocus(bool tryHexRendererBounds)
    {
        if (!TryResolveBattlefieldCenterXZ(tryHexRendererBounds, out Vector3 battlefieldCenter))
            return;

        Vector3 battlePos = new Vector3(
            battlefieldCenter.x + initialFocusOffsetXZ.x,
            0f,
            battlefieldCenter.z + initialFocusOffsetXZ.y);

        if (baseCoreFramingBlend > 0.0001f &&
            TryGetBaseCoreXZ(out Vector3 baseXZ))
        {
            Vector3 baseTarget = new Vector3(
                baseXZ.x + baseCoreFramingOffsetXZ.x,
                0f,
                baseXZ.z + baseCoreFramingOffsetXZ.y);
            float t = Mathf.Clamp01(baseCoreFramingBlend);
            battlePos.x = Mathf.Lerp(battlePos.x, baseTarget.x, t);
            battlePos.z = Mathf.Lerp(battlePos.z, baseTarget.z, t);
        }

        Vector3 pos = transform.position;
        float keepY = pos.y;
        pos.x = battlePos.x;
        pos.z = battlePos.z;
        pos.y = keepY;
        transform.position = pos;
    }

    bool TryGetBaseCoreXZ(out Vector3 xz)
    {
        xz = default;
        if (string.IsNullOrEmpty(baseCoreObjectName))
            return false;
        GameObject go = GameObject.Find(baseCoreObjectName);
        if (go == null)
            return false;
        Vector3 p = go.transform.position;
        xz = new Vector3(p.x, 0f, p.z);
        return true;
    }

    private bool TryResolveBattlefieldCenterXZ(bool tryHexRendererBounds, out Vector3 battlefieldCenter)
    {
        battlefieldCenter = default;

        if (!string.IsNullOrEmpty(battlefieldFocusObjectName))
        {
            GameObject hexGo = GameObject.Find(battlefieldFocusObjectName);
            if (hexGo != null)
            {
                if (tryHexRendererBounds &&
                    TryGetHexGridRootRendererBoundsCenterXZ(hexGo.transform, out Vector3 fromRenderers))
                {
                    battlefieldCenter = fromRenderers;
                    return true;
                }

                Vector3 p = hexGo.transform.position;
                battlefieldCenter = new Vector3(p.x, 0f, p.z);
                return true;
            }
        }

        if (!string.IsNullOrEmpty(baseCoreObjectName))
        {
            GameObject baseGo = GameObject.Find(baseCoreObjectName);
            if (baseGo != null)
            {
                Vector3 p = baseGo.transform.position;
                battlefieldCenter = new Vector3(p.x, 0f, p.z);
                return true;
            }
        }

        if (boundsSourceCollider != null)
        {
            Vector3 c = boundsSourceCollider.bounds.center;
            battlefieldCenter = new Vector3(c.x, 0f, c.z);
            return true;
        }

        return false;
    }

    static bool TryGetHexGridRootRendererBoundsCenterXZ(Transform hexGridTransform, out Vector3 xz)
    {
        xz = default;
        Transform root = hexGridTransform.Find("HexGridRoot");
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        xz = new Vector3(b.center.x, 0f, b.center.z);
        return true;
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
        float orthoT = Mathf.InverseLerp(minOrthographicSize, maxOrthographicSize, ortho);
        float padScale = Mathf.Lerp(1f + edgePaddingZoomInfluence, 1f - edgePaddingZoomInfluence, orthoT);
        float pad = edgePadding * padScale;

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
            SanitizeTargetCameraLocalRotation();

        UpdateUsableBoundsCache();

        bool uiBlocks = IsUIBlockingCameraInput();
        if (uiBlocks)
        {
            // UI has priority: stop any in-progress camera-driven movement so it doesn't feel like "fighting" UI.
            _quickFocusActive = false;
            _quickFocusVelocity = Vector3.zero;
            _moveVelocity = Vector3.zero;
            _panSmoothVelocity = Vector3.zero;
            _zoomMouseNudgePending = Vector3.zero;
            _zoomMouseNudgeVelocity = Vector3.zero;
            _idleCenterVelocity = Vector3.zero;
        }

        if (!uiBlocks)
        {
            HandleQuickFocusInput();
            bool skipMoveAndPan = _quickFocusActive && HandleQuickFocusMove();

            if (!skipMoveAndPan)
            {
                HandleMove();
                HandleMiddleMousePan();
            }

            HandleZoom();
            ApplyZoomTowardMouseNudge();
            ApplyIdleCentering();
        }

        ApplySelectedTowerBias(uiBlocks);
        ApplyOrthographicZoom();
        ClampRigToCachedBounds();
    }

    bool IsUIBlockingCameraInput()
    {
        if (blockCameraInputWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return true;

        if (blockCameraInputWhenTowerMenuOpen &&
            TowerMenu.Instance != null &&
            TowerMenu.Instance.IsOpen)
            return true;

        if (blockCameraInputWhenSelectionPanelShowing &&
            SelectionInfoPanel.Instance != null &&
            SelectionInfoPanel.Instance.IsShowing)
            return true;

        return false;
    }

    bool IsPlayerProvidingMoveInput()
    {
        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f) return true;
        if (Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f) return true;
        if (enableMiddleMousePan && Input.GetMouseButton(2)) return true;
        return false;
    }

    void ApplySelectedTowerBias(bool uiBlocks)
    {
        if (selectedTowerBiasBlend <= 0.0001f)
            return;

        // Keep UI stable: don't auto-bias while pointer is interacting with UI.
        if (uiBlocks && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TowerMenu menu = TowerMenu.Instance;
        if (menu == null || menu.SelectedTower == null)
            return;

        // Preserve player control: only bias when the player isn't actively moving the camera.
        if (IsPlayerProvidingMoveInput() || _quickFocusActive)
            return;

        Vector3 towerPos = menu.SelectedTower.transform.position;
        Vector3 desired = transform.position;
        desired.x = towerPos.x + selectedTowerBiasOffsetXZ.x;
        desired.z = towerPos.z + selectedTowerBiasOffsetXZ.y;
        desired.y = transform.position.y;

        float smooth = Mathf.Max(0.0001f, selectedTowerBiasSmoothTime);
        Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref _selectedTowerBiasVelocity, smooth);
        next.y = transform.position.y;

        float t = Mathf.Clamp01(selectedTowerBiasBlend);
        transform.position = Vector3.Lerp(transform.position, next, t);
    }

    void HandleQuickFocusInput()
    {
        if (!enableQuickFocusBaseCore)
            return;
        if (!Input.GetKeyDown(quickFocusKey))
            return;
        if (!TryGetBaseCoreXZ(out Vector3 baseXZ))
            return;

        float y = transform.position.y;
        _quickFocusTarget = new Vector3(
            baseXZ.x + quickFocusOffsetXZ.x,
            y,
            baseXZ.z + quickFocusOffsetXZ.y);
        _quickFocusActive = true;
        _quickFocusVelocity = Vector3.zero;
    }

    /// <summary>Returns true if quick focus consumed the frame (skip WASD / MMB).</summary>
    bool HandleQuickFocusMove()
    {
        if (!_quickFocusActive)
            return false;

        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
            Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f ||
            (enableMiddleMousePan && Input.GetMouseButton(2)))
        {
            _quickFocusActive = false;
            _quickFocusVelocity = Vector3.zero;
            _moveVelocity = Vector3.zero;
            return false;
        }

        float smooth = Mathf.Max(0.0001f, quickFocusSmoothTime);
        Vector3 next = Vector3.SmoothDamp(transform.position, _quickFocusTarget, ref _quickFocusVelocity, smooth);
        next.y = transform.position.y;
        transform.position = next;

        Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 b = new Vector3(_quickFocusTarget.x, 0f, _quickFocusTarget.z);
        if (Vector3.Distance(a, b) < 0.12f)
        {
            _quickFocusActive = false;
            _quickFocusVelocity = Vector3.zero;
        }

        return true;
    }

    /// <summary>Keeps rig inside usable rect after ortho/zoom changes (same limits as WASD / pan).</summary>
    void ClampRigToCachedBounds()
    {
        if (targetCamera == null)
            return;
        Vector3 p = transform.position;
        p.x = Mathf.Clamp(p.x, _cachedUsableMinX, _cachedUsableMaxX);
        p.z = Mathf.Clamp(p.z, _cachedUsableMinZ, _cachedUsableMaxZ);
        transform.position = p;
    }

    /// <summary>Ensures target camera localRotation is finite and approximately unit; avoids non-unit quaternions in Inspector/Game view.</summary>
    void SanitizeTargetCameraLocalRotation()
    {
        if (targetCamera == null)
            return;

        Quaternion rot = targetCamera.transform.localRotation;
        if (RotationDebug.IsFinite(rot) &&
            RotationDebug.IsApproximatelyUnit(rot, CameraLocalRotationUnitMagSqSlop))
            return;

        rot = RotationDebug.NormalizeOrIdentity(rot);
        if (!RotationDebug.IsFinite(rot) || !RotationDebug.IsApproximatelyUnit(rot, CameraLocalRotationUnitMagSqSlop))
            rot = Quaternion.Euler(FixedPitch, FixedYaw, 0f).normalized;

        targetCamera.transform.localRotation = rot;
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

        bool mmb = Input.GetMouseButton(2);
        if (!mmb && _wasMiddleMousePanLastFrame)
            _panSmoothVelocity = Vector3.zero;
        _wasMiddleMousePanLastFrame = mmb;

        if (!mmb) return;

        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");
        if (Mathf.Abs(mx) < 0.00001f && Mathf.Abs(my) < 0.00001f) return;

        float deltaMag = Mathf.Sqrt(mx * mx + my * my);
        float boost = Mathf.Clamp(deltaMag * middleMousePanAccelK, 0f, middleMousePanAccelMaxBoost);
        float speedFactor = 1f + boost;

        Vector3 forward = targetCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();

        Vector3 pan = (-right * mx - forward * my) * (middleMousePanSensitivity * speedFactor);
        Vector3 targetPos = transform.position + pan;
        targetPos.x = Mathf.Clamp(targetPos.x, _cachedUsableMinX, _cachedUsableMaxX);
        targetPos.z = Mathf.Clamp(targetPos.z, _cachedUsableMinZ, _cachedUsableMaxZ);

        float panSmooth = Mathf.Max(0.0001f, middleMousePanSmoothTime);
        Vector3 smoothed = Vector3.SmoothDamp(transform.position, targetPos, ref _panSmoothVelocity, panSmooth);
        smoothed.y = transform.position.y;
        transform.position = smoothed;
    }

    private void HandleZoom()
    {
        if (targetCamera == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        _targetOrthoSize -= scroll * zoomSpeed;
        _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, minOrthographicSize, maxOrthographicSize);

        if (enableZoomTowardMouse && zoomTowardMouseStrength > 0.0001f)
            AccumulateZoomTowardMouseNudge(scroll);
    }

    void AccumulateZoomTowardMouseNudge(float scroll)
    {
        // Raycast mouse + screen center onto ground plane (y=0). Keep subtle and stable.
        Ray mouseRay = targetCamera.ScreenPointToRay(Input.mousePosition);
        Ray centerRay = targetCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        Plane ground = new Plane(Vector3.up, Vector3.zero);

        if (!ground.Raycast(mouseRay, out float mouseEnter) || !ground.Raycast(centerRay, out float centerEnter))
            return;

        Vector3 mouseWorld = mouseRay.GetPoint(mouseEnter);
        Vector3 centerWorld = centerRay.GetPoint(centerEnter);
        Vector3 delta = mouseWorld - centerWorld;
        delta.y = 0f;

        // scroll>0 => zoom in => move toward mouse; scroll<0 => zoom out => move away (subtle).
        float amount = scroll * zoomTowardMouseStrength;
        Vector3 nudge = delta * amount;

        float maxNudgePerTick = 2.2f;
        if (nudge.sqrMagnitude > maxNudgePerTick * maxNudgePerTick)
            nudge = nudge.normalized * maxNudgePerTick;

        _zoomMouseNudgePending += nudge;
    }

    void ApplyZoomTowardMouseNudge()
    {
        if (!enableZoomTowardMouse)
            return;
        if (_zoomMouseNudgePending.sqrMagnitude < 0.000001f)
            return;

        Vector3 target = transform.position + _zoomMouseNudgePending;
        target.y = transform.position.y;
        float smooth = Mathf.Max(0.0001f, zoomTowardMouseSmoothTime);
        Vector3 next = Vector3.SmoothDamp(transform.position, target, ref _zoomMouseNudgeVelocity, smooth);
        next.y = transform.position.y;
        transform.position = next;

        _zoomMouseNudgePending = Vector3.Lerp(_zoomMouseNudgePending, Vector3.zero, 0.35f);
        if (_zoomMouseNudgePending.sqrMagnitude < 0.00005f)
            _zoomMouseNudgePending = Vector3.zero;
    }

    void ApplyIdleCentering()
    {
        if (idleCenteringStrength <= 0.0001f)
            return;
        if (IsPlayerProvidingMoveInput() || _quickFocusActive)
            return;
        if (!TryResolveBattlefieldCenterXZ(tryHexRendererBounds: false, out Vector3 battlefieldCenter))
            return;

        Vector3 desired = transform.position;
        desired.x = battlefieldCenter.x + initialFocusOffsetXZ.x;
        desired.z = battlefieldCenter.z + initialFocusOffsetXZ.y;
        desired.y = transform.position.y;

        float smooth = Mathf.Max(0.0001f, idleCenteringSmoothTime);
        Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref _idleCenterVelocity, smooth);
        next.y = transform.position.y;

        float t = Mathf.Clamp01(idleCenteringStrength * Time.deltaTime * 60f);
        transform.position = Vector3.Lerp(transform.position, next, t);
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
        if (targetCamera != null)
            EnsureValidRotation(targetCamera.transform);
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
