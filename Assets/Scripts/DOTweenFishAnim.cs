using DG.Tweening;
using UnityEngine;

public class DOTweenFishAnim : MonoBehaviour
{
    [Header("Vùng bơi")]
    public BoxCollider2D swimBounds;

    [Header("Spawn")]
    [Min(0f)] public float spawnDuration = 0.5f;
    [Min(0f)] public float dropDuration = 1.2f;

    [Header("Bơi")]
    [Min(0.1f)] public float swimSpeed = 3f;
    [Min(2)] public int waypointsPerSegment = 5;
    [Range(-1, 1)]
    [Tooltip("-1: bắt đầu sang trái, 1: bắt đầu sang phải, 0: tự chọn theo vị trí.")]
    public int initialDirectionOverride;

    [Header("Đường bơi")]
    [Min(0f)] public float waveHeight = 2f;
    [Tooltip("Giữ cá trong một dải Y riêng để đàn cá không dồn vào giữa màn hình.")]
    public bool useSwimLane;
    [Range(0f, 1f)] public float swimLaneCenter01 = 0.5f;
    [Range(0.05f, 1f)] public float swimLaneHeight01 = 0.2f;
    [Range(0f, 45f)] public float maxTiltAngle = 20f;
    [Min(0.1f)] public float tiltSmoothSpeed = 6f;
    [Range(1f, 3f)] public float downwardTiltMultiplier = 1.35f;
    [Range(0f, 15f)] public float minimumDownwardTilt = 5f;

    [Header("Hướng model gốc")]
    [Tooltip("Bật nếu model/sprite ở scale X dương vốn quay mặt sang phải.")]
    public bool spriteFacesRight = true;

    [Header("Vẫy đuôi (shader FishBody)")]
    [Tooltip("Số đoạn chia dọc thân của mesh lưới dùng chung cho mọi con cá.")]
    [Min(2)] public int bodyMeshSegments = 12;
    [Min(0f)] public float waveAmplitude = 0.3f;
    [Tooltip("Tần số vẫy = swimSpeed * hệ số này.")]
    [Min(0f)] public float waveFrequencyPerSpeed = 1.4f;
    public float waveLength = 2.5f;
    [Tooltip("Đuôi vẫy mạnh thêm bao nhiêu % khi cá đang rẽ gấp (theo góc nghiêng thực tế).")]
    [Range(0f, 2f)] public float turnAmplitudeBoost = 0.6f;
    [Tooltip("0..1: trước ngưỡng này (tính từ đầu) gần như đứng yên, chỉ đoạn cuối đuôi mới vẫy.")]
    [Range(0f, 0.95f)] public float tailWaveStart = 0.72f;

    [Header("Nhịp bơi")]
    [Tooltip("Biến thiên tốc độ mỗi đoạn bơi, dùng Perlin noise chậm thay vì random đột ngột.")]
    [Range(0f, 0.6f)] public float speedVariation = 0.25f;
    [Min(0.01f)] public float speedNoiseFrequency = 0.15f;

    [Header("Xoắn toàn thân (ngẫu nhiên, phong cách hoạt hình)")]
    [Tooltip("Thời gian con xoắn lan từ đầu mũi đến đuôi.")]
    [Min(0.05f)] public float twistTravelDuration = 0.5f;
    [Tooltip("Thời gian mỗi điểm trên thân tự xoay đủ 1 vòng 360° khi con xoắn đi qua.")]
    [Min(0.05f)] public float twistSpinDuration = 0.35f;
    [Tooltip("Khoảng thời gian ngẫu nhiên nghỉ thêm giữa 2 lần xoắn (sau khi lần trước đã lan hết tới đuôi).")]
    public Vector2 twistRestIntervalRange = new Vector2(2.5f, 6f);

    [Header("Thời gian tồn tại")]
    public float lifetime = 15f;
    [Min(0f)] public float fadeOutDuration = 1f;

    private static Mesh sharedBodyMesh;

