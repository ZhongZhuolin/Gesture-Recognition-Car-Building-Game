using UnityEngine;

/// <summary>
/// Shows completion screen with time, motivational message, and navigation buttons.
/// </summary>
public class CompletionScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CarAssemblyManager assemblyManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private CarCompletionBar completionBar;

    [Header("Motivational Messages")]
    [SerializeField] private string[] motivationalMessages = new string[]
    {
        "Great job completing this challenge!",
        "Your skills are improving with every level!",
        "Excellent work - keep up the momentum!",
        "You're making fantastic progress!",
        "Well done - practice makes perfect!",
        "Awesome assembly skills on display!",
        "Your coordination is getting better!",
        "Nice work - you nailed it!",
        "Keep going - you're doing great!",
        "Brilliant effort - onwards and upwards!",
        "Your hard work is paying off!",
        "Steady hands lead to success!",
        "Another level conquered!",
        "You're becoming an assembly expert!",
        "Great focus and determination!"
    };

    private bool showCompletionScreen;
    private float completionTime;
    private string selectedMessage;
    private float startTime;

    private GUIStyle titleStyle, timeStyle, messageStyle, levelInfoStyle;

    private void Start()
    {
        CacheReferences();
        SetupStyles();
        StartGame();
    }

    private void CacheReferences()
    {
        if (assemblyManager == null) assemblyManager = FindObjectOfType<CarAssemblyManager>();
        if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
        if (completionBar == null) completionBar = FindObjectOfType<CarCompletionBar>();
    }

    private void SetupStyles()
    {
        titleStyle = new GUIStyle
        {
            fontSize = 48, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, wordWrap = true
        };
        titleStyle.normal.textColor = Color.white;

        timeStyle = new GUIStyle
        {
            fontSize = 48, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        timeStyle.normal.textColor = Color.white;

        messageStyle = new GUIStyle
        {
            fontSize = 26, fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter, wordWrap = true,
            padding = new RectOffset(40, 40, 10, 10)
        };
        messageStyle.normal.textColor = Color.white;

        levelInfoStyle = new GUIStyle
        {
            fontSize = 28, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        levelInfoStyle.normal.textColor = new Color(0.8f, 1f, 0.8f);
    }

    public void StartGame()
    {
        startTime = Time.time;
        showCompletionScreen = false;
    }

    public void ShowCompletionScreen()
    {
        completionTime = Time.time - startTime;
        selectedMessage = motivationalMessages[Random.Range(0, motivationalMessages.Length)];
        showCompletionScreen = true;

        if (levelManager != null)
        {
            levelManager.SaveProgress();
            int levelIndex = levelManager.CurrentLevelNumber - 1;
            completionBar?.OnLevelComplete(levelIndex, completionTime);
        }
    }

    private void OnGUI()
    {
        if (!showCompletionScreen) return;

        GUI.color = new Color(0, 0, 0, 0.95f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float boxWidth = 800, boxHeight = 650;
        float boxX = (Screen.width - boxWidth) / 2;
        float boxY = (Screen.height - boxHeight) / 2;

        GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), "");

        GUI.color = new Color(0.2f, 0.5f, 0.3f, 1f);
        GUI.Box(new Rect(boxX, boxY, boxWidth, 80), "");

        GUI.color = Color.white;
        GUI.Label(new Rect(boxX, boxY + 20, boxWidth, 60), "ASSEMBLY COMPLETE!", titleStyle);

        float timeY = boxY + 150;
        if (levelManager != null && levelManager.CurrentLevel != null)
        {
            string levelInfo = $"{levelManager.CurrentLevel.levelName}\n({levelManager.CurrentLevelNumber}/{levelManager.TotalLevels})";
            GUI.Label(new Rect(boxX, boxY + 100, boxWidth, 60), levelInfo, levelInfoStyle);
            timeY = boxY + 200;
        }

        int minutes = Mathf.FloorToInt(completionTime / 60f);
        int seconds = Mathf.FloorToInt(completionTime % 60f);
        GUI.Label(new Rect(boxX, timeY, boxWidth, 100), $"Time: {minutes}:{seconds:00}", timeStyle);

        GUI.Label(new Rect(boxX + 50, timeY + 120, boxWidth - 100, 100), selectedMessage, messageStyle);

        DrawButtons(boxX, boxY, boxWidth, boxHeight);
    }

    private void DrawButtons(float x, float y, float w, float h)
    {
        GUIStyle btnStyle = new GUIStyle("button") { fontSize = 28, fontStyle = FontStyle.Bold };
        btnStyle.normal.textColor = Color.white;

        float buttonWidth = 200, buttonHeight = 60;
        float buttonY = y + h - 100;
        float spacing = 40;

        GUI.color = new Color(0.3f, 0.7f, 1f);
        if (GUI.Button(new Rect(x + (w / 2) - buttonWidth - (spacing / 2), buttonY, buttonWidth, buttonHeight), "RETRY", btnStyle))
            OnRetryClicked();

        bool hasNext = levelManager != null && levelManager.HasNextLevel;
        GUI.color = hasNext ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.4f, 0.4f, 0.4f);
        if (GUI.Button(new Rect(x + (w / 2) + (spacing / 2), buttonY, buttonWidth, buttonHeight), hasNext ? "NEXT LEVEL" : "COMPLETE", btnStyle))
            if (hasNext) OnNextLevelClicked();

        GUI.color = Color.white;
    }

    private void OnRetryClicked()
    {
        showCompletionScreen = false;

        if (levelManager != null)
            levelManager.RestartCurrentLevel();
        else
            assemblyManager?.ResetAssembly();

        StartGame();
    }

    private void OnNextLevelClicked()
    {
        showCompletionScreen = false;
        levelManager?.LoadNextLevel();
        StartGame();
    }
}