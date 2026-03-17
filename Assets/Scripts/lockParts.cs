using UnityEngine;

/// <summary>
/// Locks a part at a specific Z position, preventing drift.
/// Add this component to parts that should stay at a fixed Z depth.
/// </summary>
public class LockZPosition : MonoBehaviour
{
    private float lockedZ;
    private bool isLocked = true;
    private CarPartMover partMover;
    
    private void Start()
    {
        lockedZ = transform.position.z;
        partMover = FindObjectOfType<CarPartMover>();
    }
    
    private void LateUpdate()
    {
        // Don't lock Z if this part is currently being dragged
        if (partMover != null && partMover.IsDragging && partMover.CurrentPart == gameObject)
            return;
        
        // Don't lock Z if part is placed
        if (gameObject.CompareTag("Placed"))
        {
            Destroy(this);
            return;
        }
        
        // Check if part is being snapped
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic && !gameObject.CompareTag("CarPart"))
        {
            Destroy(this);
            return;
        }
        
        // Lock the Z position
        if (isLocked)
        {
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.z - lockedZ) > 0.01f)
            {
                pos.z = lockedZ;
                transform.position = pos;
            }
        }
    }
    
    public void SetLockedZ(float z) => lockedZ = z;
    public void SetLocked(bool locked) => isLocked = locked;
}