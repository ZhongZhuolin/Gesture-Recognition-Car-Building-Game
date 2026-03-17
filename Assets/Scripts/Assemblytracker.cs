using UnityEngine;

/// <summary>
/// Bridge component between CarAssemblyManager and GameDataTracker.
/// Add this component to the same GameObject as GameDataTracker.
/// </summary>
public class AssemblyTrackingIntegrator : MonoBehaviour
{
    private GameDataTracker dataTracker;

    private void Awake()
    {
        dataTracker = GetComponent<GameDataTracker>();

        if (dataTracker == null)
            Debug.LogError("[AssemblyTrackingIntegrator] GameDataTracker not found on this GameObject!");
    }

    /// <summary>
    /// Call this when a part is placed (correctly or incorrectly).
    /// </summary>
    public void OnPartPlaced(string partName, bool wasCorrect)
    {
        if (dataTracker == null) return;
        dataTracker.RecordPartPlacement(partName, wasCorrect);
    }
}