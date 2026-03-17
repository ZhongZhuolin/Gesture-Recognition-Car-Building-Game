using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// BULLETPROOF GameDataTracker - Tracks continuously without stopping
/// Enhanced with clear rotation data for Level 5 analysis
/// </summary>
public class GameDataTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    [Tooltip("Sampling interval in milliseconds (10ms = 100Hz, 16ms = 60Hz)")]
    [SerializeField] private float trackingIntervalMs = 10f;

    [Header("Save Location")]
    [SerializeField] private string companyFolderName = "ADHDCarBuilderGame";
    [SerializeField] private string gameFolderName = "CarAssemblyGame";

    [Header("References (Auto-found if empty)")]
    [SerializeField] private GameObject handSphere;
    [SerializeField] private HandZone handZone;
    [SerializeField] private CarAssemblyManager assemblyManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private CarPartMover carPartMover;

    [Header("Debug")]
    [SerializeField] private bool showTrackingStatus = false;

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
    private Camera cachedCamera;

    // Tracking state
    private List<TrackingDataPoint> trackingData;
    private string currentGameFolder;
    private string currentLevelFolder;
    private int currentLevel;
    private int trackingCounter;
    private Dictionary<int, int> levelAttemptCounts = new Dictionary<int, int>();
    private int totalDataPointsRecorded;
    private bool isTracking;
    
    // Safety tracking
    private int consecutiveErrors = 0;
    private const int MAX_CONSECUTIVE_ERRORS = 10;
    
    // Real-time tracking
    private float lastTrackTime = 0f;
    private float trackingInterval = 0.01f;
    
    // Velocity tracking (for research metrics)
    private Vector2 lastPalmPosition = Vector2.zero;
    private float lastPalmVelocity = 0f;
    private bool hasLastPosition = false;

    [Serializable]
    private class TrackingDataPoint
    {
        // ========== TIMING ==========
        public int timestampMs;                 // Time since level start (ms)
        
        // ========== HAND POSITION (Screen Coordinates) ==========
        public float palmX, palmY;              // Palm center position (pixels)
        public float thumbX, thumbY;            // Thumb tip position (pixels)
        public float indexX, indexY;            // Index finger tip position (pixels)
        public float middleX, middleY;          // Middle finger tip position (pixels)
        
        // ========== HAND MOVEMENT METRICS ==========
        public float palmVelocity;              // Hand movement speed (pixels/second)
        public float palmAcceleration;          // Hand acceleration (pixels/second²)
        
        // ========== GAME STATE ==========
        public int inPickupZone;                // 1 = hand is in the pickup zone
        public int isDragging;                  // 1 = currently holding a part
        public int isCorrectPart;               // 1 = holding the correct part for placement
        public string currentPartName;          // Name of part being dragged (empty if none)
        
        // ========== PLACEMENT EVENTS ==========
        public int placementAttempt;            // 1 = placement was attempted this frame
        public int placementSuccess;            // 1 = placement was successful
        public string placedPartName;           // Name of part placed (empty if no event)
        
        // ========== POSITION ACCURACY (when dragging) ==========
        public float distanceToTarget;          // Distance from part to target (world units)
        public float partX, partY;              // Current part position (world coords)
        public float targetX, targetY;          // Target position (world coords)
        
        // ========== ROTATION DATA (Level 5) ==========
        public float handAngleDeg;              // Hand rotation angle (-180 to 180°)
        public float handAngleDelta;            // Change in hand angle since pickup
        public float partRotationDeg;           // Part's current Z rotation (-180 to 180°)
        public float targetRotationDeg;         // Required rotation for snap
        public float rotationErrorDeg;          // Absolute rotation error (|part - target|)
        public int rotationEnabled;             // 1 = rotation control is active
    }

    private void Start()
    {
        CacheReferences();
        InitializeGameSessionFolder();
        SaveSessionInfoFile();
    }

    private void Update()
    {
        if (!isTracking) return;

        float currentTime = Time.realtimeSinceStartup;
        
        if (currentTime - lastTrackTime > trackingInterval * 2f)
        {
            if (showTrackingStatus)
            {
                Debug.LogWarning($"[GameDataTracker] Tracking gap detected! " +
                                $"Expected interval: {trackingInterval * 1000f:F1}ms, " +
                                $"Actual gap: {(currentTime - lastTrackTime) * 1000f:F1}ms");
            }
        }
    }

    private void CacheReferences()
    {
        try
        {
            cachedCamera = Camera.main;

            if (handSphere == null)
                handSphere = GameObject.Find("HandIndicator");
            if (handZone == null)
                handZone = FindObjectOfType<HandZone>();
            if (assemblyManager == null)
                assemblyManager = FindObjectOfType<CarAssemblyManager>();
            if (levelManager == null)
                levelManager = FindObjectOfType<LevelManager>();
            if (carPartMover == null)
                carPartMover = FindObjectOfType<CarPartMover>();

            Debug.Log("[GameDataTracker] References cached successfully");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameDataTracker] Error caching references (non-fatal): {e.Message}");
        }
    }

    private string GetGameRootPath()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string companyFolder = Path.Combine(docs, companyFolderName);
        string gameFolder = Path.Combine(companyFolder, gameFolderName);

        if (!Directory.Exists(companyFolder))
            Directory.CreateDirectory(companyFolder);

        if (!Directory.Exists(gameFolder))
            Directory.CreateDirectory(gameFolder);

        return gameFolder;
    }

    private void InitializeGameSessionFolder()
    {
        try
        {
            string root = GetGameRootPath();

            int gameIndex = 1;
            string candidate;

            do
            {
                candidate = Path.Combine(root, $"Game_{gameIndex:D3}");
                gameIndex++;
            }
            while (Directory.Exists(candidate));

            currentGameFolder = candidate;
            Directory.CreateDirectory(currentGameFolder);

            Debug.Log("=============================================================");
            Debug.Log("[GameDataTracker] NEW GAME SESSION STARTED");
            Debug.Log($"[GameDataTracker] Saving to: {currentGameFolder}");
            Debug.Log($"[GameDataTracker] Sampling Rate: {1000f / trackingIntervalMs:F0} Hz");
            Debug.Log("=============================================================");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataTracker] CRITICAL: Failed to initialize session folder: {e.Message}");
        }
    }

    private void SaveSessionInfoFile()
    {
        try
        {
            string infoPath = Path.Combine(currentGameFolder, "session_info.txt");
            string info =
                $"Session Started: {DateTime.Now}\n" +
                $"Unity Version: {Application.unityVersion}\n" +
                $"Platform: {Application.platform}\n" +
                $"Tracking Interval: {trackingIntervalMs}ms ({1000f / trackingIntervalMs:F0} Hz)\n" +
                $"Output Format: CSV (Excel-compatible)\n" +
                $"\n=== DATA COLUMNS ===\n" +
                $"Timestamp_ms: Time since level start in milliseconds\n" +
                $"Palm_X/Y: Hand palm center screen position\n" +
                $"Thumb/Index/Middle_X/Y: Finger tip screen positions\n" +
                $"In_Zone: 1 if hand is in pickup zone\n" +
                $"Dragging_Correct/Wrong: 1 if dragging correct/incorrect part\n" +
                $"Correct/Wrong_Placed: 1 if placement event occurred\n" +
                $"Part_Name: Name of part if placement event\n" +
                $"\n=== ROTATION DATA (Level 5) ===\n" +
                $"Hand_Rotation_Deg: Hand angle in degrees (-180 to 180)\n" +
                $"Part_Rotation_Deg: Part's current Z rotation\n" +
                $"Target_Rotation_Deg: Target rotation for snap\n" +
                $"Rotation_Error_Deg: Angular difference (part - target)\n" +
                $"Is_Rotating: 1 if rotation control is active\n";

            File.WriteAllText(infoPath, info);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameDataTracker] Failed to write session_info.txt (non-fatal): {e.Message}");
        }
    }

    // ============================================================
    // LEVEL TRACKING API
    // ============================================================
    public void StartLevelTracking(int levelNumber)
    {
        try
        {
            currentLevel = levelNumber;

            if (!levelAttemptCounts.ContainsKey(levelNumber))
                levelAttemptCounts[levelNumber] = 1;
            else
                levelAttemptCounts[levelNumber]++;

            int attemptNumber = levelAttemptCounts[levelNumber];

            string levelFolderName = attemptNumber == 1
                ? $"Level_{levelNumber}"
                : $"Level_{levelNumber}_Attempt_{attemptNumber}";

            currentLevelFolder = Path.Combine(currentGameFolder, levelFolderName);
            Directory.CreateDirectory(currentLevelFolder);

            trackingData = new List<TrackingDataPoint>(10000);
            trackingCounter = 0;
            totalDataPointsRecorded = 0;
            consecutiveErrors = 0;
            isTracking = true;

            float intervalSeconds = Mathf.Max(0.001f, trackingIntervalMs / 1000f);
            trackingInterval = intervalSeconds;
            lastTrackTime = Time.realtimeSinceStartup;
            
            CancelInvoke(nameof(TrackDataPoint));
            InvokeRepeating(nameof(TrackDataPoint), 0f, intervalSeconds);

            Debug.Log($"[GameDataTracker] ✅ CONTINUOUS TRACKING STARTED");
            Debug.Log($"[GameDataTracker] Level {levelNumber} (Attempt {attemptNumber})");
            Debug.Log($"[GameDataTracker] Tracking at {1000f / trackingIntervalMs:F0} Hz ({intervalSeconds * 1000f:F1}ms interval)");
            Debug.Log($"[GameDataTracker] Folder: {currentLevelFolder}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataTracker] Failed to start tracking: {e.Message}");
            isTracking = true;
        }
    }

    public void EndLevelTracking()
    {
        try
        {
            isTracking = false;
            CancelInvoke(nameof(TrackDataPoint));

            if (trackingData == null || trackingData.Count == 0)
            {
                Debug.LogWarning("[GameDataTracker] No data to save!");
                trackingData = null;
                return;
            }

            SaveTrackingData();
            SaveRotationSummary();
            
            Debug.Log($"[GameDataTracker] ✅ TRACKING ENDED SUCCESSFULLY");
            Debug.Log($"[GameDataTracker] Saved {trackingData.Count} data points for Level {currentLevel}");
            Debug.Log($"[GameDataTracker] Duration: {trackingCounter / 1000f:F2} seconds");

            trackingData = null;
            consecutiveErrors = 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataTracker] Error ending tracking: {e.Message}");
        }
    }

    // ============================================================
    // CONTINUOUS TRACKING LOOP
    // ============================================================
    private void TrackDataPoint()
    {
        if (!isTracking) return;

        lastTrackTime = Time.realtimeSinceStartup;

        try
        {
            // ========== HAND POSITIONS ==========
            Vector2 palmScreen = SafeGetPalmScreenPosition();
            Vector2 thumbScreen = SafeGetFingerScreenPosition(THUMB_TIP);
            Vector2 indexScreen = SafeGetFingerScreenPosition(INDEX_TIP);
            Vector2 middleScreen = SafeGetFingerScreenPosition(MIDDLE_TIP);

            // ========== VELOCITY & ACCELERATION ==========
            float velocity = 0f;
            float acceleration = 0f;
            if (hasLastPosition)
            {
                float deltaTime = trackingIntervalMs / 1000f;
                velocity = Vector2.Distance(palmScreen, lastPalmPosition) / deltaTime;
                acceleration = (velocity - lastPalmVelocity) / deltaTime;
            }
            lastPalmPosition = palmScreen;
            lastPalmVelocity = velocity;
            hasLastPosition = true;

            // ========== GAME STATE ==========
            int inZone = SafeGetInZoneStatus();
            bool isDragging = carPartMover != null && carPartMover.IsDragging;
            bool isCorrect = false;
            string currentPartName = "";
            
            if (isDragging && carPartMover.CurrentPart != null)
            {
                currentPartName = carPartMover.CurrentPart.name;
                isCorrect = assemblyManager != null && assemblyManager.CanPickupPart(carPartMover.CurrentPart);
            }

            // ========== POSITION ACCURACY ==========
            float distanceToTarget = 0f;
            Vector2 partPos = Vector2.zero;
            Vector2 targetPos = Vector2.zero;
            
            if (isDragging && carPartMover.CurrentPart != null)
            {
                Vector3 partWorld = carPartMover.CurrentPart.transform.position;
                partPos = new Vector2(partWorld.x, partWorld.y);
                targetPos = SafeGetTargetPosition();
                distanceToTarget = Vector2.Distance(partPos, targetPos);
            }

            // ========== ROTATION TRACKING ==========
            float handAngle = SafeGetHandRotationAngle();
            float handAngleDelta = SafeGetHandAngleDelta();
            float partAngle = SafeGetPartRotationZ();
            float targetAngle = SafeGetTargetRotation();
            float rotationError = Mathf.Abs(NormalizeAngle(partAngle - targetAngle));
            bool rotationEnabled = carPartMover != null && carPartMover.IsRotationEnabled;

            var dataPoint = new TrackingDataPoint
            {
                // Timing
                timestampMs = trackingCounter,
                
                // Hand positions
                palmX = palmScreen.x,
                palmY = palmScreen.y,
                thumbX = thumbScreen.x,
                thumbY = thumbScreen.y,
                indexX = indexScreen.x,
                indexY = indexScreen.y,
                middleX = middleScreen.x,
                middleY = middleScreen.y,
                
                // Movement metrics
                palmVelocity = velocity,
                palmAcceleration = acceleration,
                
                // Game state
                inPickupZone = inZone,
                isDragging = isDragging ? 1 : 0,
                isCorrectPart = isCorrect ? 1 : 0,
                currentPartName = currentPartName,
                
                // Placement events (set by RecordPartPlacement)
                placementAttempt = 0,
                placementSuccess = 0,
                placedPartName = "",
                
                // Position accuracy
                distanceToTarget = distanceToTarget,
                partX = partPos.x,
                partY = partPos.y,
                targetX = targetPos.x,
                targetY = targetPos.y,
                
                // Rotation
                handAngleDeg = handAngle,
                handAngleDelta = handAngleDelta,
                partRotationDeg = partAngle,
                targetRotationDeg = targetAngle,
                rotationErrorDeg = rotationError,
                rotationEnabled = rotationEnabled ? 1 : 0
            };

            trackingData.Add(dataPoint);
            totalDataPointsRecorded++;
            trackingCounter += Mathf.RoundToInt(trackingIntervalMs);

            consecutiveErrors = 0;

            if (showTrackingStatus && totalDataPointsRecorded % 500 == 0)
            {
                Debug.Log($"[GameDataTracker] ✅ {totalDataPointsRecorded} pts @ {trackingCounter / 1000f:F1}s | " +
                          $"Vel: {velocity:F0} px/s | DistToTarget: {distanceToTarget:F2}");
            }
        }
        catch (Exception e)
        {
            consecutiveErrors++;
            
            if (consecutiveErrors <= 5)
            {
                Debug.LogWarning($"[GameDataTracker] Tracking error #{consecutiveErrors}: {e.Message}");
            }

            // Add default point to maintain timeline continuity
            try
            {
                trackingData.Add(CreateDefaultDataPoint());
                trackingCounter += Mathf.RoundToInt(trackingIntervalMs);
            }
            catch
            {
                trackingCounter += Mathf.RoundToInt(trackingIntervalMs);
            }
        }
    }
    
    private TrackingDataPoint CreateDefaultDataPoint()
    {
        return new TrackingDataPoint
        {
            timestampMs = trackingCounter,
            palmX = 0, palmY = 0,
            thumbX = 0, thumbY = 0,
            indexX = 0, indexY = 0,
            middleX = 0, middleY = 0,
            palmVelocity = 0,
            palmAcceleration = 0,
            inPickupZone = 0,
            isDragging = 0,
            isCorrectPart = 0,
            currentPartName = "",
            placementAttempt = 0,
            placementSuccess = 0,
            placedPartName = "",
            distanceToTarget = 0,
            partX = 0, partY = 0,
            targetX = 0, targetY = 0,
            handAngleDeg = 0,
            handAngleDelta = 0,
            partRotationDeg = 0,
            targetRotationDeg = 0,
            rotationErrorDeg = 0,
            rotationEnabled = 0
        };
    }

    // ============================================================
    // SAFE GETTER METHODS
    // ============================================================
    
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    
    private Vector2 SafeGetPalmScreenPosition()
    {
        try
        {
            if (carPartMover != null && carPartMover.TryGetPalmScreenPosition(out Vector2 palm))
                return palm;

            if (handSphere != null && cachedCamera != null)
            {
                Vector3 screenPos = cachedCamera.WorldToScreenPoint(handSphere.transform.position);
                return new Vector2(screenPos.x, screenPos.y);
            }
        }
        catch { }
        
        return Vector2.zero;
    }

    private Vector2 SafeGetFingerScreenPosition(int landmarkIndex)
    {
        try
        {
            if (carPartMover != null)
                return carPartMover.GetFingerScreenPosition(landmarkIndex);
        }
        catch { }
        
        return Vector2.zero;
    }

    private int SafeGetInZoneStatus()
    {
        try
        {
            if (handZone != null && handZone.CanPickupPart())
                return 1;
        }
        catch { }
        
        return 0;
    }

    private float SafeGetHandRotationAngle()
    {
        try
        {
            if (carPartMover != null)
                return carPartMover.CurrentHandAngle;
        }
        catch { }
        
        return 0f;
    }

    private float SafeGetPartRotationZ()
    {
        try
        {
            if (carPartMover != null && carPartMover.CurrentPart != null)
            {
                float angle = carPartMover.CurrentPart.transform.rotation.eulerAngles.z;
                return NormalizeAngle(angle);
            }
        }
        catch { }
        
        return 0f;
    }

    private float SafeGetTargetRotation()
    {
        try
        {
            if (carPartMover != null && carPartMover.CurrentPart != null && assemblyManager != null)
            {
                return assemblyManager.GetPartTargetRotation(carPartMover.CurrentPart);
            }
        }
        catch { }
        
        return 0f;
    }
    
    private Vector2 SafeGetTargetPosition()
    {
        try
        {
            if (carPartMover != null && carPartMover.CurrentPart != null && assemblyManager != null)
            {
                Vector3 targetPos = assemblyManager.GetPartTargetPosition(carPartMover.CurrentPart);
                return new Vector2(targetPos.x, targetPos.y);
            }
        }
        catch { }
        
        return Vector2.zero;
    }
    
    private float SafeGetHandAngleDelta()
    {
        try
        {
            if (carPartMover != null && carPartMover.IsDragging)
            {
                return carPartMover.RotationDelta;
            }
        }
        catch { }
        
        return 0f;
    }

    public void RecordPartPlacement(string partName, bool wasCorrect)
    {
        if (!isTracking || trackingData == null) return;

        try
        {
            Vector2 palmScreen = SafeGetPalmScreenPosition();
            Vector2 thumbScreen = SafeGetFingerScreenPosition(THUMB_TIP);
            Vector2 indexScreen = SafeGetFingerScreenPosition(INDEX_TIP);
            Vector2 middleScreen = SafeGetFingerScreenPosition(MIDDLE_TIP);

            float handAngle = SafeGetHandRotationAngle();
            float handDelta = SafeGetHandAngleDelta();
            float partAngle = SafeGetPartRotationZ();
            float targetAngle = SafeGetTargetRotation();
            float rotationError = Mathf.Abs(NormalizeAngle(partAngle - targetAngle));
            Vector2 targetPos = SafeGetTargetPosition();
            bool rotationEnabled = carPartMover != null && carPartMover.IsRotationEnabled;

            var dataPoint = new TrackingDataPoint
            {
                timestampMs = trackingCounter,
                palmX = palmScreen.x,
                palmY = palmScreen.y,
                thumbX = thumbScreen.x,
                thumbY = thumbScreen.y,
                indexX = indexScreen.x,
                indexY = indexScreen.y,
                middleX = middleScreen.x,
                middleY = middleScreen.y,
                palmVelocity = 0,
                palmAcceleration = 0,
                inPickupZone = SafeGetInZoneStatus(),
                isDragging = 0,
                isCorrectPart = wasCorrect ? 1 : 0,
                currentPartName = "",
                placementAttempt = 1,
                placementSuccess = wasCorrect ? 1 : 0,
                placedPartName = partName,
                distanceToTarget = 0,
                partX = targetPos.x,
                partY = targetPos.y,
                targetX = targetPos.x,
                targetY = targetPos.y,
                handAngleDeg = handAngle,
                handAngleDelta = handDelta,
                partRotationDeg = partAngle,
                targetRotationDeg = targetAngle,
                rotationErrorDeg = rotationError,
                rotationEnabled = rotationEnabled ? 1 : 0
            };

            trackingData.Add(dataPoint);
            trackingCounter += Mathf.RoundToInt(trackingIntervalMs);

            Debug.Log($"[GameDataTracker] ✅ {(wasCorrect ? "SUCCESS" : "FAIL")}: {partName} | " +
                     $"Rotation Error: {rotationError:F1}°");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameDataTracker] Error recording placement: {e.Message}");
        }
    }

    // ============================================================
    // SAVE FILES
    // ============================================================
    private void SaveTrackingData()
    {
        try
        {
            int attemptNumber = levelAttemptCounts.ContainsKey(currentLevel) ? levelAttemptCounts[currentLevel] : 1;

            string baseFilename = attemptNumber == 1
                ? $"Level_{currentLevel}_Data"
                : $"Level_{currentLevel}_Attempt_{attemptNumber}_Data";

            string csvPath = Path.Combine(currentLevelFolder, $"{baseFilename}.csv");
            SaveAsCsv(csvPath);

            Debug.Log($"[GameDataTracker] ✅ CSV file saved to: {currentLevelFolder}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataTracker] Failed to save tracking data: {e.Message}");
        }
    }

    private void SaveAsCsv(string path)
    {
        try
        {
            var csv = new StringBuilder(trackingData.Count * 300);

            // Research-friendly CSV header with clear column descriptions
            csv.AppendLine(
                // Timing
                "Time_ms," +
                // Hand position (screen pixels)
                "Palm_X,Palm_Y,Thumb_X,Thumb_Y,Index_X,Index_Y,Middle_X,Middle_Y," +
                // Movement metrics
                "Velocity_px_s,Acceleration_px_s2," +
                // Game state (0/1 flags)
                "In_Zone,Is_Dragging,Is_Correct_Part," +
                // Part being dragged
                "Current_Part," +
                // Placement events
                "Placement_Attempt,Placement_Success,Placed_Part," +
                // Position accuracy (world units)
                "Distance_To_Target,Part_X,Part_Y,Target_X,Target_Y," +
                // Rotation data (degrees, -180 to 180)
                "Hand_Angle,Hand_Angle_Delta,Part_Rotation,Target_Rotation,Rotation_Error,Rotation_Enabled"
            );

            foreach (var dp in trackingData)
            {
                csv.AppendLine(
                    $"{dp.timestampMs}," +
                    $"{dp.palmX:F1},{dp.palmY:F1}," +
                    $"{dp.thumbX:F1},{dp.thumbY:F1}," +
                    $"{dp.indexX:F1},{dp.indexY:F1}," +
                    $"{dp.middleX:F1},{dp.middleY:F1}," +
                    $"{dp.palmVelocity:F1},{dp.palmAcceleration:F1}," +
                    $"{dp.inPickupZone},{dp.isDragging},{dp.isCorrectPart}," +
                    $"{EscapeCsvField(dp.currentPartName)}," +
                    $"{dp.placementAttempt},{dp.placementSuccess},{EscapeCsvField(dp.placedPartName)}," +
                    $"{dp.distanceToTarget:F3},{dp.partX:F3},{dp.partY:F3},{dp.targetX:F3},{dp.targetY:F3}," +
                    $"{dp.handAngleDeg:F1},{dp.handAngleDelta:F1},{dp.partRotationDeg:F1}," +
                    $"{dp.targetRotationDeg:F1},{dp.rotationErrorDeg:F1},{dp.rotationEnabled}"
                );
            }

            File.WriteAllText(path, csv.ToString());
            Debug.Log($"[GameDataTracker] ✅ CSV: {path} ({trackingData.Count} rows)");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataTracker] CSV save failed: {e.Message}");
        }
    }
    
    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(",") || field.Contains("\""))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }

    private void SaveRotationSummary()
    {
        try
        {
            // Analyze tracking data for research metrics
            bool hasRotationData = false;
            float totalRotationError = 0f;
            int rotationSamples = 0;
            float maxRotationError = 0f;
            int successfulPlacements = 0;
            int failedPlacements = 0;
            float totalVelocity = 0f;
            int velocitySamples = 0;
            float maxVelocity = 0f;

            foreach (var dp in trackingData)
            {
                // Rotation metrics
                if (dp.rotationEnabled == 1 && dp.isDragging == 1)
                {
                    hasRotationData = true;
                    totalRotationError += dp.rotationErrorDeg;
                    rotationSamples++;
                    if (dp.rotationErrorDeg > maxRotationError)
                        maxRotationError = dp.rotationErrorDeg;
                }
                
                // Velocity metrics
                if (dp.palmVelocity > 0)
                {
                    totalVelocity += dp.palmVelocity;
                    velocitySamples++;
                    if (dp.palmVelocity > maxVelocity)
                        maxVelocity = dp.palmVelocity;
                }
                
                // Placement counts
                if (dp.placementSuccess == 1) successfulPlacements++;
                if (dp.placementAttempt == 1 && dp.placementSuccess == 0) failedPlacements++;
            }

            int attemptNumber = levelAttemptCounts.ContainsKey(currentLevel) ? levelAttemptCounts[currentLevel] : 1;
            string baseFilename = attemptNumber == 1
                ? $"Level_{currentLevel}_Summary"
                : $"Level_{currentLevel}_Attempt_{attemptNumber}_Summary";

            string summaryPath = Path.Combine(currentLevelFolder, $"{baseFilename}.txt");
            
            float avgError = rotationSamples > 0 ? totalRotationError / rotationSamples : 0;
            float avgVelocity = velocitySamples > 0 ? totalVelocity / velocitySamples : 0;
            float durationSec = trackingCounter / 1000f;

            string summary =
                $"╔══════════════════════════════════════════════════════════════╗\n" +
                $"║            LEVEL {currentLevel} PERFORMANCE SUMMARY                    ║\n" +
                $"╠══════════════════════════════════════════════════════════════╣\n" +
                $"║ Generated: {DateTime.Now,-48} ║\n" +
                $"╠══════════════════════════════════════════════════════════════╣\n" +
                $"║ SESSION INFO                                                 ║\n" +
                $"║   Duration: {durationSec:F2} seconds                                   ║\n" +
                $"║   Data Points: {trackingData.Count,-46} ║\n" +
                $"║   Sampling Rate: {1000f / trackingIntervalMs:F0} Hz                                        ║\n" +
                $"╠══════════════════════════════════════════════════════════════╣\n" +
                $"║ MOVEMENT METRICS                                             ║\n" +
                $"║   Average Hand Velocity: {avgVelocity:F1} px/s                        ║\n" +
                $"║   Maximum Hand Velocity: {maxVelocity:F1} px/s                        ║\n" +
                $"╠══════════════════════════════════════════════════════════════╣\n" +
                $"║ PLACEMENT RESULTS                                            ║\n" +
                $"║   Successful: {successfulPlacements,-48} ║\n" +
                $"║   Failed: {failedPlacements,-52} ║\n" +
                $"║   Success Rate: {(successfulPlacements + failedPlacements > 0 ? (successfulPlacements * 100f / (successfulPlacements + failedPlacements)) : 0):F1}%                                       ║\n";

            if (hasRotationData)
            {
                summary +=
                    $"╠══════════════════════════════════════════════════════════════╣\n" +
                    $"║ ROTATION METRICS (Level 5)                                   ║\n" +
                    $"║   Rotation Samples: {rotationSamples,-41} ║\n" +
                    $"║   Average Rotation Error: {avgError:F1}°                              ║\n" +
                    $"║   Maximum Rotation Error: {maxRotationError:F1}°                              ║\n" +
                    $"║   Time Rotating: {(rotationSamples * 100f / trackingData.Count):F1}%                                        ║\n";
            }
            
            summary += $"╚══════════════════════════════════════════════════════════════╝\n";

            File.WriteAllText(summaryPath, summary);
            Debug.Log($"[GameDataTracker] ✅ Summary saved: {summaryPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameDataTracker] Failed to save summary: {e.Message}");
        }
    }

    public string GetCurrentGameFolderPath() => currentGameFolder;

    private void OnApplicationQuit()
    {
        if (isTracking && trackingData != null && trackingData.Count > 0)
        {
            Debug.LogWarning("[GameDataTracker] Application quitting - saving tracking data...");
            EndLevelTracking();
        }
    }

    private void OnDestroy()
    {
        if (isTracking)
        {
            Debug.LogWarning("[GameDataTracker] GameObject destroyed - attempting to save tracking data...");
            EndLevelTracking();
        }
    }
}