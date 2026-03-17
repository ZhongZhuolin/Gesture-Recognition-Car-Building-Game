using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages level loading, transitions, and progression.
/// Enhanced with rotation support for Level 5, maze code removed.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Level Configuration")]
    [SerializeField] private List<LevelData> levels = new List<LevelData>();
    [SerializeField] private int currentLevelIndex = 0;
    
    [Header("References")]
    [SerializeField] private CarAssemblyManager assemblyManager;
    [SerializeField] private CompletionScreen completionScreen;
    [SerializeField] private Transform carSpawnPoint;
    [SerializeField] private GameDataTracker dataTracker;
    [SerializeField] private CarPartMover carPartMover;
    
    [Header("Settings")]
    [SerializeField] private bool autoLoadFirstLevel = true;
    
    private GameObject currentCarInstance;
    private List<GameObject> currentPartInstances = new List<GameObject>();
    
    private static LevelManager instance;
    public static LevelManager Instance => instance;
    public LevelData CurrentLevel => currentLevelIndex < levels.Count ? levels[currentLevelIndex] : null;
    public int CurrentLevelNumber => currentLevelIndex + 1;
    public int TotalLevels => levels.Count;
    public bool HasNextLevel => currentLevelIndex < levels.Count - 1;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        CacheReferences();
        
        if (autoLoadFirstLevel && levels.Count > 0)
            LoadLevel(0);
    }
    
    private void CacheReferences()
    {
        if (assemblyManager == null) assemblyManager = FindObjectOfType<CarAssemblyManager>();
        if (completionScreen == null) completionScreen = FindObjectOfType<CompletionScreen>();
        if (dataTracker == null) dataTracker = FindObjectOfType<GameDataTracker>();
        if (carPartMover == null) carPartMover = FindObjectOfType<CarPartMover>();
    }
    
    public void LoadLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count) return;
        
        currentLevelIndex = levelIndex;
        LevelData level = levels[currentLevelIndex];
        
        CleanupCurrentLevel();
        SetupLevel(level);
        
        dataTracker?.StartLevelTracking(currentLevelIndex + 1);
        
        Debug.Log($"[LevelManager] Loaded {level.levelName} (Level {currentLevelIndex + 1})");
    }
    
    private void CleanupCurrentLevel()
    {
        foreach (GameObject part in currentPartInstances)
            if (part != null) Destroy(part);
        currentPartInstances.Clear();
        
        if (currentCarInstance != null)
        {
            Destroy(currentCarInstance);
            currentCarInstance = null;
        }
        
        assemblyManager?.ClearAllParts();
    }
    
    private void SetupLevel(LevelData level)
    {
        Vector3 spawnPos = carSpawnPoint != null ? carSpawnPoint.position : Vector3.zero;
        Quaternion spawnRot = carSpawnPoint != null ? carSpawnPoint.rotation : Quaternion.identity;
        
        if (level.carPrefab != null)
        {
            currentCarInstance = Instantiate(level.carPrefab, spawnPos, spawnRot);
            currentCarInstance.name = $"{level.levelName}_Car";
        }
        
        if (assemblyManager != null)
        {
            assemblyManager.InitializeFromLevelData(level);
            assemblyManager.SetSnapDistance(level.recommendedSnapDistance);
            assemblyManager.enforceOrder = level.enforcePartOrder;
            
            // Set rotation tolerance for Level 5
            if (level.enableHandRotation)
            {
                assemblyManager.SetRotationSnapTolerance(level.rotationSnapTolerance);
            }
        }
        
        // Configure CarPartMover with level settings
        if (carPartMover != null)
        {
            // Basic settings
            carPartMover.SetSphereDepth(level.handSphereDepth);
            carPartMover.SetDragZPosition(level.dragZPosition);
            carPartMover.SetFreezePartsOnPickup(level.freezePartsOnPickup);
            carPartMover.SetResetOnPartTouch(level.resetOnPartTouch);
            carPartMover.SetInstantSnapBack(level.instantSnapBack);
            carPartMover.SetTouchDetectionDistance(level.touchDetectionDistance);
            
            // ========== ROTATION SETTINGS ==========
            carPartMover.SetHandRotationEnabled(level.enableHandRotation);
            
            if (level.enableHandRotation)
            {
                carPartMover.SetRotationMultiplier(level.rotationMultiplier);
                carPartMover.SetRotationSmoothTime(level.rotationSmoothTime);
                carPartMover.SetRotationOffset(level.rotationOffset);
                carPartMover.SetRotationSnapEnabled(level.enableRotationSnap);
                carPartMover.SetSnapAngleIncrement(level.snapAngleIncrement);
                carPartMover.SetSnapTolerance(level.snapTolerance);
                
                Debug.Log($"[LevelManager] ✅ ROTATION ENABLED");
                Debug.Log($"  Multiplier: {level.rotationMultiplier}x");
                Debug.Log($"  Smoothing: {level.rotationSmoothTime}s");
                Debug.Log($"  Snap: {(level.enableRotationSnap ? $"ON ({level.snapAngleIncrement}°)" : "OFF")}");
                Debug.Log($"  Placement Tolerance: {level.rotationSnapTolerance}°");
            }
            else
            {
                Debug.Log($"[LevelManager] Rotation DISABLED for this level");
            }
        }
        
        // Instantiate and configure parts
        foreach (var partConfig in level.parts)
        {
            if (partConfig.partPrefab == null) continue;
            
            GameObject partInstance = Instantiate(
                partConfig.partPrefab,
                partConfig.startPosition,
                Quaternion.Euler(partConfig.startRotation)
            );
            
            partInstance.name = partConfig.partName;
            currentPartInstances.Add(partInstance);
            
            // Add MovingCarPart if enabled
            if (level.enableMovingParts)
            {
                MovingCarPart movingPart = partInstance.GetComponent<MovingCarPart>();
                if (movingPart == null)
                    movingPart = partInstance.AddComponent<MovingCarPart>();
                
                movingPart.SetMovementPattern(MovingCarPart.MovementPattern.Bounce);
                movingPart.SetMoveSpeed(level.movingPartsSpeed);
                movingPart.SetEnablePartCollision(level.enablePartCollision);
                movingPart.SetBounceOffGhosts(level.partsBouncOffGhosts);
                movingPart.SetGhostBounceDistance(level.ghostBounceDistance);
                
                if (carSpawnPoint != null)
                    movingPart.SetCarAvoidanceZone(carSpawnPoint.position, 3f);
            }
            
            // Add Z-lock to all parts
            LockZPosition zLock = partInstance.AddComponent<LockZPosition>();
            zLock.SetLockedZ(partConfig.startPosition.z);
            
            // Register with assembly manager
            assemblyManager?.RegisterPart(
                partInstance, partConfig.partName, partConfig.targetPosition,
                partConfig.targetRotation, partConfig.placementOrder,
                partConfig.labelOffset, partConfig.labelFontSize
            );
        }
        
        assemblyManager?.FinalizeSetup();
        
        LogLevelConfiguration(level);
    }
    
    private void LogLevelConfiguration(LevelData level)
    {
        Debug.Log("=============================================================");
        Debug.Log($"[LevelManager] {level.levelName} Configuration:");
        Debug.Log($"  Moving Parts: {level.enableMovingParts}");
        Debug.Log($"  Part Collision: {level.enablePartCollision}");
        Debug.Log($"  Ghost Bounce: {level.partsBouncOffGhosts}");
        Debug.Log($"  Hand Rotation: {level.enableHandRotation}");
        if (level.enableHandRotation)
        {
            Debug.Log($"    - Multiplier: {level.rotationMultiplier}x");
            Debug.Log($"    - Smoothing: {level.rotationSmoothTime}s");
            Debug.Log($"    - Placement Tolerance: {level.rotationSnapTolerance}°");
        }
        Debug.Log("=============================================================");
    }
    
    public void LoadNextLevel()
    {
        dataTracker?.EndLevelTracking();
        
        if (HasNextLevel)
            LoadLevel(currentLevelIndex + 1);
        else
            Debug.Log("All levels complete!");
    }
    
    public void RestartCurrentLevel()
    {
        dataTracker?.EndLevelTracking();
        LoadLevel(currentLevelIndex);
    }
    
    public void SaveProgress()
    {
        PlayerPrefs.SetInt("CompletedLevels", currentLevelIndex);
        PlayerPrefs.Save();
    }
    
    public void LoadProgress()
    {
        currentLevelIndex = Mathf.Clamp(PlayerPrefs.GetInt("CompletedLevels", 0), 0, levels.Count - 1);
    }
}