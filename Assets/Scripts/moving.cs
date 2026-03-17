using UnityEngine;

/// <summary>
/// Handles movement patterns for car parts with collision and ghost bouncing.
/// Maze code removed - clean implementation.
/// </summary>
public class MovingCarPart : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private MovementPattern pattern = MovementPattern.Bounce;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float radius = 5f;
    
    [Header("Bounce Settings")]
    [SerializeField] private float screenPadding = 150f;
    [SerializeField] private bool useWorldBounds = false;
    [SerializeField] private Vector2 worldBoundsMin = new Vector2(-10, -5);
    [SerializeField] private Vector2 worldBoundsMax = new Vector2(10, 5);
    [SerializeField] private Vector3 carAvoidanceCenter = Vector3.zero;
    [SerializeField] private float carAvoidanceRadius = 3f;
    
    [Header("Physics Settings")]
    [SerializeField] private float bounceDamping = 0.95f;
    [SerializeField] private float minSpeed = 1.5f;
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float partRepulsionForce = 0.3f;
    [SerializeField] private float partRepulsionDistance = 1.5f;
    
    [Header("Collision Settings")]
    [SerializeField] private bool enablePartCollision = true;
    [SerializeField] private bool bounceOffGhosts = false;
    [SerializeField] private float ghostBounceDistance = 1.0f;
    
    [Header("Advanced")]
    [SerializeField] private bool pauseWhenGrabbed = true;
    
    public enum MovementPattern { Bounce, Circle, Horizontal, Vertical }
    
    private Vector3 _velocity;
    public Vector3 velocity { get => _velocity; set => _velocity = value; }
    
    private bool isGrabbed;
    private bool isFrozen;
    private bool isPermanentlyFrozen;
    
    private CarPartMover partMover;
    private Camera mainCamera;
    private Rigidbody rb;
    
    private float angle;
    private Vector3 startPosition;
    
    private static MovingCarPart[] allPartsCache;
    private static GameObject[] allGhostsCache;
    private static float lastCacheTime;
    private const float CACHE_REFRESH_INTERVAL = 1f;
    
    private void Start()
    {
        mainCamera = Camera.main;
        partMover = FindObjectOfType<CarPartMover>();
        rb = GetComponent<Rigidbody>();
        carAvoidanceCenter = Vector3.zero;
        
        ScatterPartAwayFromCenter();
        EnsureMinimumDistanceFromOtherParts();
        
        if (pattern == MovementPattern.Bounce)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            _velocity = new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0) * moveSpeed;
        }
        
        startPosition = transform.position;
        
        if (bounceOffGhosts)
            RefreshGhostsCache();
    }
    
    private void ScatterPartAwayFromCenter()
    {
        Vector3 currentPos = transform.position;
        Vector3 directionFromCenter = (currentPos - carAvoidanceCenter).normalized;
        
        if (directionFromCenter.magnitude < 0.1f)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            directionFromCenter = new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0);
        }
        
        float minDistanceFromCenter = carAvoidanceRadius + 2f;
        transform.position = carAvoidanceCenter + directionFromCenter * minDistanceFromCenter;
    }
    
    private void EnsureMinimumDistanceFromOtherParts()
    {
        if (!enablePartCollision) return;
        
        RefreshPartsCache();
        float minDistance = 2f;
        int maxAttempts = 10;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            bool tooClose = false;
            
            foreach (MovingCarPart otherPart in allPartsCache)
            {
                if (otherPart == this || otherPart == null) continue;
                
                Vector2 myPos = new Vector2(transform.position.x, transform.position.y);
                Vector2 otherPos = new Vector2(otherPart.transform.position.x, otherPart.transform.position.y);
                float distance = Vector2.Distance(myPos, otherPos);
                
                if (distance < minDistance)
                {
                    Vector2 pushDir = (myPos - otherPos).normalized;
                    transform.position += new Vector3(pushDir.x, pushDir.y, 0) * (minDistance - distance + 0.5f);
                    tooClose = true;
                }
            }
            
            if (!tooClose) break;
        }
    }
    
    private void Update()
    {
        if (rb != null && rb.constraints == RigidbodyConstraints.FreezeAll)
        {
            Destroy(this);
            return;
        }
        
        if (gameObject.CompareTag("Placed"))
        {
            Destroy(this);
            return;
        }
        
        if (isFrozen) return;
        
        CheckIfGrabbed();
        
        if (isGrabbed && pauseWhenGrabbed) return;
        
        ExecuteMovement();
    }
    
    private void CheckIfGrabbed()
    {
        isGrabbed = partMover != null && partMover.IsDragging && partMover.CurrentPart == gameObject;
    }
    
    private void ExecuteMovement()
    {
        switch (pattern)
        {
            case MovementPattern.Bounce: MoveBouncing(); break;
            case MovementPattern.Circle: MoveInCircle(); break;
            case MovementPattern.Horizontal: MoveHorizontal(); break;
            case MovementPattern.Vertical: MoveVertical(); break;
        }
    }
    
    private void MoveBouncing()
    {
        transform.position += _velocity * Time.deltaTime;
        
        Vector3 pos = transform.position;
        bool bounced = false;
        
        if (bounceOffGhosts && Time.frameCount % 3 == 0)
        {
            if (CheckGhostCollision(ref pos))
                bounced = true;
        }
        
        if (enablePartCollision && Time.frameCount % 5 == 0)
            ApplyPartRepulsion(ref pos);
        
        float distanceFromCar = Vector3.Distance(pos, carAvoidanceCenter);
        if (distanceFromCar < carAvoidanceRadius)
        {
            Vector3 awayFromCar = (pos - carAvoidanceCenter).normalized;
            _velocity = Vector3.Reflect(_velocity, awayFromCar) * bounceDamping;
            pos = carAvoidanceCenter + awayFromCar * carAvoidanceRadius;
            bounced = true;
        }
        
        if (useWorldBounds)
        {
            if (pos.x < worldBoundsMin.x) { _velocity.x = Mathf.Abs(_velocity.x) * bounceDamping; pos.x = worldBoundsMin.x; bounced = true; }
            else if (pos.x > worldBoundsMax.x) { _velocity.x = -Mathf.Abs(_velocity.x) * bounceDamping; pos.x = worldBoundsMax.x; bounced = true; }
            
            if (pos.y < worldBoundsMin.y) { _velocity.y = Mathf.Abs(_velocity.y) * bounceDamping; pos.y = worldBoundsMin.y; bounced = true; }
            else if (pos.y > worldBoundsMax.y) { _velocity.y = -Mathf.Abs(_velocity.y) * bounceDamping; pos.y = worldBoundsMax.y; bounced = true; }
        }
        else if (mainCamera != null)
        {
            Vector3 screenPos = mainCamera.WorldToScreenPoint(pos);
            
            if (screenPos.x < screenPadding) { _velocity.x = Mathf.Abs(_velocity.x) * bounceDamping; screenPos.x = screenPadding; pos = mainCamera.ScreenToWorldPoint(screenPos); bounced = true; }
            else if (screenPos.x > Screen.width - screenPadding) { _velocity.x = -Mathf.Abs(_velocity.x) * bounceDamping; screenPos.x = Screen.width - screenPadding; pos = mainCamera.ScreenToWorldPoint(screenPos); bounced = true; }
            
            float bottomPadding = 80f;
            if (screenPos.y < bottomPadding) { _velocity.y = Mathf.Abs(_velocity.y) * bounceDamping; screenPos.y = bottomPadding; pos = mainCamera.ScreenToWorldPoint(screenPos); bounced = true; }
            else if (screenPos.y > Screen.height - screenPadding) { _velocity.y = -Mathf.Abs(_velocity.y) * bounceDamping; screenPos.y = Screen.height - screenPadding; pos = mainCamera.ScreenToWorldPoint(screenPos); bounced = true; }
        }
        
        transform.position = pos;
        
        if (bounced)
        {
            float randomAngle = Random.Range(-5f, 5f) * Mathf.Deg2Rad;
            float currentAngle = Mathf.Atan2(_velocity.y, _velocity.x) + randomAngle;
            float speed = _velocity.magnitude;
            _velocity = new Vector3(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle), 0) * speed;
        }
        
        float currentSpeed = _velocity.magnitude;
        if (currentSpeed < minSpeed)
            _velocity = _velocity.normalized * minSpeed;
        else if (currentSpeed > maxSpeed)
            _velocity = _velocity.normalized * maxSpeed;
    }
    
    private bool CheckGhostCollision(ref Vector3 pos)
    {
        RefreshGhostsCache();
        
        bool collided = false;
        Vector2 pos2D = new Vector2(pos.x, pos.y);
        
        foreach (GameObject ghost in allGhostsCache)
        {
            if (ghost == null) continue;
            
            Vector2 ghostPos2D = new Vector2(ghost.transform.position.x, ghost.transform.position.y);
            float distance = Vector2.Distance(pos2D, ghostPos2D);
            
            if (distance < ghostBounceDistance)
            {
                Vector2 normal = (pos2D - ghostPos2D).normalized;
                Vector2 vel2D = new Vector2(_velocity.x, _velocity.y);
                Vector2 reflected = Vector2.Reflect(vel2D, normal);
                _velocity = new Vector3(reflected.x, reflected.y, 0) * bounceDamping;
                pos = ghost.transform.position + new Vector3(normal.x, normal.y, 0) * ghostBounceDistance;
                collided = true;
            }
        }
        
        return collided;
    }
    
    private void MoveInCircle()
    {
        angle += moveSpeed * Time.deltaTime * 50f;
        if (angle > 360f) angle -= 360f;
        
        float radians = angle * Mathf.Deg2Rad;
        transform.position = startPosition + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * radius;
    }
    
    private void MoveHorizontal()
    {
        angle += moveSpeed * Time.deltaTime * 50f;
        if (angle > 360f) angle -= 360f;
        transform.position = startPosition + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 0, 0);
    }
    
    private void MoveVertical()
    {
        angle += moveSpeed * Time.deltaTime * 50f;
        if (angle > 360f) angle -= 360f;
        transform.position = startPosition + new Vector3(0, Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 0);
    }
    
    private void ApplyPartRepulsion(ref Vector3 pos)
    {
        if (!enablePartCollision) return;
        
        RefreshPartsCache();
        
        foreach (MovingCarPart otherPart in allPartsCache)
        {
            if (otherPart == this || otherPart == null) continue;
            
            Vector2 myPos2D = new Vector2(pos.x, pos.y);
            Vector2 otherPos2D = new Vector2(otherPart.transform.position.x, otherPart.transform.position.y);
            float distance = Vector2.Distance(myPos2D, otherPos2D);
            
            if (distance < partRepulsionDistance && distance > 0.01f)
            {
                Vector2 pushDir = (myPos2D - otherPos2D).normalized;
                float repulsionStrength = (1f - distance / partRepulsionDistance) * partRepulsionForce;
                pos += new Vector3(pushDir.x * repulsionStrength, pushDir.y * repulsionStrength, 0);
                _velocity += new Vector3(pushDir.x, pushDir.y, 0) * repulsionStrength * 0.5f;
            }
        }
    }
    
    private static void RefreshPartsCache()
    {
        if (Time.time - lastCacheTime > CACHE_REFRESH_INTERVAL || allPartsCache == null)
        {
            allPartsCache = FindObjectsOfType<MovingCarPart>();
            lastCacheTime = Time.time;
        }
    }
    
    private static void RefreshGhostsCache()
    {
        if (Time.time - lastCacheTime > CACHE_REFRESH_INTERVAL || allGhostsCache == null)
        {
            GameObject[] tagged = GameObject.FindGameObjectsWithTag("Ghost");
            
            if (tagged.Length == 0)
            {
                System.Collections.Generic.List<GameObject> ghosts = new System.Collections.Generic.List<GameObject>();
                GameObject[] allObjects = FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name.Contains("Ghost") && obj.activeSelf && !ghosts.Contains(obj))
                        ghosts.Add(obj);
                }
                allGhostsCache = ghosts.ToArray();
            }
            else
            {
                allGhostsCache = tagged;
            }
        }
    }
    
    // Public API
    public void SetMovementPattern(MovementPattern newPattern)
    {
        pattern = newPattern;
        if (newPattern == MovementPattern.Bounce && _velocity.magnitude < 0.1f)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            _velocity = new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0) * moveSpeed;
        }
    }
    
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
        minSpeed = speed * 0.75f;
        maxSpeed = speed * 1.5f;
        
        if (pattern == MovementPattern.Bounce && _velocity.magnitude > 0.1f)
            _velocity = _velocity.normalized * speed;
    }
    
    public void SetRadius(float newRadius) => radius = newRadius;
    
    public void SetBounds(Vector2 min, Vector2 max)
    {
        useWorldBounds = true;
        worldBoundsMin = min;
        worldBoundsMax = max;
    }
    
    public void SetFrozen(bool frozen)
    {
        if (isPermanentlyFrozen && !frozen) return;
        
        if (!frozen && isFrozen && !isPermanentlyFrozen && _velocity.magnitude < 0.1f)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            _velocity = new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0) * moveSpeed;
        }
        
        isFrozen = frozen;
    }
    
    public void SetPermanentlyFrozen(bool permanent)
    {
        isPermanentlyFrozen = permanent;
        if (permanent)
        {
            isFrozen = true;
            _velocity = Vector3.zero;
        }
    }
    
    public void SetCarAvoidanceZone(Vector3 center, float avoidRadius)
    {
        carAvoidanceCenter = center;
        carAvoidanceRadius = avoidRadius;
    }
    
    public void SetEnablePartCollision(bool enable) => enablePartCollision = enable;
    
    public void SetBounceOffGhosts(bool enable)
    {
        bounceOffGhosts = enable;
        if (enable) RefreshGhostsCache();
    }
    
    public void SetGhostBounceDistance(float distance) => ghostBounceDistance = distance;
    
    public void ApplyVelocityBoost()
    {
        if (_velocity.magnitude < minSpeed)
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            _velocity = new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0) * moveSpeed;
        }
    }
}