    private Vector3 lastPos;
    private Bounds bounds;
    private float lifeTimer;
    private bool isDying;
    private Material bodyMaterial;
    private float motionSeed;
    private float currentSwimSpeed;
    private Tween twistScheduleTween;
    private Tween fadeOutTween;
    private Tween currentPathTween;
    private int currentDirection = -1;
    private int facingDirection = -1;
    private bool dropPrepared;
    private Vector3 dropLandingPosition;
    private float dropWaterSurfaceY;
    private float dropAirPhaseRatio = 0.28f;
    private float dropSettlePhaseRatio = 0.2f;
    private float dropSinkOvershoot = 0.22f;
    private float dropImpactTilt = 9f;
    private Material dropBubbleMaterial;
    private int dropBubbleCount = 18;
    private Vector2 dropBubbleSizeRange = new Vector2(0.08f, 0.2f);
    private bool initialized;

    public void PrepareDrop(Vector3 landingPosition, float duration)
    {
        dropPrepared = true;
        dropLandingPosition = landingPosition;
        dropDuration = Mathf.Max(0f, duration);
        dropWaterSurfaceY = Mathf.Lerp(transform.position.y, landingPosition.y, 0.3f);
    }

    public void PrepareWaterDrop(
        Vector3 landingPosition,
        float waterSurfaceY,
        float duration,
        float airPhaseRatio,
        float settlePhaseRatio,
        float sinkOvershoot,
        float impactTilt,
        Material bubbleMaterial,
        int bubbleCount,
        Vector2 bubbleSizeRange)
    {
        dropPrepared = true;
        dropLandingPosition = landingPosition;
        dropWaterSurfaceY = waterSurfaceY;
        dropDuration = Mathf.Max(0.2f, duration);
        dropAirPhaseRatio = Mathf.Clamp(airPhaseRatio, 0.1f, 0.55f);
        dropSettlePhaseRatio = Mathf.Clamp(settlePhaseRatio, 0.1f, 0.4f);
        dropSinkOvershoot = Mathf.Max(0f, sinkOvershoot);
        dropImpactTilt = Mathf.Clamp(impactTilt, 0f, 25f);
        dropBubbleMaterial = bubbleMaterial;
        dropBubbleCount = Mathf.Max(0, bubbleCount);
        dropBubbleSizeRange = bubbleSizeRange;
    }

    public void StopSwimmingImmediately()
    {
        if (currentPathTween != null && currentPathTween.IsActive())
            currentPathTween.Kill();

        currentPathTween = null;
        // Con xoan (neu dang chay) van tu ket thuc theo _Time.y ben shader, chi
        // can dung hen gio cho lan xoan tiep theo trong luc ca dang bo chay.
        twistScheduleTween?.Kill();
        transform.DOKill();
        enabled = false;
    }

    public void ResumeSwimmingFromCurrentPosition(int horizontalDirection)
    {
        if (currentPathTween != null && currentPathTween.IsActive())
            currentPathTween.Kill();

        transform.DOKill();
        bool wasInitialized = initialized;
        initialized = true;
        isDying = false;
        currentSwimSpeed = swimSpeed;
        SetWaveFrequencyMultiplier(1f);
        lastPos = transform.position;
        waypointsPerSegment = Mathf.Max(2, waypointsPerSegment);
        ResolveBounds();
        ClampPositionToBounds();

        if (!wasInitialized)
            lifeTimer = lifetime;

        currentDirection = horizontalDirection >= 0 ? 1 : -1;
        facingDirection = currentDirection;
        transform.rotation = Quaternion.identity;
        enabled = true;
        ApplyFacingDirection();
        StartNewSwimSegment(false);
        ScheduleNextTwist();
    }

    void Awake()
    {
        ApplyBodyMesh();
    }

