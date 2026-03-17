// CarAssemblyManager with Level 5 Rotation Support
// Handles rotation tolerance for part placement, maze code removed
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CarAssemblyManager : MonoBehaviour
{
    [Header("Assembly Configuration")]
    [SerializeField] private CarPartMover partMover;
    [Range(0.1f, 15f)]
    [SerializeField] private float snapDistance = 3f;
    [SerializeField] private float smoothSnapDuration = 0.3f;
    [SerializeField] private bool showLabels = true;
    [SerializeField] private bool pulseGhosts = true;

    [Header("Placement Order")]
    public bool enforceOrder = false;
    [SerializeField] private Color lockedPartColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Rotation Tolerance (Level 5)")]
    [Range(5f, 45f)]
    [SerializeField] private float rotationSnapTolerance = 15f;
    [SerializeField] private bool requireRotationMatch = false;

    [Header("Game End")]
    [SerializeField] private bool endGameOnCompletion = true;
    [SerializeField] private float endGameDelay = 2f;
    [SerializeField] private GameObject completionScreenObject;
    [SerializeField] private UnityEngine.Events.UnityEvent onGameComplete;

    [Header("Visual Settings")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Color ghostColor = new Color(0.5f, 0.8f, 1f, 0.3f);
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color correctPlacementColor = Color.green;
    [SerializeField] private Color wrongPlacementColor = Color.red;
    [SerializeField] private Color rotationWrongColor = new Color(1f, 0.5f, 0f, 1f);

    [Header("Car Parts Setup")]
    [SerializeField] private List<CarPartSetup> carParts = new List<CarPartSetup>();

    [Header("Audio")]
    [SerializeField] private AudioClip snapSound;
    [SerializeField] private AudioClip wrongPlacementSound;
    [SerializeField] private AudioClip completionSound;

    [Header("Ghost Collision Safety")]
    [SerializeField] private string ghostLayerName = "Ghost";
    [SerializeField] private string movingPartLayerName = "MovingPart";

    private AudioSource audioSource;
    private Dictionary<GameObject, CarPartSetup> partLookup = new Dictionary<GameObject, CarPartSetup>();
    private int totalParts;
    private int placedParts;
    private int currentRequiredOrder = 1;

    [System.Serializable]
    public class CarPartSetup
    {
        [Header("Part Info")]
        public string partName = "Part Name";
        public GameObject movablePart;
        public int placementOrder = 0;

        [Header("Target Position")]
        public Transform targetTransform;
        public Vector3 targetPosition;
        public Vector3 targetRotation;

        [Header("Label")]
        public Vector3 labelLocalPosition = Vector3.up * 0.7f;
        public Vector3 labelLocalScale = Vector3.one;
        public float labelFontSize = 2f;

        [HideInInspector] public GameObject ghost;
        [HideInInspector] public bool isPlaced;
        [HideInInspector] public bool isBeingDragged;
        [HideInInspector] public Vector3 originalPosition;
        [HideInInspector] public Quaternion originalRotation;
        [HideInInspector] public Material originalMaterial;
        [HideInInspector] public GameObject label;
    }

    private void Start()
    {
        if (partMover == null)
            partMover = FindObjectOfType<CarPartMover>();

        EnsureAudioSource();
        CreateGhostMaterial();
        TryIgnoreGhostCollisions();

        if (LevelManager.Instance == null)
        {
            InitializeAllParts();
            CreateAllGhosts();
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.mute = false;
        audioSource.volume = 1f;
        audioSource.spatialBlend = 0f;
        audioSource.priority = 0;
    }

    private void PlaySfx(AudioClip clip, string label)
    {
        EnsureAudioSource();
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, 1f);
    }

    private void TryIgnoreGhostCollisions()
    {
        int ghostLayer = LayerMask.NameToLayer(ghostLayerName);
        int movingLayer = LayerMask.NameToLayer(movingPartLayerName);

        if (ghostLayer >= 0 && movingLayer >= 0)
            Physics.IgnoreLayerCollision(movingLayer, ghostLayer, true);
    }

    public void InitializeFromLevelData(LevelData levelData)
    {
        ClearAllParts();

        ghostColor = levelData.ghostColor;
        highlightColor = levelData.highlightColor;
        correctPlacementColor = levelData.correctPlacementColor;
        snapSound = levelData.snapSound;
        wrongPlacementSound = levelData.wrongPlacementSound;
        completionSound = levelData.completionSound;
        requireRotationMatch = levelData.enableHandRotation;

        CreateGhostMaterial();
    }

    public void SetRotationSnapTolerance(float tolerance)
    {
        rotationSnapTolerance = Mathf.Clamp(tolerance, 5f, 45f);
    }

    public void RegisterPart(GameObject partObject, string partName, Vector3 targetPos, Vector3 targetRot,
                            int order, Vector3 labelOffset, float labelSize)
    {
        var setup = new CarPartSetup
        {
            partName = partName,
            movablePart = partObject,
            targetPosition = targetPos,
            targetRotation = targetRot,
            placementOrder = order,
            labelLocalPosition = labelOffset,
            labelFontSize = labelSize,
            originalPosition = partObject.transform.position,
            originalRotation = partObject.transform.rotation
        };

        Renderer renderer = partObject.GetComponent<Renderer>() ?? partObject.GetComponentInChildren<Renderer>();
        if (renderer != null)
            setup.originalMaterial = renderer.material;

        EnsurePartComponents(setup);
        carParts.Add(setup);
        partLookup[partObject] = setup;

        if (showLabels)
            CreateLabel(setup);

        totalParts++;
    }

    public void FinalizeSetup()
    {
        CreateAllGhosts();
        currentRequiredOrder = 1;
        placedParts = 0;
    }

    public void ClearAllParts()
    {
        foreach (var setup in carParts)
        {
            if (setup.ghost != null) Destroy(setup.ghost);
            if (setup.label != null) Destroy(setup.label);
        }

        carParts.Clear();
        partLookup.Clear();
        totalParts = 0;
        placedParts = 0;
        currentRequiredOrder = 1;
    }

    /// <summary>
    /// Get the target rotation Z for a part (used by GameDataTracker)
    /// </summary>
    public float GetPartTargetRotation(GameObject part)
    {
        if (part != null && partLookup.ContainsKey(part))
            return NormalizeAngle(partLookup[part].targetRotation.z);
        return 0f;
    }
    
    /// <summary>
    /// Get the target position for a part (used by GameDataTracker)
    /// </summary>
    public Vector3 GetPartTargetPosition(GameObject part)
    {
        if (part != null && partLookup.ContainsKey(part))
            return partLookup[part].targetPosition;
        return Vector3.zero;
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void CreateGhostMaterial()
    {
        if (ghostMaterial == null)
            ghostMaterial = new Material(Shader.Find("Standard")) { name = "GhostMaterial" };

        ghostMaterial.color = ghostColor;
        ghostMaterial.SetFloat("_Mode", 3);
        ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ghostMaterial.SetInt("_ZWrite", 0);
        ghostMaterial.DisableKeyword("_ALPHATEST_ON");
        ghostMaterial.EnableKeyword("_ALPHABLEND_ON");
        ghostMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        ghostMaterial.renderQueue = 3000;
    }

    private void InitializeAllParts()
    {
        totalParts = 0;

        foreach (var setup in carParts)
        {
            if (setup.movablePart == null) continue;

            totalParts++;
            setup.originalPosition = setup.movablePart.transform.position;
            setup.originalRotation = setup.movablePart.transform.rotation;

            if (setup.targetTransform != null)
            {
                setup.targetPosition = setup.targetTransform.position;
                setup.targetRotation = setup.targetTransform.eulerAngles;
            }

            Renderer renderer = setup.movablePart.GetComponent<Renderer>() ??
                              setup.movablePart.GetComponentInChildren<Renderer>();
            if (renderer != null)
                setup.originalMaterial = renderer.material;

            EnsurePartComponents(setup);
            partLookup[setup.movablePart] = setup;

            if (showLabels)
                CreateLabel(setup);
        }
    }

    private void EnsurePartComponents(CarPartSetup setup)
    {
        if (setup.movablePart.GetComponent<Collider>() == null)
            setup.movablePart.AddComponent<BoxCollider>();

        setup.movablePart.tag = "CarPart";

        Rigidbody rb = setup.movablePart.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = setup.movablePart.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    private void CreateLabel(CarPartSetup setup)
    {
        GameObject labelGO = new GameObject($"{setup.partName}_Label");
        labelGO.transform.SetParent(setup.movablePart.transform);

        TextMeshPro tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.text = setup.partName;
        tmp.fontSize = setup.labelFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineColor = Color.black;
        tmp.outlineWidth = 0.2f;

        labelGO.transform.localPosition = setup.labelLocalPosition;
        labelGO.transform.localScale = setup.labelLocalScale;
        labelGO.AddComponent<BillboardLabel>();

        setup.label = labelGO;
    }

    private void CreateAllGhosts()
    {
        int ghostLayer = LayerMask.NameToLayer(ghostLayerName);

        foreach (var setup in carParts)
        {
            if (setup.movablePart == null) continue;

            GameObject ghost = Instantiate(setup.movablePart);
            ghost.name = $"{setup.partName}_Ghost";
            ghost.transform.position = setup.targetPosition;
            ghost.transform.rotation = Quaternion.Euler(setup.targetRotation);

            ghost.tag = "Untagged";
            if (ghostLayer >= 0) SetLayerRecursive(ghost, ghostLayer);

            foreach (Renderer rend in ghost.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = new Material[rend.materials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = ghostMaterial;
                rend.materials = mats;
            }

            CleanupGhostComponents(ghost);

            if (pulseGhosts)
                ghost.AddComponent<GhostPulse>();

            setup.ghost = ghost;

            if (enforceOrder && setup.placementOrder > 0 && setup.placementOrder != currentRequiredOrder)
                UpdateGhostLockedState(setup, true);
        }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void CleanupGhostComponents(GameObject ghost)
    {
        foreach (Collider col in ghost.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        foreach (Rigidbody rb in ghost.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);

        foreach (MonoBehaviour script in ghost.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(script is GhostPulse))
                Destroy(script);

        foreach (TextMeshPro tmp in ghost.GetComponentsInChildren<TextMeshPro>(true))
            Destroy(tmp.gameObject);
    }

    public void SetSnapDistance(float distance) => snapDistance = Mathf.Clamp(distance, 0.1f, 15f);
    public float GetCurrentSnapDistance() => snapDistance;

    private void UpdateGhostLockedState(CarPartSetup setup, bool locked)
    {
        if (setup.ghost == null) return;

        foreach (Renderer rend in setup.ghost.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = new Material[rend.materials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = locked ? new Material(ghostMaterial) { color = lockedPartColor } : ghostMaterial;
            rend.materials = mats;
        }
    }

    private bool CanPlacePart(CarPartSetup setup)
    {
        if (!enforceOrder) return true;
        if (setup.placementOrder == 0) return true;
        return setup.placementOrder == currentRequiredOrder;
    }

    public bool CanPickupPart(GameObject part)
    {
        if (!partLookup.ContainsKey(part)) return true;
        return CanPlacePart(partLookup[part]);
    }

    public void OnPartPickup(GameObject part)
    {
        if (!partLookup.ContainsKey(part)) return;

        var setup = partLookup[part];
        if (!CanPlacePart(setup)) return;

        setup.isBeingDragged = true;
        HighlightPart(setup, highlightColor);

        if (setup.label != null)
        {
            TextMeshPro tmp = setup.label.GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                tmp.fontSize = setup.labelFontSize * 2;
                tmp.color = highlightColor;
            }
        }
    }

    public void OnPartDrag(GameObject part, Vector3 currentPosition)
    {
        if (part == null || !partLookup.ContainsKey(part)) return;

        var setup = partLookup[part];

        Vector2 currentPos2D = new Vector2(currentPosition.x, currentPosition.y);
        Vector2 targetPos2D = new Vector2(setup.targetPosition.x, setup.targetPosition.y);
        float distance = Vector2.Distance(currentPos2D, targetPos2D);

        bool rotationOk = true;
        if (requireRotationMatch)
        {
            float currentRotZ = NormalizeAngle(part.transform.rotation.eulerAngles.z);
            float targetRotZ = NormalizeAngle(setup.targetRotation.z);
            float rotationDiff = Mathf.Abs(NormalizeAngle(currentRotZ - targetRotZ));
            rotationOk = rotationDiff <= rotationSnapTolerance;
        }

        if (distance < snapDistance)
        {
            if (rotationOk)
            {
                HighlightPart(setup, correctPlacementColor);
                GhostPulse pulse = setup.ghost?.GetComponent<GhostPulse>();
                if (pulse != null) pulse.SetSpeed(3f);
            }
            else
            {
                HighlightPart(setup, rotationWrongColor);
            }
        }
        else
        {
            HighlightPart(setup, highlightColor);
            GhostPulse pulse = setup.ghost?.GetComponent<GhostPulse>();
            if (pulse != null) pulse.SetSpeed(1f);
        }
    }

    public bool OnPartRelease(GameObject part, Vector3 releasePosition)
    {
        if (part == null || !partLookup.ContainsKey(part)) return false;

        var setup = partLookup[part];
        setup.isBeingDragged = false;

        if (!CanPlacePart(setup))
        {
            StartCoroutine(ReturnToOriginal(setup));
            return false;
        }

        Vector2 releasePos2D = new Vector2(releasePosition.x, releasePosition.y);
        Vector2 targetPos2D = new Vector2(setup.targetPosition.x, setup.targetPosition.y);
        float distance = Vector2.Distance(releasePos2D, targetPos2D);

        bool rotationOk = true;
        if (requireRotationMatch)
        {
            float currentRotZ = NormalizeAngle(part.transform.rotation.eulerAngles.z);
            float targetRotZ = NormalizeAngle(setup.targetRotation.z);
            float rotationDiff = Mathf.Abs(NormalizeAngle(currentRotZ - targetRotZ));
            rotationOk = rotationDiff <= rotationSnapTolerance;
            
            Debug.Log($"[Rotation Check] Part: {currentRotZ:F1}° | Target: {targetRotZ:F1}° | " +
                     $"Diff: {rotationDiff:F1}° | Tolerance: {rotationSnapTolerance}° | OK: {rotationOk}");
        }

        if (distance < snapDistance && rotationOk && !setup.isPlaced)
        {
            StartCoroutine(SnapPartToPosition(setup));
            return true;
        }

        StartCoroutine(ReturnToOriginal(setup));
        return false;
    }

    private IEnumerator SnapPartToPosition(CarPartSetup setup)
    {
        setup.isPlaced = true;
        placedParts++;

        PlaySfx(snapSound, "Snap");

        float elapsed = 0;
        Vector3 startPos = setup.movablePart.transform.position;
        Quaternion startRot = setup.movablePart.transform.rotation;
        Quaternion targetRot = Quaternion.Euler(setup.targetRotation);

        while (elapsed < smoothSnapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / smoothSnapDuration);
            setup.movablePart.transform.position = Vector3.Lerp(startPos, setup.targetPosition, t);
            setup.movablePart.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }

        setup.movablePart.transform.position = setup.targetPosition;
        setup.movablePart.transform.rotation = targetRot;

        if (setup.ghost != null) setup.ghost.SetActive(false);
        if (setup.label != null) setup.label.SetActive(false);

        setup.movablePart.tag = "Placed";

        Rigidbody rb = setup.movablePart.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Collider col = setup.movablePart.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        HighlightPart(setup, correctPlacementColor);
        yield return new WaitForSeconds(0.5f);
        RestoreMaterial(setup);

        if (enforceOrder && setup.placementOrder > 0)
        {
            currentRequiredOrder++;
            UnlockNextPart();
        }

        CheckCompletion();
    }

    private void UnlockNextPart()
    {
        foreach (var setup in carParts)
        {
            if (!setup.isPlaced && setup.placementOrder == currentRequiredOrder)
                UpdateGhostLockedState(setup, false);
        }
    }

    private IEnumerator ReturnToOriginal(CarPartSetup setup)
    {
        PlaySfx(wrongPlacementSound, "Wrong");

        HighlightPart(setup, wrongPlacementColor);
        yield return new WaitForSeconds(0.5f);

        float elapsed = 0;
        Vector3 startPos = setup.movablePart.transform.position;
        Quaternion startRot = setup.movablePart.transform.rotation;

        while (elapsed < smoothSnapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / smoothSnapDuration);
            setup.movablePart.transform.position = Vector3.Lerp(startPos, setup.originalPosition, t);
            setup.movablePart.transform.rotation = Quaternion.Lerp(startRot, setup.originalRotation, t);
            yield return null;
        }

        setup.movablePart.transform.position = setup.originalPosition;
        setup.movablePart.transform.rotation = setup.originalRotation;
        RestoreMaterial(setup);

        if (setup.label != null)
        {
            TextMeshPro tmp = setup.label.GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                tmp.fontSize = setup.labelFontSize;
                tmp.color = Color.white;
            }
        }
    }

    private void HighlightPart(CarPartSetup setup, Color color)
    {
        Renderer renderer = setup.movablePart.GetComponent<Renderer>() ??
                          setup.movablePart.GetComponentInChildren<Renderer>();

        if (renderer != null)
        {
            Material highlightMat = new Material(setup.originalMaterial) { color = color };
            renderer.material = highlightMat;
        }
    }

    private void RestoreMaterial(CarPartSetup setup)
    {
        Renderer renderer = setup.movablePart.GetComponent<Renderer>() ??
                          setup.movablePart.GetComponentInChildren<Renderer>();

        if (renderer != null && setup.originalMaterial != null)
            renderer.material = setup.originalMaterial;
    }

    private void CheckCompletion()
    {
        if (placedParts >= totalParts)
        {
            Debug.Log("🎉 CAR ASSEMBLY COMPLETE!");
            StartCoroutine(OnAssemblyComplete());
        }
    }

    private IEnumerator OnAssemblyComplete()
    {
        PlaySfx(completionSound, "Completion");
        onGameComplete?.Invoke();

        yield return new WaitForSeconds(endGameDelay);

        if (completionScreenObject != null)
            completionScreenObject.SendMessage("ShowCompletionScreen", SendMessageOptions.DontRequireReceiver);
        else if (endGameOnCompletion)
            EndGame();
    }

    private void EndGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ResetAssembly()
    {
        placedParts = 0;
        currentRequiredOrder = 1;

        foreach (var setup in carParts)
        {
            setup.isPlaced = false;
            setup.movablePart.transform.position = setup.originalPosition;
            setup.movablePart.transform.rotation = setup.originalRotation;
            setup.movablePart.tag = "CarPart";

            if (setup.ghost != null) setup.ghost.SetActive(true);
            if (setup.label != null) setup.label.SetActive(true);

            Rigidbody rb = setup.movablePart.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.None;
            }

            Collider col = setup.movablePart.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;

            RestoreMaterial(setup);
            UpdateGhostLockedState(setup, enforceOrder && setup.placementOrder > 0 && setup.placementOrder != currentRequiredOrder);
        }

        UnlockNextPart();
    }
}

// GhostPulse component
public class GhostPulse : MonoBehaviour
{
    private Material material;
    private float baseAlpha;
    private float pulseSpeed = 1f;

    private void Start()
    {
        Renderer renderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            baseAlpha = material.color.a;
        }
    }

    private void Update()
    {
        if (material != null)
        {
            Color color = material.color;
            color.a = baseAlpha + Mathf.Sin(Time.time * pulseSpeed) * 0.1f;
            material.color = color;
        }
    }

    public void SetSpeed(float speed) => pulseSpeed = speed;
}

// BillboardLabel component
public class BillboardLabel : MonoBehaviour
{
    private Camera cam;
    private void Start() => cam = Camera.main;

    private void LateUpdate()
    {
        if (cam != null)
            transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward, cam.transform.rotation * Vector3.up);
    }
}