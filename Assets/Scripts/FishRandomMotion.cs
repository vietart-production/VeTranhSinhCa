using UnityEngine;

public enum FishMotionSpecies
{
    Horizontal,
    Seahorse,
    Jellyfish,
    Shrimp,
    Crab,
    Starfish
}

public sealed class FishRandomMotion : MonoBehaviour
{
    public BoxCollider2D movementBounds;
    public FishMotionSpecies species = FishMotionSpecies.Horizontal;
    public float speed = 1f;
    public float verticalRange = 1f;
    public float targetReachDistance = 0.08f;
    public bool spriteFacesRight = true;
    [Min(0f)] public float cameraEdgePadding = 0.05f;
    public bool startOnEnable = true;

    Vector3 targetPosition;
    float seed;
    bool isMoving;
    int travelDirection;
    bool hasTravelDirection;
    float startleTimer;
    bool startleUpward;
    float startleSpeedMultiplier = 1f;
    bool rotationSuppressed;

    public void Configure(
        BoxCollider2D bounds,
        FishMotionSpecies motionSpecies,
        float motionSpeed,
        float motionVerticalRange,
        bool modelFacesRight,
        bool beginImmediately = true)
    {
        movementBounds = bounds;
        species = motionSpecies;
        speed = Mathf.Max(0.05f, motionSpeed);
        verticalRange = Mathf.Max(0.1f, motionVerticalRange);
        spriteFacesRight = modelFacesRight;
        startOnEnable = beginImmediately;
        seed = Random.Range(0f, 1000f);
        isMoving = beginImmediately;
        PickNextTarget();
    }

    public void BeginMotion()
    {
        isMoving = true;
        PickNextTarget();
    }

    public void StartleFromClick(float duration)
    {
        isMoving = true;
        startleSpeedMultiplier = 2.5f;
        startleTimer = Mathf.Max(0f, duration);

        if (species == FishMotionSpecies.Jellyfish)
        {
            startleUpward = true;
            return;
        }

        Bounds bounds = GetCameraMovementBounds();
        float reverseX = travelDirection >= 0 ? bounds.min.x : bounds.max.x;
        targetPosition = new Vector3(reverseX, transform.position.y, transform.position.z);
        travelDirection *= -1;
    }

    public void SetRotationSuppressed(bool suppressed)
    {
        rotationSuppressed = suppressed;
    }

    void OnEnable()
    {
        if (startOnEnable)
            isMoving = true;
    }