    void Start()
    {
        if (initialized)
            return;

        initialized = true;
        lastPos = transform.position;
        lifeTimer = lifetime;
        waypointsPerSegment = Mathf.Max(2, waypointsPerSegment);
        currentSwimSpeed = swimSpeed;
        SetWaveFrequencyMultiplier(1f);

        ResolveBounds();

        if (dropPrepared)
        {
            dropLandingPosition.x = Mathf.Clamp(dropLandingPosition.x, bounds.min.x, bounds.max.x);
            dropLandingPosition.y = Mathf.Clamp(dropLandingPosition.y, bounds.min.y, bounds.max.y);
            dropLandingPosition.z = transform.position.z;
        }
        else
        {
            ClampPositionToBounds();
        }

        // Segment đầu tiên đi về phía còn nhiều khoảng trống hơn.
        float directionX = dropPrepared ? dropLandingPosition.x : transform.position.x;
        float distanceToLeft = directionX - bounds.min.x;
        float distanceToRight = bounds.max.x - directionX;
        currentDirection = initialDirectionOverride == 0
            ? (distanceToRight >= distanceToLeft ? 1 : -1)
            : (initialDirectionOverride > 0 ? 1 : -1);
        facingDirection = currentDirection;

        Vector3 originalScale = transform.localScale;
        if (dropPrepared)
        {
            ApplyFacingDirection();
            StartPreparedWaterDrop();
        }
        else
        {
            transform.localScale = Vector3.zero;
            currentPathTween = transform.DOScale(originalScale, spawnDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    currentPathTween = null;
                    StartNewSwimSegment(false);
                });
        }

