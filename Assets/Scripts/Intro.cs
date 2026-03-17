using UnityEngine;
using System.Collections;

/// <summary>
/// Multi-page intro tutorial explaining game controls and objectives.
/// </summary>
public class CarAssemblyIntro : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showIntro = true;
    
    [Header("References")]
    [SerializeField] private CarPartMover partMover;
    
    private int currentPage;
    private const int TOTAL_PAGES = 4;
    private bool introComplete;
    
    private GUIStyle titleStyle, bodyStyle;
    
    private void Start()
    {
        if (partMover == null)
            partMover = FindObjectOfType<CarPartMover>();
        
        if (showIntro)
        {
            Time.timeScale = 0f;
            if (partMover != null) partMover.enabled = false;
        }
        else
        {
            StartGame();
        }
        
        SetupStyles();
    }
    
    private void SetupStyles()
    {
        titleStyle = new GUIStyle
        {
            fontSize = 36, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, wordWrap = true
        };
        titleStyle.normal.textColor = Color.white;
        
        bodyStyle = new GUIStyle
        {
            fontSize = 20, alignment = TextAnchor.MiddleCenter,
            wordWrap = true, padding = new RectOffset(20, 20, 10, 10)
        };
        bodyStyle.normal.textColor = Color.white;
    }
    
    private void Update()
    {
        if (!introComplete && Input.GetKeyDown(KeyCode.Space))
            StartGame();
    }
    
    private void OnGUI()
    {
        if (!showIntro || introComplete) return;
        
        // Background
        GUI.color = new Color(0, 0, 0, 0.95f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        
        float boxWidth = 800, boxHeight = 550;
        float boxX = (Screen.width - boxWidth) / 2;
        float boxY = (Screen.height - boxHeight) / 2;
        
        // Box
        GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), "");
        
        // Header
        GUI.color = new Color(0.2f, 0.4f, 0.6f, 1f);
        GUI.Box(new Rect(boxX, boxY, boxWidth, 80), "");
        
        GUI.color = Color.white;
        
        // Content
        string[] titles = { "CAR ASSEMBLY SIMULATOR", "HAND CONTROLS", "HOW TO PLAY", "TIPS FOR SUCCESS" };
        string[] content = {
            "Welcome to the Interactive Car Assembly Experience!\n\n" +
            "Use hand tracking to assemble a complete car by picking up\n" +
            "and placing parts in their correct positions.\n\n" +
            "Make sure your hand is clearly visible to the camera\n" +
            "and you have good lighting.",
            
            "HOW TO INTERACT:\n\n" +
            "✋ Show your hand - A red sphere tracks your movement\n\n" +
            "👌 PINCH to grab - Bring thumb and index finger together\n\n" +
            "🚀 MOVE while pinching to drag parts\n\n" +
            "👐 RELEASE pinch to drop - Parts snap if close enough",
            
            "ASSEMBLY PROCESS:\n\n" +
            "🔍 LOCATE - Each part has a label and ghost position\n\n" +
            "✊ GRAB - Move hand near part and pinch\n\n" +
            "📍 POSITION - Drag to ghost, highlights GREEN when close\n\n" +
            "✅ SNAP - Release when green to lock in place",
            
            "PRO TIPS:\n\n" +
            "👻 Follow the pulsing ghost positions\n\n" +
            "🎨 YELLOW = selected, GREEN = ready to snap, RED = too far\n\n" +
            "💡 Ensure good lighting and keep hand visible\n\n" +
            "Ready to build your car? Let's go!"
        };
        
        GUI.Label(new Rect(boxX, boxY + 20, boxWidth, 60), titles[currentPage], titleStyle);
        GUI.Label(new Rect(boxX + 50, boxY + 120, boxWidth - 100, boxHeight - 200), content[currentPage], bodyStyle);
        
        DrawNavigationButtons(boxX, boxY, boxWidth, boxHeight);
    }
    
    private void DrawNavigationButtons(float x, float y, float w, float h)
    {
        GUIStyle btnStyle = new GUIStyle("button") { fontSize = 24, fontStyle = FontStyle.Bold };
        btnStyle.normal.textColor = Color.white;
        
        float buttonWidth = 150, buttonHeight = 50;
        float buttonY = y + h - 80;
        
        // Back button
        if (currentPage > 0)
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            if (GUI.Button(new Rect(x + 50, buttonY, buttonWidth, buttonHeight), "◀ BACK", btnStyle))
                currentPage--;
        }
        
        // Next/Start button
        bool isLastPage = currentPage == TOTAL_PAGES - 1;
        GUI.color = isLastPage ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.3f, 0.6f, 1f);
        float finalButtonWidth = isLastPage ? 200 : buttonWidth;
        
        if (GUI.Button(new Rect(x + w - finalButtonWidth - 50, buttonY, finalButtonWidth, buttonHeight), 
                      isLastPage ? "START BUILDING" : "NEXT ▶", btnStyle))
        {
            if (isLastPage) StartGame();
            else currentPage++;
        }
        
        // Page indicator
        GUI.color = Color.white;
        GUIStyle pageStyle = new GUIStyle { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        pageStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x, buttonY, w, buttonHeight), $"Page {currentPage + 1} / {TOTAL_PAGES}", pageStyle);
        
        // Skip button
        GUI.color = new Color(0.5f, 0.5f, 0.5f);
        GUIStyle skipStyle = new GUIStyle(btnStyle) { fontSize = 16 };
        if (GUI.Button(new Rect(x + w - 100, y + 10, 80, 30), "Skip", skipStyle))
            StartGame();
    }
    
    private void StartGame()
    {
        introComplete = true;
        showIntro = false;
        Time.timeScale = 1f;
        
        if (partMover != null)
            partMover.enabled = true;
        
        StartCoroutine(ShowStartMessage());
    }
    
    private IEnumerator ShowStartMessage()
    {
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Show your hand to the camera to begin assembly!");
    }
    
    public bool IsIntroComplete() => introComplete;
}