    void Update()
    {
        if (!isMoving || movementBounds == null)
            return;

        if (species == FishMotionSpecies.Jellyfish)
        {
            MoveJellyfish();
            return;
        }

        if (Vector2.Distance(transform.position, targetPosition) <= targetReachDistance)
        {
            PickNextTarget(true);
        }

        float motionSpeed = speed;
        if (species == FishMotionSpecies.Shrimp)
            motionSpeed *= 1.8f;
        else if (species == FishMotionSpecies.Crab)
            motionSpeed *= 0.55f;
        else if (species == FishMotionSpecies.Starfish)
            motionSpeed *= 0.35f;
        motionSpeed *= startleSpeedMultiplier;
        if (startleTimer > 0f)
        {
            startleTimer -= Time.deltaTime;
            if (startleTimer <= 0f)
                startleSpeedMultiplier = 1f;
        }

        float horizontalMovement = targetPosition.x - transform.position.x;
        if (!rotationSuppressed)
            ApplyFacing(horizontalMovement);
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            motionSpeed * Time.deltaTime);
    }

    void PickNextTarget(bool playTurnSpin = false)
    {
        Bounds bounds = GetCameraMovementBounds();
        float x = ChooseHorizontalTargetX(bounds);
        float y;

        switch (species)
        {
            case FishMotionSpecies.Crab:
                y = Mathf.Lerp(bounds.min.y, bounds.max.y, Random.Range(0.04f, 0.16f));
                break;
            case FishMotionSpecies.Starfish:
                y = Mathf.Lerp(bounds.min.y, bounds.max.y, Random.Range(0.06f, 0.28f));
                break;
            case FishMotionSpecies.Seahorse:
                y = Random.Range(bounds.min.y, bounds.max.y);
                break;
            case FishMotionSpecies.Shrimp:
                x = Mathf.Clamp(
                    transform.position.x + Random.Range(-2.5f, 2.5f),
                    bounds.min.x,
                    bounds.max.x);
                y = Mathf.Clamp(
                    transform.position.y + Random.Range(-0.35f, 0.35f),
                    bounds.min.y,
                    bounds.max.y);
                break;
            default:
                y = Random.Range(bounds.min.y, bounds.max.y);
                break;
        }

        targetPosition = new Vector3(x, y, transform.position.z);
        UpdateTravelDirection(x - transform.position.x, playTurnSpin);
    }

    void UpdateTravelDirection(float horizontalMovement, bool allowAutoSpin)
    {
        if (Mathf.Abs(horizontalMovement) < 0.001f)
            return;

        int newDirection = horizontalMovement > 0f ? 1 : -1;
        if (hasTravelDirection && newDirection != travelDirection && allowAutoSpin &&
            species != FishMotionSpecies.Shrimp &&
            species != FishMotionSpecies.Starfish)
        {
            GetComponent<DOTweenFishAnim>()?.TriggerTurnBend();
            GetComponent<FishClickInteraction>()?.PlaySpinVariant(
                FishClickInteraction.ClickSpinVariant.ReturnToOriginal);
        }

        // Spin khi có click/startle hoặc khi cá hoàn tất đường đi tới biên và quay
        // đầu. Các target ngẫu nhiên ngắn của tôm/sao biển không kích hoạt spin.
        travelDirection = newDirection;
        hasTravelDirection = true;
    }

    float ChooseHorizontalTargetX(Bounds bounds)
    {
        if (species == FishMotionSpecies.Shrimp || species == FishMotionSpecies.Starfish)
            return Random.Range(bounds.min.x, bounds.max.x);

        if (transform.position.x <= bounds.center.x)
            return bounds.max.x;

        return bounds.min.x;
    }

    Bounds GetCameraMovementBounds()
    {
        Bounds bounds = movementBounds.bounds;
        float padding = Mathf.Max(0f, cameraEdgePadding);
        bounds.Expand(new Vector3(-padding * 2f, -padding * 2f, 0f));
        bounds.center = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
        bounds.size = new Vector3(
            Mathf.Max(0.01f, bounds.size.x),
            Mathf.Max(0.01f, bounds.size.y),
            0f);
        return bounds;
    }

    void ApplyFacing(float horizontalMovement)
    {
        if (Mathf.Abs(horizontalMovement) < 0.001f || species == FishMotionSpecies.Jellyfish)
            return;

        float direction = horizontalMovement > 0f ? 1f : -1f;
        float facingSign = spriteFacesRight ? direction : -direction;
        Vector3 scale = transform.localScale;
        transform.localScale = new Vector3(
            Mathf.Abs(scale.x) * facingSign,
            scale.y,
            scale.z);
    }

    void MoveJellyfish()
    {
        Bounds bounds = GetCameraMovementBounds();
        float time = Time.time;
        Vector3 previousPosition = transform.position;
        Vector3 position = transform.position;
        position.x += Mathf.Sin(time * 0.23f + seed) * speed * Time.deltaTime;
        float verticalSpeed = startleUpward ? speed * startleSpeedMultiplier : speed * 0.75f;
        position.y += startleUpward
            ? verticalSpeed * Time.deltaTime
            : Mathf.Sin(time * 0.7f + seed * 0.37f) * verticalSpeed * Time.deltaTime;
        position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
        position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
        transform.position = position;
        Vector3 movement = position - previousPosition;
        if (movement.x > 0.0001f || movement.x < -0.0001f)
            UpdateTravelDirection(movement.x, true);

        if (startleTimer > 0f)
        {
            startleTimer -= Time.deltaTime;
            if (startleTimer <= 0f)
            {
                startleSpeedMultiplier = 1f;
                startleUpward = false;
            }
        }
    }

    void ApplyMovementRotation(Vector3 movement)
    {
        if (rotationSuppressed || movement.sqrMagnitude <= 0.0000001f)
            return;

        float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
        if (!spriteFacesRight)
            angle += 180f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
