using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject containing all data for a single level.
/// Optimized with rotation settings for Level 5, maze code removed.
/// Create via Assets > Create > Car Assembly > Level Data
/// </summary>
[CreateAssetMenu(fileName = "New Level", menuName = "Car Assembly/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Level Info")]
    public string levelName = "Level 1";
    public int levelNumber = 1;
    [TextArea(3, 5)]
    public string levelDescription = "Build your first car!";
    public Sprite levelThumbnail;
    
    [Header("Difficulty")]
    public int targetTimeSeconds = 180;
    public bool enforcePartOrder = false;
    public float recommendedSnapDistance = 3f;
    
    [Header("Moving Parts")]
    public bool enableMovingParts = false;
    [Range(0.5f, 10f)]
    public float movingPartsSpeed = 2.0f;
    
    [Header("Part Collision Settings")]
    [Tooltip("Enable collision between moving parts")]
    public bool enablePartCollision = true;
    
    [Header("Ghost Collision")]
    [Tooltip("Parts bounce off ghost objects (target positions)")]
    public bool partsBouncOffGhosts = false;
    [Tooltip("Distance from ghost to trigger bounce")]
    public float ghostBounceDistance = 1.0f;
    
    [Header("Hand Sphere Settings")]
    [Range(3f, 15f)]
    public float handSphereDepth = 5f;
    [Range(-10f, 20f)]
    public float dragZPosition = 8f;
    public bool freezePartsOnPickup = false;
    public bool resetOnPartTouch = false;
    public bool instantSnapBack = false;
    [Range(0.5f, 2.0f)]
    public float touchDetectionDistance = 0.6f;
    
    [Header("========== LEVEL 5: ROTATION SETTINGS ==========")]
    [Tooltip("Enable hand rotation control for this level")]
    public bool enableHandRotation = false;
    
    [Range(0.1f, 3.0f)]
    [Tooltip("Rotation multiplier: 1.0 = 1:1 hand-to-part rotation")]
    public float rotationMultiplier = 1.0f;
    
    [Range(0f, 0.3f)]
    [Tooltip("Rotation smoothing time in seconds")]
    public float rotationSmoothTime = 0.08f;
    
    [Tooltip("Offset angle to calibrate rotation (degrees)")]
    public float rotationOffset = 0f;
    
    [Header("Rotation Snap (Optional)")]
    [Tooltip("Enable snapping to specific angles")]
    public bool enableRotationSnap = false;
    
    [Range(5f, 90f)]
    [Tooltip("Snap to nearest multiple of this angle")]
    public float snapAngleIncrement = 45f;
    
    [Range(1f, 20f)]
    [Tooltip("Tolerance before snapping activates")]
    public float snapTolerance = 10f;
    
    [Header("Rotation Tolerance for Placement")]
    [Range(5f, 45f)]
    [Tooltip("How close rotation must be to target for successful snap")]
    public float rotationSnapTolerance = 15f;
    
    [Header("Car Configuration")]
    public GameObject carPrefab;
    public List<CarPartConfig> parts = new List<CarPartConfig>();
    
    [Header("Visual Settings")]
    public Color ghostColor = new Color(0.5f, 0.8f, 1f, 0.3f);
    public Color highlightColor = Color.yellow;
    public Color correctPlacementColor = Color.green;
    
    [Header("Audio")]
    public AudioClip levelMusic;
    public AudioClip snapSound;
    public AudioClip wrongPlacementSound;
    public AudioClip completionSound;
    
    [System.Serializable]
    public class CarPartConfig
    {
        [Header("Part Info")]
        public string partName;
        public GameObject partPrefab;
        
        [Header("Start Transform")]
        public Vector3 startPosition;
        public Vector3 startRotation;
        
        [Header("Target Transform")]
        public Vector3 targetPosition;
        public Vector3 targetRotation;
        
        [Header("Placement")]
        public int placementOrder = 0;
        
        [Header("Label Settings")]
        public Vector3 labelOffset = Vector3.up * 0.7f;
        public float labelFontSize = 2f;
    }
}