using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Dropdown bar showing completed cars with render texture previews.
/// FIXED: Slot indexing and time formatting only.
/// </summary>
public class CarCompletionBar : MonoBehaviour
{
    [Header("Car Prefabs (Assign 5 Cars)")]
    [SerializeField] private List<CarSlotConfig> carConfigs = new List<CarSlotConfig>();
    
    [System.Serializable]
    public class CarSlotConfig
    {
        public GameObject carPrefab;
        [Range(0f, 360f)] public float rotationY = 45f;
        [Range(-45f, 45f)] public float rotationX = 0f;
        [Range(-180f, 180f)] public float rotationZ = 0f;
        [Range(0.1f, 5f)] public float scale = 2.5f;  // Changed from 0.5f to 0.1f minimum
    }
    
    [Header("Colors")]
    [SerializeField] private Color barBackgroundColor = new Color(0.1f, 0.15f, 0.2f, 0.95f);
    [SerializeField] private Color slotBackgroundColor = new Color(0.05f, 0.08f, 0.12f, 1f);
    [SerializeField] private Color ghostColor = new Color(0.5f, 0.8f, 1f, 0.4f);
    [SerializeField] private Color slotCompletedColor = new Color(0.1f, 0.5f, 0.2f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.55f, 0.85f, 1f);
    
    [Header("Dimensions")]
    [SerializeField] private float collapsedHeight = 60f;
    [SerializeField] private float expandedHeight = 300f;
    [SerializeField] private float slotWidth = 220f;
    [SerializeField] private float animationSpeed = 8f;
    
    [Header("Render Settings")]
    [SerializeField] private int renderTextureSize = 512;
    [SerializeField] private float cameraDistance = 8f;
    
    private GameObject barPanel;
    private GameObject contentPanel;
    private Canvas dedicatedCanvas;
    private List<MiniCarSlot> carSlots = new List<MiniCarSlot>();
    private Camera previewCamera;
    private GameObject previewCameraObject;
    private GameObject previewLightObject;
    private bool isExpanded;
    private float currentHeight;
    private TextMeshProUGUI buttonText;
    
    private class MiniCarSlot
    {
        public GameObject slotObject;
        public RawImage carImage;
        public GameObject ghostInstance;
        public GameObject completedInstance;
        public RenderTexture renderTexture;
        public TextMeshProUGUI timeText;
        public Image slotBackground;
        public bool isCompleted;
        public int slotIndex;
    }
    
    private void Start()
    {
        currentHeight = collapsedHeight;
        SetupPreviewCamera();
        CreateDedicatedCanvas();
        CreateDropdownBar();
        CreateAllCarSlots();
        
        if (contentPanel != null)
            contentPanel.SetActive(false);
    }
    