        ScheduleNextTwist();
    }

    // Chay trong Awake (khong phai Start) de van gan duoc mesh/material cho ca
    // bi disable ngay sau Instantiate (VD ca_muc dung JellyfishRiseAnim thay vi
    // DOTweenFishAnim), vi Awake luon chay du component co the bi tat sau do.
    void ApplyBodyMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
            meshFilter.sharedMesh = GetOrCreateBodyMesh(bodyMeshSegments);

        Renderer renderer = GetComponent<Renderer>();
        bodyMaterial = renderer != null ? renderer.sharedMaterial : null;
        // Seed rieng cho tung con: dung lam pha vay trong shader va lam toa do
        // x cua Perlin noise tao nhip toc do, tranh ca dong bo "ban sao" giong nhau.
        // Chay o Awake nen swimSpeed luc nay co the chua duoc caller chinh theo
        // do sau; currentSwimSpeed se duoc dong bo lai o Start/StartNewSwimSegment.
        motionSeed = Random.Range(0f, 1000f);

        if (bodyMaterial == null)
            return;

        if (bodyMaterial.HasProperty("_WaveAmplitude"))
            bodyMaterial.SetFloat("_WaveAmplitude", waveAmplitude);
        if (bodyMaterial.HasProperty("_WaveLength"))
            bodyMaterial.SetFloat("_WaveLength", waveLength);
        if (bodyMaterial.HasProperty("_WavePhase"))
            bodyMaterial.SetFloat("_WavePhase", motionSeed);
        if (bodyMaterial.HasProperty("_TailWaveStart"))
            bodyMaterial.SetFloat("_TailWaveStart", tailWaveStart);
        if (bodyMaterial.HasProperty("_TwistTravelDuration"))
            bodyMaterial.SetFloat("_TwistTravelDuration", twistTravelDuration);
        if (bodyMaterial.HasProperty("_TwistSpinDuration"))
            bodyMaterial.SetFloat("_TwistSpinDuration", twistSpinDuration);
        if (bodyMaterial.HasProperty("_TwistTriggerTime"))
            bodyMaterial.SetFloat("_TwistTriggerTime", -1000f);
    }

    // Mesh phang (Quad) chi co 4 dinh goc nen shader khong the uon cong duoc;
    // luoi nay them dinh doc theo truc dai than de bien do vay tang dan ve
    // phia duoi. Dung chung 1 instance cho moi con ca vi hinh hoc giong het nhau.
    static Mesh GetOrCreateBodyMesh(int segments)
    {
        if (sharedBodyMesh != null)
            return sharedBodyMesh;

        segments = Mathf.Max(2, segments);
        int columnCount = segments + 1;
        var vertices = new Vector3[columnCount * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];

        for (int x = 0; x < columnCount; x++)
        {
            float u = x / (float)segments;
            float posX = u - 0.5f;
            vertices[x * 2] = new Vector3(posX, -0.5f, 0f);
            vertices[x * 2 + 1] = new Vector3(posX, 0.5f, 0f);
            uv[x * 2] = new Vector2(u, 0f);
            uv[x * 2 + 1] = new Vector2(u, 1f);
        }

        int triangleIndex = 0;
        for (int x = 0; x < segments; x++)
        {
            int bottomLeft = x * 2;
            int topLeft = x * 2 + 1;
            int bottomRight = (x + 1) * 2;
            int topRight = (x + 1) * 2 + 1;

            triangles[triangleIndex++] = bottomLeft;
            triangles[triangleIndex++] = topLeft;
            triangles[triangleIndex++] = bottomRight;

            triangles[triangleIndex++] = topLeft;
            triangles[triangleIndex++] = topRight;
            triangles[triangleIndex++] = bottomRight;
        }

        sharedBodyMesh = new Mesh { name = "FishBodyGrid" };
        sharedBodyMesh.SetVertices(vertices);
        sharedBodyMesh.SetUVs(0, uv);
        sharedBodyMesh.SetTriangles(triangles, 0);
        sharedBodyMesh.RecalculateNormals();
        sharedBodyMesh.RecalculateBounds();
        return sharedBodyMesh;
    }

    // Dung chung cho ca bo chay (tang tam thoi roi lerp ve 1) va cho lan bat dau
    // boi binh thuong (multiplier = 1). Tan so cuoi = currentSwimSpeed * he so *
    // multiplier de ca boi nhanh thi vay nhanh, khop dung theo toc do boi thuc te
    // (ke ca khi toc do dang dao dong theo nhip Perlin noise moi doan).
    public void SetWaveFrequencyMultiplier(float multiplier)
    {
        if (bodyMaterial == null || !bodyMaterial.HasProperty("_WaveFrequency"))
            return;

        bodyMaterial.SetFloat("_WaveFrequency", currentSwimSpeed * waveFrequencyPerSpeed * multiplier);
    }

    // Kich hoat 1 lan xoan 360 do lan tu dau den duoi (xem FishBody.shader).
    // Chi can bao shader "moc thoi gian bat dau" 1 lan; toan bo tien trinh lan
    // toi + tu xoay du 1 vong roi dung tai moi diem deu do chinh shader tinh
    // theo _Time.y, khong can script cap nhat gia tri moi frame.
    void ScheduleNextTwist()
    {
        twistScheduleTween?.Kill();
        float restDelay = Random.Range(twistRestIntervalRange.x, twistRestIntervalRange.y);
        float nextDelay = twistTravelDuration + twistSpinDuration + restDelay;
        twistScheduleTween = DOVirtual.DelayedCall(nextDelay, PlayTwist);
    }

    void PlayTwist()
    {
        if (isDying)
            return;

        if (bodyMaterial != null && bodyMaterial.HasProperty("_TwistTriggerTime"))
            bodyMaterial.SetFloat("_TwistTriggerTime", Time.timeSinceLevelLoad);

        ScheduleNextTwist();
    }

    void StartPreparedWaterDrop()
    {
        float safeDuration = Mathf.Max(0.2f, dropDuration);
        float airRatio = Mathf.Clamp(dropAirPhaseRatio, 0.1f, 0.55f);
        float settleRatio = Mathf.Clamp(
            dropSettlePhaseRatio,
            0.1f,
            Mathf.Max(0.1f, 0.8f - airRatio));
        float underwaterRatio = Mathf.Max(0.15f, 1f - airRatio - settleRatio);

        float airDuration = safeDuration * airRatio;
        float underwaterDuration = safeDuration * underwaterRatio;
        float settleDuration = safeDuration * settleRatio;

        float surfaceY = Mathf.Clamp(
            dropWaterSurfaceY,
            dropLandingPosition.y + 0.08f,
            transform.position.y - 0.05f);
        Vector3 waterEntryPosition = new Vector3(
            dropLandingPosition.x,
            surfaceY,
            dropLandingPosition.z);

        Vector3 submergedPosition = dropLandingPosition;
        submergedPosition.y = Mathf.Max(
            bounds.min.y + 0.05f,
            dropLandingPosition.y - dropSinkOvershoot);
        submergedPosition.x = Mathf.Clamp(
            dropLandingPosition.x + UnityEngine.Random.Range(-0.14f, 0.14f),
            bounds.min.x,
            bounds.max.x);

        float tiltDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float entryTilt = dropImpactTilt * tiltDirection;
        transform.rotation = Quaternion.Euler(0f, 0f, entryTilt * -0.25f);

        Sequence dropSequence = DOTween.Sequence();
        dropSequence.Append(
            transform.DOMove(waterEntryPosition, airDuration)
                .SetEase(Ease.InQuad));
        dropSequence.Join(
            transform.DORotate(new Vector3(0f, 0f, entryTilt), airDuration)
                .SetEase(Ease.InSine));
        dropSequence.AppendCallback(() => CreateWaterEntryBubbles(waterEntryPosition));
        dropSequence.Append(
            transform.DOMove(submergedPosition, underwaterDuration)
                .SetEase(Ease.OutCubic));
        dropSequence.Join(
            transform.DORotate(new Vector3(0f, 0f, -entryTilt * 0.35f), underwaterDuration)
                .SetEase(Ease.InOutSine));
        dropSequence.Append(
            transform.DOMove(dropLandingPosition, settleDuration)
                .SetEase(Ease.OutSine));
        dropSequence.Join(
            transform.DORotate(Vector3.zero, settleDuration)
                .SetEase(Ease.OutSine));
        dropSequence.OnComplete(() =>
        {
            currentPathTween = null;
            transform.rotation = Quaternion.identity;
            StartNewSwimSegment(false);
        });

        currentPathTween = dropSequence;
    }

    void CreateWaterEntryBubbles(Vector3 position)
    {
        if (dropBubbleCount <= 0)
            return;

        GameObject effectObject = new GameObject("FishWaterEntryBubbles");
        effectObject.transform.position = position;
        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);
        float minimumSize = Mathf.Min(dropBubbleSizeRange.x, dropBubbleSizeRange.y);
        float maximumSize = Mathf.Max(dropBubbleSizeRange.x, dropBubbleSizeRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(minimumSize, maximumSize);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.7f, 0.93f, 1f, 0.82f),
            new Color(0.9f, 0.99f, 1f, 0.55f));
        main.maxParticles = Mathf.Max(24, dropBubbleCount * 2);
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.burstCount = 1;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)dropBubbleCount));

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.6f;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortingOrder = 25;
        if (dropBubbleMaterial != null)
            particleRenderer.sharedMaterial = dropBubbleMaterial;

        particles.Play();
    }

    void Update()
    {
        if (isDying)
            return;

        // lifetime <= 0 được dùng cho đàn cá mặc định tồn tại suốt scene.
        if (lifetime > 0f)
        {
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f)
                Die();
        }
    }

    void ResolveBounds()
    {
        if (swimBounds != null)
        {
            bounds = swimBounds.bounds;
            return;
        }

        if (Camera.main != null && Camera.main.orthographic)
        {
            float height = Camera.main.orthographicSize;
            float width = height * Camera.main.aspect;
            bounds = new Bounds(Camera.main.transform.position, new Vector3(width * 2f, height * 2f, 10f));
        }
        else
        {
            bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 10f));
        }

        Debug.LogWarning("[DOTweenFishAnim] Chưa gán Swim Bounds; đang dùng vùng bơi mặc định.");
    }

    void StartNewSwimSegment(bool reverseDirection = true)
    {
        if (isDying)
            return;

        // Không đảo ở segment đầu. Các segment sau chỉ đảo đúng một lần khi chạm mép.
        if (reverseDirection)
            currentDirection *= -1;

        facingDirection = currentDirection;
        ApplyFacingDirection();
        lastPos = transform.position;

        // Noise cham (co seed rieng tung con) thay vi Random.Range moi doan, de
        // toc do co "nhip" tu nhien thay vi deu tuyet doi giua cac doan boi.
        float speedNoise = Mathf.PerlinNoise(motionSeed, Time.time * speedNoiseFrequency) * 2f - 1f;
        currentSwimSpeed = swimSpeed * (1f + speedNoise * speedVariation);
        SetWaveFrequencyMultiplier(1f);

        Vector3[] path = GenerateSmoothPath();
        float duration = CalculatePathLength(path) / Mathf.Max(currentSwimSpeed, 0.1f);

        if (currentPathTween != null && currentPathTween.IsActive())
            currentPathTween.Kill();

        currentPathTween = transform.DOPath(path, duration, PathType.CatmullRom)
            .SetEase(Ease.Linear)
            .OnUpdate(UpdateFishTilt)
            .OnComplete(() => StartNewSwimSegment(true));
    }

    Vector3[] GenerateSmoothPath()
    {
        int waypointCount = Mathf.Max(2, waypointsPerSegment);
        Vector3[] path = new Vector3[waypointCount];
        const float desiredPadding = 1f;

        float paddingX = Mathf.Min(desiredPadding, bounds.extents.x * 0.4f);
        float paddingY = Mathf.Min(desiredPadding, bounds.extents.y * 0.4f);
        float minX = bounds.min.x + paddingX;
        float maxX = bounds.max.x - paddingX;
        float minY = bounds.min.y + paddingY;
        float maxY = bounds.max.y - paddingY;
        float clampedWaveHeight = Mathf.Min(waveHeight, Mathf.Max(0f, (maxY - minY) * 0.5f));
        float pathCenterY = bounds.center.y;
        float pathHalfHeight = clampedWaveHeight;

        if (useSwimLane)
        {
            pathCenterY = Mathf.Lerp(minY, maxY, Mathf.Clamp01(swimLaneCenter01));
            float laneHalfHeight = (maxY - minY) * Mathf.Clamp(swimLaneHeight01, 0.05f, 1f) * 0.5f;
            pathHalfHeight = Mathf.Min(clampedWaveHeight, laneHalfHeight);
        }

        float startX = Mathf.Clamp(transform.position.x, minX, maxX);
        float startY = Mathf.Clamp(transform.position.y, minY, maxY);
        float endX = currentDirection > 0 ? maxX : minX;

        // Mỗi segment chỉ dùng một cung uốn lớn, thay vì random Y độc lập ở từng waypoint.
        // Cách này loại bỏ zigzag và tạo nhịp bơi chậm, dễ theo dõi bằng mắt.
        float targetY = pathCenterY + Random.Range(-pathHalfHeight * 0.45f, pathHalfHeight * 0.45f);
        targetY = Mathf.Clamp(targetY, minY, maxY);
        float arcDirection = Random.value < 0.5f ? -1f : 1f;
        float arcAmplitude = Random.Range(0.25f, 0.65f) * pathHalfHeight * arcDirection;

        for (int i = 0; i < waypointCount; i++)
        {
            float t = (float)(i + 1) / waypointCount;
            float x = Mathf.Lerp(startX, endX, t);
            float smoothT = t * t * (3f - 2f * t);
            float y = Mathf.Lerp(startY, targetY, smoothT) + Mathf.Sin(t * Mathf.PI) * arcAmplitude;
            y = Mathf.Clamp(y, minY, maxY);
            path[i] = new Vector3(x, y, transform.position.z);
        }

        return path;
    }

    float CalculatePathLength(Vector3[] path)
    {
        float length = 0f;
        Vector3 previous = transform.position;

        for (int i = 0; i < path.Length; i++)
        {
            length += Vector3.Distance(previous, path[i]);
            previous = path[i];
        }

        return Mathf.Max(length, 0.01f);
    }

    void ApplyFacingDirection()
    {
        float baseScale = Mathf.Abs(transform.localScale.x);
        float targetScaleX = facingDirection > 0
            ? (spriteFacesRight ? baseScale : -baseScale)
            : (spriteFacesRight ? -baseScale : baseScale);

        transform.localScale = new Vector3(targetScaleX, transform.localScale.y, transform.localScale.z);
    }

    void UpdateFishTilt()
    {
        Vector3 movement = transform.position - lastPos;

        if (movement.sqrMagnitude > 0.0001f)
        {
            // Dùng vận tốc X thực tế để xác nhận hướng nhìn. Việc này xử lý cả sai lệch nhỏ
            // khi spline chuyển đoạn/overshoot thay vì chỉ tin vào hướng dự kiến của segment.
            if (Mathf.Abs(movement.x) > 0.0001f)
            {
                int movementDirection = movement.x > 0f ? 1 : -1;
                if (movementDirection != facingDirection)
                {
                    facingDirection = movementDirection;
                    ApplyFacingDirection();
                }
            }

            // Hướng trái/phải được khóa theo segment. Dao động nhỏ của đường cong
            // không còn làm model lật mặt giữa đường.
            float pathAngle = Mathf.Atan2(movement.y, Mathf.Abs(movement.x)) * Mathf.Rad2Deg;

            // Các đoạn đi xuống thường ngắn và thoải, nên góc âm dễ bị phần smoothing
            // làm gần như không nhìn thấy. Tăng nhẹ độ chúi và giữ một góc tối thiểu
            // khi cá thực sự đang hạ độ cao.
            if (movement.y < -0.0001f)
            {
                float downwardAngle = Mathf.Abs(pathAngle) * downwardTiltMultiplier;
                pathAngle = -Mathf.Max(downwardAngle, minimumDownwardTilt);
            }

            // Khi cá quay trái, cùng một góc quay dương sẽ khiến mũi chúi xuống.
            // Đảo dấu theo hướng bơi để mũi luôn ngẩng khi dy > 0 và chúi khi dy < 0.
            float angle = pathAngle * facingDirection;
            angle = Mathf.Clamp(angle, -maxTiltAngle, maxTiltAngle);
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            float responseSpeed = movement.y < 0f ? tiltSmoothSpeed * 1.5f : tiltSmoothSpeed;
            float smoothing = 1f - Mathf.Exp(-responseSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothing);

            // Re gap (goc nghieng lon) thi vay duoi manh hon, giong ca that dung
            // duoi de doi huong thay vi giu bien do deu bat ke dang re hay boi thang.
            if (bodyMaterial != null && bodyMaterial.HasProperty("_WaveAmplitude"))
            {
                float turnRatio = Mathf.Abs(angle) / Mathf.Max(1f, maxTiltAngle);
                bodyMaterial.SetFloat("_WaveAmplitude", waveAmplitude * (1f + turnAmplitudeBoost * turnRatio));
            }
        }

        lastPos = transform.position;
    }

    void Die()
    {
        if (isDying)
            return;

        isDying = true;
        if (currentPathTween != null && currentPathTween.IsActive())
            currentPathTween.Kill();
        twistScheduleTween?.Kill();

        if (bodyMaterial != null && bodyMaterial.HasProperty("_BaseColor"))
        {
            fadeOutTween = DOTween.To(
                    () => bodyMaterial.GetColor("_BaseColor").a,
                    alpha =>
                    {
                        Color color = bodyMaterial.GetColor("_BaseColor");
                        color.a = alpha;
                        bodyMaterial.SetColor("_BaseColor", color);
                    },
                    0f,
                    fadeOutDuration)
                .OnComplete(() => Destroy(gameObject));
        }
        else
        {
            Destroy(gameObject, fadeOutDuration);
        }
    }

    void ClampPositionToBounds()
    {
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
        position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
        transform.position = position;
    }

    void OnDestroy()
    {
        transform.DOKill();
        fadeOutTween?.Kill();
        twistScheduleTween?.Kill();
    }

    void OnDrawGizmosSelected()
    {
        if (swimBounds == null)
            return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawCube(swimBounds.bounds.center, swimBounds.bounds.size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(swimBounds.bounds.center, swimBounds.bounds.size);
    }
}
