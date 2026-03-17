// Optimized CarPartMover with Clean Level 5 Rotation System
// Maze code removed, rotation settings exposed for easy configuration
using UnityEngine;

public class CarPartMover : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private GameObject handSphere;
    [SerializeField] private float sphereSize = 0.15f;
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color pinchColor = Color.yellow;

    [Header("Camera")]
    [SerializeField] private Camera followCamera;
    [SerializeField] private float sphereDepth = 5f;

    [Header("Smoothing & Stabilization")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float positionSmoothTime = 0.1f;
    [SerializeField] private float maxSpeedPerFrame = 2.0f;
    [SerializeField] private int stabilizationFrames = 5;

    [Header("Three-Finger Pinch Detection")]
    [SerializeField] private float pinchThreshold = 0.045f;
    [SerializeField] private float pinchReleaseThreshold = 0.06f;
    [SerializeField] private int pinchConfirmFrames = 3;

    [Header("Car Part Interaction")]
    [SerializeField] private float pickupRange = 0.5f;
    [Range(-10f, 20f)]
    [SerializeField] private float dragZPosition = 8f;
    [SerializeField] private bool freezePartsOnPickup = false;
    [SerializeField] private bool resetOnPartTouch = false;
    [SerializeField] private bool instantSnapBack = false;
    [SerializeField] private float touchDetectionDistance = 0.6f;

    [Header("========== LEVEL 5: HAND ROTATION ==========")]
    [Tooltip("Enable hand rotation control (Level 5 feature)")]
    [SerializeField] private bool enableHandRotation = false;
    
    [Header("Rotation Settings")]
    [Range(0.1f, 3.0f)]
    [Tooltip("How fast part rotates vs hand:\n• 1.0 = matches hand exactly\n• 2.0 = part rotates 2x faster\n• 0.5 = part rotates half speed")]
    [SerializeField] private float rotationMultiplier = 1.0f;
    
    [Range(0f, 0.3f)]
    [Tooltip("Smoothing delay:\n• 0 = instant but jittery\n• 0.05 = fast & smooth\n• 0.2 = slow & floaty")]
    [SerializeField] private float rotationSmoothTime = 0.08f;
    
    [Tooltip("Constant offset added to rotation (usually leave at 0)")]
    [SerializeField] private float rotationOffset = 0f;
    
    [Header("Rotation Snapping (like a dial with notches)")]
    [Tooltip("If ON, part snaps to fixed angles instead of free rotation")]
    [SerializeField] private bool enableRotationSnap = false;
    
    [Range(5f, 90f)]
    [Tooltip("The angle between snap positions:\n• 90° = 4 positions (0, 90, 180, 270)\n• 45° = 8 positions\n• 15° = 24 positions (fine control)")]
    [SerializeField] private float snapAngleIncrement = 45f;
    
    [Range(1f, 20f)]
    [Tooltip("Snap speed:\n• 1 = instant snap (clicky)\n• 10 = smooth transition\n• 20 = very gradual")]
    [SerializeField] private float snapTolerance = 5f;
    
    [Header("Hand Calibration")]
    [Range(-180f, 180f)]
    [Tooltip("Makes your natural hand position = 0°\n\nTo calibrate:\n1. Turn ON 'Show Rotation UI'\n2. Hold hand in neutral position\n3. Set this to the angle shown")]
    [SerializeField] private float handAngleCalibration = 0f;
    
    [Header("Debug")]
    [Tooltip("Print rotation values to Console")]
    [SerializeField] private bool showRotationDebug = false;
    [Tooltip("Show rotation angles on screen (helpful for calibration)")]
    [SerializeField] private bool showRotationUI = false;

    [Header("References")]
    [SerializeField] private CarAssemblyManager assemblyManager;
    [SerializeField] private HandZone handZone;

    // MediaPipe landmark indices
    private const int WRIST = 0;
    private const int THUMB_TIP = 4;
    private const int INDEX_TIP = 8;
    private const int INDEX_MCP = 5;
    private const int MIDDLE_TIP = 12;
    private const int MIDDLE_MCP = 9;
    private const int RING_MCP = 13;
    private const int PINKY_MCP = 17;

    // Cached references
    private Transform multiHandAnnotation;
    private RectTransform canvasRect;
    private Renderer sphereRenderer;
    private AudioSource audioSource;
    private MovingCarPart[] cachedMovingParts;
    private float lastCacheTime;
    private const float CACHE_REFRESH_INTERVAL = 0.5f;
    private AssemblyTrackingIntegrator cachedTracker;

    // Drag state
    private bool isPinching;
    private bool isDraggingPart;
    private GameObject currentCarPart;
    private Vector3 grabOffset;
    private Vector3 partStartPosition;
    private Quaternion partStartRotation;
    private bool wasAlreadyTouching;

    // Position tracking
    private Vector3 targetPosition;
    private Vector3 velocity;
    private Vector3[] positionHistory;
    private int historyIndex;

    // Pinch detection
    private float[] pinchDistanceHistory;
    private int pinchHistoryIndex;
    private int pinchConfirmCounter;
    private bool wasPinching;

    // Rotation tracking state
    private float currentHandAngle = 0f;
    private float rotationVelocity = 0f;
    private float initialHandAngle = 0f;
    private float initialPartRotation = 0f;
    private float currentRotationAmount = 0f;
    private Quaternion originalPartRotation = Quaternion.identity;
    private bool hasInitializedRotation = false;

    // Public properties
    public Transform MultiHandAnnotation => multiHandAnnotation;
    public RectTransform CanvasRect => canvasRect;
    public bool IsDragging => isDraggingPart;
    public GameObject CurrentPart => currentCarPart;
    public float CurrentHandAngle => currentHandAngle;
    public bool IsRotationEnabled => enableHandRotation;
    public float InitialHandAngle => initialHandAngle;
    public float RotationDelta => hasInitializedRotation ? NormalizeAngle(currentHandAngle - initialHandAngle) : 0f;

    private void Awake()
    {
        InitializeCamera();
        InitializeHandSphere();
        InitializeHistoryArrays();
        InitializeAudioSource();

        if (assemblyManager == null) assemblyManager = FindObjectOfType<CarAssemblyManager>();
        if (handZone == null) handZone = FindObjectOfType<HandZone>();
    }

    private void Start()
    {
        Invoke(nameof(ResolveMediapipeRefs), 0.5f);
    }

    private void InitializeCamera()
    {
        if (followCamera == null) followCamera = Camera.main;
    }

    private void InitializeHandSphere()
    {
        if (handSphere == null)
        {
            handSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handSphere.name = "HandIndicator";
            Collider col = handSphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            sphereRenderer = handSphere.GetComponent<Renderer>();
            sphereRenderer.material = new Material(Shader.Find("Standard"));
            sphereRenderer.material.color = normalColor;
        }
        else
        {
            sphereRenderer = handSphere.GetComponent<Renderer>();
        }

        handSphere.transform.localScale = Vector3.one * sphereSize;
        targetPosition = handSphere.transform.position;
    }

    private void InitializeHistoryArrays()
    {
        positionHistory = new Vector3[stabilizationFrames];
        pinchDistanceHistory = new float[stabilizationFrames];

        for (int i = 0; i < stabilizationFrames; i++)
        {
            positionHistory[i] = Vector3.zero;
            pinchDistanceHistory[i] = 1f;
        }
    }

    private void InitializeAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (!TryProcessHand())
        {
            FadeOutSphere();
            if (isDraggingPart) ReleasePart();
        }
    }

    private bool TryProcessHand()
    {
        if (!ValidateMediapipeRefs()) return false;

        Transform pointList = FindActiveHandLandmarks();
        if (pointList == null) return false;

        Vector3 palmCenter = CalculatePalmCenter(pointList);
        Vector2 palmScreen = CanvasLocalToScreen(palmCenter, canvasRect);
        if (!IsInScreenBounds(palmScreen)) return false;

        UpdateSpherePosition(palmScreen);
        SetSphereAlpha(1f);

        // Calculate hand angle continuously for tracking
        currentHandAngle = CalculateHandAngle(pointList);

        ProcessPinchGesture(pointList);

        // Apply rotation if enabled and dragging
        if (enableHandRotation && isDraggingPart && currentCarPart != null)
            ApplyHandRotation();

        if (isDraggingPart && currentCarPart != null)
            UpdateDraggedPart();

        return true;
    }

    // ==================== HAND ROTATION SYSTEM ====================
    
    private float CalculateHandAngle(Transform pointList)
    {
        if (pointList.childCount < 18) return 0f;
        
        // Calculate angle from knuckle line (most stable for rotation)
        Vector3 indexMCP = pointList.GetChild(INDEX_MCP).localPosition;
        Vector3 pinkyMCP = pointList.GetChild(PINKY_MCP).localPosition;
        
        // Calculate the angle of the knuckle line
        float dx = indexMCP.x - pinkyMCP.x;
        float dy = indexMCP.y - pinkyMCP.y;
        
        float rawAngle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        
        // Apply calibration offset (adjustable in inspector)
        // Positive values rotate the "zero point" clockwise
        return NormalizeAngle(rawAngle - handAngleCalibration);
    }

    private void ApplyHandRotation()
    {
        if (currentCarPart == null) return;

        // Initialize rotation tracking on first frame after pickup
        if (!hasInitializedRotation)
        {
            initialHandAngle = currentHandAngle;
            
            // Store the part's original rotation
            originalPartRotation = currentCarPart.transform.rotation;
            currentRotationAmount = 0f;
            
            hasInitializedRotation = true;
            rotationVelocity = 0f;
            
            if (showRotationDebug)
            {
                Debug.Log($"[Rotation INIT] Hand: {initialHandAngle:F1}°");
            }
            return;
        }

        // Calculate how much the hand has rotated since pickup
        float handRotationDelta = NormalizeAngle(currentHandAngle - initialHandAngle);
        
        // Target rotation amount
        float targetAmount = (handRotationDelta * rotationMultiplier) + rotationOffset;

        // Apply snap if enabled - ALWAYS snaps to nearest increment
        if (enableRotationSnap)
        {
            // Round to nearest snap angle (e.g., if increment is 45, snaps to 0, 45, 90, 135...)
            targetAmount = Mathf.Round(targetAmount / snapAngleIncrement) * snapAngleIncrement;
        }

        // Smooth the rotation (snapTolerance now controls how quickly it snaps)
        // Lower tolerance = faster snap, higher = smoother transition
        float smoothTime = enableRotationSnap ? (snapTolerance * 0.01f) : rotationSmoothTime;
        currentRotationAmount = Mathf.SmoothDampAngle(currentRotationAmount, targetAmount, ref rotationVelocity, smoothTime);
        
        // Apply rotation around camera's forward axis from the original rotation
        Vector3 rotationAxis = followCamera != null ? followCamera.transform.forward : Vector3.forward;
        currentCarPart.transform.rotation = originalPartRotation * Quaternion.AngleAxis(currentRotationAmount, Quaternion.Inverse(originalPartRotation) * rotationAxis);

        if (showRotationDebug && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Rotation] HandDelta: {handRotationDelta:F1}° | Target: {targetAmount:F1}° | Applied: {currentRotationAmount:F1}°");
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void OnGUI()
    {
        if (showRotationUI && enableHandRotation)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;

            string displayText = $"Hand Angle: {currentHandAngle:F1}°\n";
            
            if (isDraggingPart && currentCarPart != null)
            {
                float partAngle = currentCarPart.transform.rotation.eulerAngles.z;
                float angleDelta = currentHandAngle - initialHandAngle;
                displayText += $"Part Angle: {partAngle:F1}°\n";
                displayText += $"Rotation Delta: {angleDelta:F1}°\n";
            }
            
            displayText += $"\nSettings:\n";
            displayText += $"Multiplier: {rotationMultiplier:F2}x\n";
            displayText += $"Smoothing: {rotationSmoothTime:F2}s\n";
            displayText += $"Snap: {(enableRotationSnap ? $"ON ({snapAngleIncrement}°)" : "OFF")}";

            GUI.Label(new Rect(10, 10, 350, 200), displayText, style);
        }
    }

    // ==================== CORE HAND PROCESSING ====================

    private bool ValidateMediapipeRefs()
    {
        if (multiHandAnnotation == null || canvasRect == null || followCamera == null)
        {
            ResolveMediapipeRefs();
            return multiHandAnnotation != null && canvasRect != null && followCamera != null;
        }
        return true;
    }

    private Transform FindActiveHandLandmarks()
    {
        if (multiHandAnnotation == null) return null;

        for (int i = 0; i < multiHandAnnotation.childCount; i++)
        {
            var hand = multiHandAnnotation.GetChild(i);
            if (!hand.gameObject.activeInHierarchy) continue;

            for (int j = 0; j < hand.childCount; j++)
            {
                var child = hand.GetChild(j);
                if (child.childCount >= 21) return child;
            }
        }

        return null;
    }

    private Vector3 CalculatePalmCenter(Transform pointList)
    {
        Vector3 wrist = pointList.GetChild(WRIST).localPosition;
        Vector3 indexBase = pointList.GetChild(INDEX_MCP).localPosition;
        Vector3 middleBase = pointList.GetChild(MIDDLE_MCP).localPosition;
        Vector3 ringBase = pointList.GetChild(RING_MCP).localPosition;
        Vector3 pinkyBase = pointList.GetChild(PINKY_MCP).localPosition;

        return wrist * 0.3f + indexBase * 0.175f + middleBase * 0.175f + ringBase * 0.175f + pinkyBase * 0.175f;
    }

    private bool IsInScreenBounds(Vector2 screenPos)
    {
        return screenPos.x >= -100 && screenPos.x <= Screen.width + 100 &&
               screenPos.y >= -100 && screenPos.y <= Screen.height + 100;
    }

    private void UpdateSpherePosition(Vector2 screenPos)
    {
        Vector3 worldPos = followCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, sphereDepth));

        positionHistory[historyIndex] = worldPos;
        historyIndex = (historyIndex + 1) % stabilizationFrames;

        Vector3 stabilizedPos = Vector3.zero;
        for (int i = 0; i < stabilizationFrames; i++)
            stabilizedPos += positionHistory[i];
        stabilizedPos /= stabilizationFrames;

        targetPosition = Vector3.SmoothDamp(targetPosition, stabilizedPos, ref velocity, positionSmoothTime);

        Vector3 frameDelta = targetPosition - handSphere.transform.position;
        if (frameDelta.magnitude > maxSpeedPerFrame)
            frameDelta = frameDelta.normalized * maxSpeedPerFrame;

        handSphere.transform.position += frameDelta;
    }

    private void ProcessPinchGesture(Transform pointList)
    {
        Vector3 thumbLocal = pointList.GetChild(THUMB_TIP).localPosition;
        Vector3 indexLocal = pointList.GetChild(INDEX_TIP).localPosition;
        Vector3 middleLocal = pointList.GetChild(MIDDLE_TIP).localPosition;

        Vector2 thumbScreen = CanvasLocalToScreen(thumbLocal, canvasRect);
        Vector2 indexScreen = CanvasLocalToScreen(indexLocal, canvasRect);
        Vector2 middleScreen = CanvasLocalToScreen(middleLocal, canvasRect);

        Vector2 fingerCenter = (thumbScreen + indexScreen + middleScreen) / 3f;

        float avgDistance =
            (Vector2.Distance(thumbScreen, fingerCenter) +
             Vector2.Distance(indexScreen, fingerCenter) +
             Vector2.Distance(middleScreen, fingerCenter)) / 3f;

        float normalizedDist = avgDistance / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);

        pinchDistanceHistory[pinchHistoryIndex] = normalizedDist;
        pinchHistoryIndex = (pinchHistoryIndex + 1) % pinchDistanceHistory.Length;

        float avgPinchDist = 0f;
        for (int i = 0; i < pinchDistanceHistory.Length; i++)
            avgPinchDist += pinchDistanceHistory[i];
        avgPinchDist /= pinchDistanceHistory.Length;

        bool wantsPinch = isPinching ? (avgPinchDist < pinchReleaseThreshold) : (avgPinchDist < pinchThreshold);

        if (wantsPinch != isPinching)
        {
            pinchConfirmCounter++;
            if (pinchConfirmCounter >= pinchConfirmFrames)
            {
                wasPinching = isPinching;
                isPinching = wantsPinch;
                pinchConfirmCounter = 0;

                if (isPinching && !wasPinching) OnPinchStart();
                else if (!isPinching && wasPinching) OnPinchEnd();
            }
        }
        else
        {
            pinchConfirmCounter = 0;
        }

        if (sphereRenderer != null)
            sphereRenderer.material.color = isPinching ? pinchColor : normalColor;
    }

    private void UpdateDraggedPart()
    {
        Vector3 newPos = handSphere.transform.position + grabOffset;
        newPos.z = dragZPosition;

        if (resetOnPartTouch)
        {
            bool isTouchingNow = CheckPartTouchingOthers();
            if (isTouchingNow && !wasAlreadyTouching)
            {
                if (instantSnapBack)
                {
                    currentCarPart.transform.position = partStartPosition;
                    currentCarPart.transform.rotation = partStartRotation;
                    grabOffset = partStartPosition - handSphere.transform.position;
                    grabOffset.z = dragZPosition - handSphere.transform.position.z;
                }
                else
                {
                    ResetAndReleasePart();
                    return;
                }
            }
        }

        if (currentCarPart != null) currentCarPart.transform.position = newPos;
        if (assemblyManager != null) assemblyManager.OnPartDrag(currentCarPart, newPos);
    }

    private void ResetAndReleasePart()
    {
        currentCarPart.transform.position = partStartPosition;
        currentCarPart.transform.rotation = partStartRotation;

        if (freezePartsOnPickup) UnfreezeAllParts();

        currentCarPart = null;
        isDraggingPart = false;
        grabOffset = Vector3.zero;
        wasAlreadyTouching = false;
        ResetRotationState();
    }

    private void ResetRotationState()
    {
        rotationVelocity = 0f;
        initialHandAngle = 0f;
        initialPartRotation = 0f;
        currentRotationAmount = 0f;
        originalPartRotation = Quaternion.identity;
        hasInitializedRotation = false;
    }

    private bool CheckPartTouchingOthers()
    {
        if (currentCarPart == null) return false;

        RefreshMovingPartsCache();

        Vector2 currentPos2D = new Vector2(currentCarPart.transform.position.x, currentCarPart.transform.position.y);

        foreach (MovingCarPart otherPart in cachedMovingParts)
        {
            if (otherPart == null || otherPart.gameObject == currentCarPart) continue;

            Vector2 otherPos2D = new Vector2(otherPart.transform.position.x, otherPart.transform.position.y);
            float distance = Vector2.Distance(currentPos2D, otherPos2D);
            if (distance < touchDetectionDistance) return true;
        }
        return false;
    }

    private void RefreshMovingPartsCache()
    {
        if (Time.time - lastCacheTime > CACHE_REFRESH_INTERVAL || cachedMovingParts == null)
        {
            cachedMovingParts = FindObjectsOfType<MovingCarPart>();
            lastCacheTime = Time.time;
        }
    }

    private void OnPinchStart()
    {
        if (handZone != null && !handZone.CanPickupPart())
            return;

        Collider[] colliders = Physics.OverlapSphere(handSphere.transform.position, pickupRange);

        float closestDist = float.MaxValue;
        GameObject closestPart = null;

        foreach (var col in colliders)
        {
            if (col.CompareTag("CarPart"))
            {
                float dist = Vector3.Distance(handSphere.transform.position, col.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPart = col.gameObject;
                }
            }
        }

        if (closestPart != null)
        {
            currentCarPart = closestPart;
            isDraggingPart = true;

            partStartPosition = currentCarPart.transform.position;
            partStartRotation = currentCarPart.transform.rotation;

            wasAlreadyTouching = CheckPartTouchingOthers();

            Vector3 partPos = currentCarPart.transform.position;
            Vector3 handPos = handSphere.transform.position;

            grabOffset = new Vector3(partPos.x - handPos.x, partPos.y - handPos.y, dragZPosition - handPos.z);

            // Initialize rotation tracking
            initialHandAngle = currentHandAngle;
            initialPartRotation = NormalizeAngle(currentCarPart.transform.rotation.eulerAngles.z);
            rotationVelocity = 0f;

            if (freezePartsOnPickup) FreezeAllOtherParts();

            if (assemblyManager != null) assemblyManager.OnPartPickup(currentCarPart);

            Rigidbody rb = currentCarPart.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            if (showRotationDebug && enableHandRotation)
            {
                Debug.Log($"[Pickup] Part: {currentCarPart.name} | Initial Hand: {initialHandAngle:F1}° | Initial Part: {initialPartRotation:F1}°");
            }
        }
    }

    private void OnPinchEnd()
    {
        ReleasePart();
    }

    private void ReleasePart()
    {
        if (currentCarPart != null)
        {
            Vector3 releasePos = currentCarPart.transform.position;
            bool snapped = false;

            if (assemblyManager != null)
                snapped = assemblyManager.OnPartRelease(currentCarPart, releasePos);

            if (cachedTracker == null)
                cachedTracker = FindObjectOfType<AssemblyTrackingIntegrator>();

            if (cachedTracker != null)
                cachedTracker.OnPartPlaced(currentCarPart.name, snapped);

            if (!snapped)
            {
                currentCarPart.transform.position = partStartPosition;
                currentCarPart.transform.rotation = partStartRotation;
            }

            if (freezePartsOnPickup) UnfreezeAllParts();
            if (snapped && handZone != null) handZone.OnPartPlacedSuccessfully();

            currentCarPart = null;
        }

        isDraggingPart = false;
        grabOffset = Vector3.zero;
        ResetRotationState();
    }

    private void ResolveMediapipeRefs()
    {
        GameObject annotationGO = GameObject.Find("Multi HandLandmarkList Annotation");
        if (annotationGO != null)
        {
            multiHandAnnotation = annotationGO.transform;
            Canvas canvas = multiHandAnnotation.GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    private Vector2 CanvasLocalToScreen(Vector3 localPos, RectTransform rect)
    {
        if (rect == null) return Vector2.zero;

        float nx = Mathf.Clamp01((localPos.x + rect.sizeDelta.x * 0.5f) / rect.sizeDelta.x);
        float ny = Mathf.Clamp01((localPos.y + rect.sizeDelta.y * 0.5f) / rect.sizeDelta.y);

        return new Vector2(nx * Screen.width, ny * Screen.height);
    }

    private void FadeOutSphere()
    {
        if (sphereRenderer == null) return;

        Color c = sphereRenderer.material.color;
        c.a = Mathf.Lerp(c.a, 0.3f, Time.deltaTime * 3f);
        sphereRenderer.material.color = c;
    }

    private void SetSphereAlpha(float alpha)
    {
        if (sphereRenderer == null) return;

        Color c = sphereRenderer.material.color;
        c.a = alpha;
        sphereRenderer.material.color = c;
    }

    private void FreezeAllOtherParts()
    {
        RefreshMovingPartsCache();
        foreach (MovingCarPart movingPart in cachedMovingParts)
            if (movingPart != null && movingPart.gameObject != currentCarPart)
                movingPart.SetFrozen(true);
    }

    private void UnfreezeAllParts()
    {
        if (cachedMovingParts == null) return;

        foreach (MovingCarPart movingPart in cachedMovingParts)
        {
            if (movingPart != null)
            {
                movingPart.SetFrozen(false);
                movingPart.ApplyVelocityBoost();
            }
        }
    }

    // ==================== PUBLIC API ====================
    
    public void ForceRelease()
    {
        if (isDraggingPart) ReleasePart();
    }

    // Basic settings
    public void SetSphereDepth(float depth) => sphereDepth = depth;
    public void SetDragZPosition(float zPos) => dragZPosition = zPos;
    public void SetFreezePartsOnPickup(bool freeze) => freezePartsOnPickup = freeze;
    public void SetResetOnPartTouch(bool reset) => resetOnPartTouch = reset;
    public void SetInstantSnapBack(bool instant) => instantSnapBack = instant;
    public void SetTouchDetectionDistance(float distance) => touchDetectionDistance = distance;

    // ========== ROTATION API ==========
    public void SetHandRotationEnabled(bool enabled) => enableHandRotation = enabled;
    public void SetRotationMultiplier(float multiplier) => rotationMultiplier = Mathf.Clamp(multiplier, 0.1f, 3.0f);
    public void SetRotationSensitivity(float sensitivity) => rotationMultiplier = Mathf.Clamp(sensitivity, 0.1f, 3.0f);
    public void SetRotationSmoothTime(float smooth) => rotationSmoothTime = Mathf.Clamp(smooth, 0f, 0.3f);
    public void SetRotationOffset(float offset) => rotationOffset = offset;
    public void SetRotationSnapEnabled(bool enabled) => enableRotationSnap = enabled;
    public void SetSnapAngleIncrement(float angle) => snapAngleIncrement = Mathf.Clamp(angle, 5f, 90f);
    public void SetSnapTolerance(float tolerance) => snapTolerance = Mathf.Clamp(tolerance, 1f, 20f);
    public void SetShowRotationDebug(bool enabled) => showRotationDebug = enabled;
    public void SetShowRotationUI(bool enabled) => showRotationUI = enabled;

    // Finger tracking for GameDataTracker
    public Vector2 GetFingerScreenPosition(int landmarkIndex)
    {
        if (!ValidateMediapipeRefs()) return Vector2.zero;
        Transform pointList = FindActiveHandLandmarks();
        if (pointList == null || landmarkIndex >= pointList.childCount) return Vector2.zero;

        Vector3 localPos = pointList.GetChild(landmarkIndex).localPosition;
        return CanvasLocalToScreen(localPos, canvasRect);
    }

    public bool TryGetPalmScreenPosition(out Vector2 palmScreen)
    {
        palmScreen = Vector2.zero;
        if (!ValidateMediapipeRefs()) return false;

        Transform pointList = FindActiveHandLandmarks();
        if (pointList == null) return false;

        Vector3 palmLocal = CalculatePalmCenter(pointList);
        palmScreen = CanvasLocalToScreen(palmLocal, canvasRect);
        return true;
    }
}