    private void Update()
    {
        float targetHeight = isExpanded ? expandedHeight : collapsedHeight;
        if (Mathf.Abs(currentHeight - targetHeight) > 0.1f)
        {
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * animationSpeed);
            barPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, currentHeight);
        }
        
        if (contentPanel != null)
            contentPanel.SetActive(currentHeight > collapsedHeight + 10f);
    }
    
    private void SetupPreviewCamera()
    {
        previewCameraObject = new GameObject("PreviewCam");
        previewCamera = previewCameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = slotBackgroundColor;
        previewCamera.cullingMask = 1 << 31;
        previewCamera.orthographic = true;
        previewCamera.orthographicSize = 0.8f;  // Reduced from 1.2f to 0.8f for tighter view
        previewCamera.enabled = false;
        previewCameraObject.transform.position = new Vector3(5000, 5000, 5000);
        previewCameraObject.transform.rotation = Quaternion.Euler(20, -30, 0);
        
        previewLightObject = new GameObject("PreviewLight");
        previewLightObject.transform.position = previewCameraObject.transform.position + Vector3.up * 5;
        Light lightComp = previewLightObject.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.0f;
        lightComp.transform.rotation = Quaternion.Euler(50, -30, 0);
        lightComp.cullingMask = 1 << 31;
    }
    
    private void CreateDedicatedCanvas()
    {
        GameObject canvasGO = new GameObject("CompletionBarCanvas");
        dedicatedCanvas = canvasGO.AddComponent<Canvas>();
        dedicatedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dedicatedCanvas.sortingOrder = 10000;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
    }
    
    private void CreateDropdownBar()
    {
        barPanel = new GameObject("CarBar");
        barPanel.transform.SetParent(dedicatedCanvas.transform, false);
        
        RectTransform barRect = barPanel.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0, collapsedHeight);
        
        barPanel.AddComponent<Image>().color = barBackgroundColor;
        
        // Button
        GameObject buttonPanel = new GameObject("ButtonPanel");
        buttonPanel.transform.SetParent(barPanel.transform, false);
        
        RectTransform buttonRect = buttonPanel.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0, 1);
        buttonRect.anchorMax = new Vector2(1, 1);
        buttonRect.pivot = new Vector2(0.5f, 1);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(0, collapsedHeight);
        
        Button button = buttonPanel.AddComponent<Button>();
        buttonPanel.AddComponent<Image>().color = buttonColor;
        button.onClick.AddListener(ToggleDropdown);
        
        // Button text
        GameObject textGO = new GameObject("ButtonText");
        textGO.transform.SetParent(buttonPanel.transform, false);
        
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        
        buttonText = textGO.AddComponent<TextMeshProUGUI>();
        buttonText.text = $"▼ COMPLETED CARS (0/{carConfigs.Count})";
        buttonText.fontSize = 26;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;
        
        // Content panel
        contentPanel = new GameObject("ContentPanel");
        contentPanel.transform.SetParent(barPanel.transform, false);
        
        RectTransform contentRect = contentPanel.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(0, 0);
        contentRect.offsetMax = new Vector2(0, -collapsedHeight);
        
        HorizontalLayoutGroup layout = contentPanel.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 15;
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
    }
    
    private void ToggleDropdown()
    {
        isExpanded = !isExpanded;
        UpdateButtonText();
    }
    
    private void UpdateButtonText()
    {
        int count = 0;
        foreach (var slot in carSlots)
            if (slot.isCompleted) count++;
        
        buttonText.text = $"{(isExpanded ? "▲" : "▼")} COMPLETED CARS ({count}/{carConfigs.Count})";
    }
    
    private void CreateAllCarSlots()
    {
        for (int i = 0; i < 5; i++)
            CreateCarSlot(i);
    }
    
    private void CreateCarSlot(int slotIndex)
    {
        MiniCarSlot slot = new MiniCarSlot();
        slot.slotIndex = slotIndex;
        
        slot.slotObject = new GameObject($"Slot{slotIndex + 1}");
        slot.slotObject.transform.SetParent(contentPanel.transform, false);
        
        LayoutElement layoutElement = slot.slotObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = slotWidth;
        layoutElement.preferredHeight = expandedHeight - collapsedHeight - 20;
        layoutElement.flexibleWidth = 1;
        
        slot.slotBackground = slot.slotObject.AddComponent<Image>();
        slot.slotBackground.color = slotBackgroundColor;
        
        // Car display
        GameObject carDisplay = new GameObject("CarDisplay");
        carDisplay.transform.SetParent(slot.slotObject.transform, false);
        
        RectTransform displayRect = carDisplay.AddComponent<RectTransform>();
        displayRect.anchorMin = Vector2.zero;
        displayRect.anchorMax = Vector2.one;
        displayRect.offsetMin = new Vector2(8, 35);
        displayRect.offsetMax = new Vector2(-8, -8);
        
        slot.renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16) { antiAliasing = 4 };
        slot.carImage = carDisplay.AddComponent<RawImage>();
        slot.carImage.texture = slot.renderTexture;
        
        carDisplay.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        
        // Time text
        GameObject timeGO = new GameObject("TimeText");
        timeGO.transform.SetParent(slot.slotObject.transform, false);
        
        RectTransform timeRect = timeGO.AddComponent<RectTransform>();
        timeRect.anchorMin = new Vector2(0, 0);
        timeRect.anchorMax = new Vector2(1, 0);
        timeRect.anchoredPosition = new Vector2(0, 12);
        timeRect.sizeDelta = new Vector2(0, 30);
        
        slot.timeText = timeGO.AddComponent<TextMeshProUGUI>();
        slot.timeText.text = $"LEVEL {slotIndex + 1}";
        slot.timeText.fontSize = 16;
        slot.timeText.fontStyle = FontStyles.Bold;
        slot.timeText.alignment = TextAlignmentOptions.Center;
        slot.timeText.color = Color.white;
        
        carSlots.Add(slot);
        
        if (slotIndex < carConfigs.Count && carConfigs[slotIndex].carPrefab != null)
            CreateCarModels(slot, slotIndex);
    }
    
    private void CreateCarModels(MiniCarSlot slot, int slotIndex)
    {
        CarSlotConfig config = carConfigs[slotIndex];
        Vector3 basePos = previewCameraObject.transform.position + previewCameraObject.transform.forward * cameraDistance + Vector3.right * slotIndex * 10f;
        Quaternion rotation = Quaternion.Euler(config.rotationX, config.rotationY, config.rotationZ);
        
        slot.ghostInstance = Instantiate(config.carPrefab, basePos, rotation);
        slot.ghostInstance.name = $"Ghost_Level{slotIndex + 1}";
        slot.ghostInstance.transform.localScale = Vector3.one * config.scale;
        SetupCarModel(slot.ghostInstance, true);
        
        slot.completedInstance = Instantiate(config.carPrefab, basePos, rotation);
        slot.completedInstance.name = $"Complete_Level{slotIndex + 1}";
        slot.completedInstance.transform.localScale = Vector3.one * config.scale;
        SetupCarModel(slot.completedInstance, false);
        slot.completedInstance.SetActive(false);
        
        RenderCar(slot, slot.ghostInstance);
    }
    
    private void SetupCarModel(GameObject car, bool isGhost)
    {
        SetLayer(car, 31);
        
        foreach (MonoBehaviour script in car.GetComponentsInChildren<MonoBehaviour>())
            Destroy(script);
        foreach (Collider col in car.GetComponentsInChildren<Collider>())
            Destroy(col);
        foreach (Rigidbody rb in car.GetComponentsInChildren<Rigidbody>())
            Destroy(rb);
        
        if (isGhost)
        {
            foreach (Renderer rend in car.GetComponentsInChildren<Renderer>())
            {
                foreach (Material mat in rend.materials)
                {
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = 3000;
                    mat.color = ghostColor;
                }
            }
        }
    }
    
    private void SetLayer(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayer(child.gameObject, layer);
    }
    
    private void RenderCar(MiniCarSlot slot, GameObject car)
    {
        if (car == null || !car.activeInHierarchy) return;
        
        previewCamera.targetTexture = slot.renderTexture;
        previewCamera.transform.LookAt(car.transform.position);
        previewCamera.Render();
        previewCamera.targetTexture = null;
    }
    
    private string FormatTime(float timeInSeconds)
    {
        int totalSeconds = Mathf.FloorToInt(timeInSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }
    
    public void OnLevelComplete(int levelIndex, float completionTime)
    {
        if (levelIndex < 0 || levelIndex >= carSlots.Count)
        {
            Debug.LogWarning($"[CarCompletionBar] Invalid levelIndex {levelIndex}! Must be 0-{carSlots.Count - 1}");
            return;
        }
        
        MiniCarSlot slot = carSlots[levelIndex];
        
        if (slot.slotIndex != levelIndex)
        {
            Debug.LogError($"[CarCompletionBar] Slot mismatch! levelIndex={levelIndex}, slot.slotIndex={slot.slotIndex}");
            return;
        }
        
        if (slot.isCompleted)
        {
            slot.timeText.text = FormatTime(completionTime);
            return;
        }
        
        slot.isCompleted = true;
        slot.slotBackground.color = slotCompletedColor;
        
        if (slot.ghostInstance != null) slot.ghostInstance.SetActive(false);
        if (slot.completedInstance != null) slot.completedInstance.SetActive(true);
        
        RenderCar(slot, slot.completedInstance);
        
        slot.timeText.text = FormatTime(completionTime);
        slot.timeText.color = new Color(0.3f, 1f, 0.3f, 1f);
        
        UpdateButtonText();
    }
    
    public void SetVisible(bool visible)
    {
        if (barPanel != null)
            barPanel.SetActive(visible);
    }
    
    private void OnDestroy()
    {
        foreach (var slot in carSlots)
        {
            if (slot.renderTexture != null) slot.renderTexture.Release();
            if (slot.ghostInstance != null) Destroy(slot.ghostInstance);
            if (slot.completedInstance != null) Destroy(slot.completedInstance);
        }
        
        if (dedicatedCanvas != null)
            Destroy(dedicatedCanvas.gameObject);
        
        if (previewCameraObject != null)
            Destroy(previewCameraObject);
        
        if (previewLightObject != null)
            Destroy(previewLightObject);
    }
}