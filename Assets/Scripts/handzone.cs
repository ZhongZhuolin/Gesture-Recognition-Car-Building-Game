using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Defines a screen zone where the player must return their hand after placing a part.
/// Shows green when ready to pick up, yellow when must return.
/// </summary>
public class HandZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private Vector2 zoneScreenPosition = new Vector2(50, 20);
    [SerializeField] private Vector2 zoneSize = new Vector2(100, 80);
    [SerializeField] private Color zoneActiveColor = new Color(0.2f, 1f, 0.2f, 0.8f);
    [SerializeField] private Color zoneInactiveColor = new Color(1f, 1f, 0.2f, 0.8f);
    
    private Canvas canvas;
    private GameObject zonePanel;
    private Image zonePanelImage;
    private bool canPickupPart;
    
    private GameObject handSphere;
    private Camera mainCam;
    
    private void Start()
    {
        handSphere = GameObject.Find("HandIndicator");
        mainCam = Camera.main;
        CreateZoneUI();
    }
    
    private void CreateZoneUI()
    {
        GameObject canvasGO = new GameObject("HandZoneCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9998;
        canvasGO.AddComponent<GraphicRaycaster>();
        
        zonePanel = new GameObject("ZonePanel");
        zonePanel.transform.SetParent(canvas.transform, false);
        
        RectTransform panelRect = zonePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = zoneScreenPosition;
        panelRect.sizeDelta = zoneSize;
        
        zonePanelImage = zonePanel.AddComponent<Image>();
        zonePanelImage.color = zoneInactiveColor;
        
        Outline outline = zonePanel.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(3, -3);
    }
    
    private void Update()
    {
        CheckHandInZone();
        zonePanelImage.color = canPickupPart ? zoneActiveColor : zoneInactiveColor;
    }
    
    private void CheckHandInZone()
    {
        if (handSphere == null)
        {
            handSphere = GameObject.Find("HandIndicator");
            return;
        }
        
        if (mainCam == null) return;
        
        Vector3 handScreenPos = mainCam.WorldToScreenPoint(handSphere.transform.position);
        
        bool inZone = handScreenPos.x >= zoneScreenPosition.x && 
                      handScreenPos.x <= zoneScreenPosition.x + zoneSize.x &&
                      handScreenPos.y >= zoneScreenPosition.y && 
                      handScreenPos.y <= zoneScreenPosition.y + zoneSize.y;
        
        if (inZone && !canPickupPart)
            canPickupPart = true;
    }
    
    public void OnPartPlacedSuccessfully() => canPickupPart = false;
    public bool CanPickupPart() => canPickupPart;
    public void ResetZone() => canPickupPart = false;
    
    public void SetZonePosition(Vector2 position)
    {
        zoneScreenPosition = position;
        if (zonePanel != null)
            zonePanel.GetComponent<RectTransform>().anchoredPosition = position;
    }
    
    public void SetZoneSize(Vector2 size)
    {
        zoneSize = size;
        if (zonePanel != null)
            zonePanel.GetComponent<RectTransform>().sizeDelta = size;
    }
    
    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}