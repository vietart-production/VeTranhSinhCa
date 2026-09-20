using System.Collections;
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
    [Tooltip("false: song vẫy dọc theo trục đầu-đuôi (đa số cá). true: song toả tròn từ tâm ra viền, dùng cho sinh vật không có trục đầu-đuôi (sao biển, cua).")]
    public bool useRadialDeform;
    [Tooltip("Chỉ dùng khi Use Radial Deform bật: số 'cánh' toả quanh tâm.")]
    [Min(1f)] public float radialArmCount = 5f;
    [Tooltip("Bật cho loài có textureRotationDegrees=90 (sứa, cá mực, cá ngựa): trục đầu-đuôi thật nằm dọc theo UV.y sau khi ảnh bị xoay, không phải UV.x mặc định.")]
    public bool useVerticalBodyAxis;
    [Min(0f)] public float waveAmplitude = 0.3f;
    [Tooltip("Tần số vẫy = swimSpeed * hệ số này.")]
    [Min(0f)] public float waveFrequencyPerSpeed = 1.4f;
    public float waveLength = 2.5f;
    [Tooltip("Đuôi vẫy mạnh thêm bao nhiêu % khi cá đang rẽ gấp (theo góc nghiêng thực tế).")]
    [Range(0f, 2f)] public float turnAmplitudeBoost = 0.6f;
    [Tooltip("0..1: trước ngưỡng này (tính từ đầu) gần như đứng yên, chỉ đoạn cuối đuôi mới vẫy.")]
    [Range(0f, 0.95f)] public float tailWaveStart = 0.72f;

    [Header("Vẫy vây ngực (gần đầu, độc lập với đuôi)")]
    [Tooltip("Vị trí vây ngực dọc thân, 0 = đầu, 1 = đuôi.")]
    [Range(0f, 1f)] public float finBandCenter = 0.18f;
    [Range(0.02f, 0.5f)] public float finBandWidth = 0.12f;
    [Min(0f)] public float finWaveFrequency = 6f;
    [Tooltip("0 = tắt hẳn (mặc định). Đặt > 0 để phần đầu không còn đứng im hoàn toàn.")]
    [Min(0f)] public float finWaveAmplitude;

    [Header("Nhịp bơi")]
    [Tooltip("Biến thiên tốc độ mỗi đoạn bơi, dùng Perlin noise chậm thay vì random đột ngột.")]
    [Range(0f, 0.6f)] public float speedVariation = 0.25f;
    [Min(0.01f)] public float speedNoiseFrequency = 0.15f;

    [Header("Xoay tròn Z ngẫu nhiên (giống Cua), dùng chung cho mọi loài")]
    [Tooltip("Tắt hẳn cú xoay tròn 360 độ cho những loài không hợp (cá mập, rùa, cua dùng script riêng...).")]
    public bool enableBodyTwist = true;
    [Tooltip("Không còn dùng để tạo hiệu ứng xoay (chỉ còn cộng vào khoảng nghỉ giữa 2 lần xoay). Giữ lại để không mất giá trị đã tinh chỉnh trên các prefab.")]
    [Min(0.05f)] public float twistTravelDuration = 0.5f;
    [Tooltip("Thời gian 1 vòng xoay tròn Z diễn ra (chỉ có tác dụng với loài đọc SpinOffsetAngle).")]
    [Min(0.05f)] public float twistSpinDuration = 0.35f;
    [Tooltip("Không còn dùng để tạo hiệu ứng xoay. Giữ lại để không mất giá trị đã tinh chỉnh trên các prefab.")]
    [Min(0.5f)] public float twistFlailCycles = 2.5f;
    [Tooltip("Không còn dùng để tạo hiệu ứng xoay. Giữ lại để không mất giá trị đã tinh chỉnh trên các prefab.")]
    [Range(5f, 90f)] public float twistMaxAngleDegrees = 35f;
    [Tooltip("Khoảng thời gian ngẫu nhiên nghỉ thêm giữa 2 lần xoay tròn.")]
    public Vector2 twistRestIntervalRange = new Vector2(2.5f, 6f);

    [Header("Thời gian tồn tại")]
    public float lifetime = 15f;
    [Min(0f)] public float fadeOutDuration = 1f;
    [Tooltip("Khi hết vòng đời, cá rơi tõm xuống dưới đáy vùng bơi thay vì mờ dần tại chỗ. Khoảng cách rơi thêm qua khỏi đáy.")]
    [Min(0f)] public float sinkBelowBoundsMargin = 3f;

    [Header("Bong bóng (dùng chung OceanBubbleSystem trong scene)")]
    [Tooltip("Số bong bóng phun ra lúc cá vừa chạm mặt nước khi được thả xuống.")]
    [Min(0)] public int waterEntryBubbleCount = 18;
    [Tooltip("Số bong bóng phun ra lúc cá bắt đầu chìm/despawn.")]
    [Min(0)] public int despawnBubbleCount = 10;

    private static Mesh sharedBodyMesh;

    private Vector3 lastPos;
    private Bounds bounds;
    private float lifeTimer;
    private bool isDying;
    private Material bodyMaterial;
    private float motionSeed;
    private float currentSwimSpeed;
    private Tween twistScheduleTween;
    private Tween spinTween;
    private float currentTiltAngle;
    private float spinOffsetAngle;
    private Tween fadeOutTween;
    private Tween currentPathTween;
    private Coroutine dropBubbleCoroutine;
    private int currentDirection = -1;
    private int facingDirection = -1;
    private bool dropPrepared;
    private Vector3 dropLandingPosition;
    private float dropWaterSurfaceY;
    private float dropAirPhaseRatio = 0.28f;
    private float dropSettlePhaseRatio = 0.2f;
    private float dropSinkOvershoot = 0.22f;
    private float dropImpactTilt = 9f;
    private bool initialized;
    private float startledSpeedMultiplier = 1f;

    // Doc boi cac script chuyen dong rieng (Seahorse/Jellyfish...) de cong them
    // vao goc xoay Z cua chinh chung - component nay co the dang bi disable
    // (loai dung bespoke motion) nhung PlaySpin() van chay binh thuong vi no
    // chi la 1 DOTween.To() tren field, khong phu thuoc Update()/enabled.
    public float SpinOffsetAngle => spinOffsetAngle;

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
        float impactTilt)
    {
        dropPrepared = true;
        dropLandingPosition = landingPosition;
        dropWaterSurfaceY = waterSurfaceY;
        dropDuration = Mathf.Max(0.2f, duration);
        dropAirPhaseRatio = Mathf.Clamp(airPhaseRatio, 0.1f, 0.55f);
        dropSettlePhaseRatio = Mathf.Clamp(settlePhaseRatio, 0.1f, 0.4f);
        dropSinkOvershoot = Mathf.Max(0f, sinkOvershoot);
        dropImpactTilt = Mathf.Clamp(impactTilt, 0f, 25f);
    }

    public void StopSwimmingImmediately()
    {
        if (currentPathTween != null && currentPathTween.IsActive())
            currentPathTween.Kill();

        currentPathTween = null;
        twistScheduleTween?.Kill();
        spinTween?.Kill();
        transform.DOKill();
        enabled = false;
    }

    // Bi cham/click: KHONG roi quy dao dang boi (tranh phai ghep lai sau, tung
    // gay giat/lech vi tri). Chi dao nguoc huong tren chinh duong bang dang di
    // (StartNewSwimSegment(true) tu dao currentDirection va nham ve dung mep
    // A/B vua xuat phat) va tang toc rut lui trong dung 1 doan boi do.
    public void StartleReverse(float speedMultiplier, float waveFrequencyMultiplier)
    {
        if (isDying)
            return;

        startledSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        StartNewSwimSegment(true);
        // Goi sau StartNewSwimSegment vi ham do tu reset _WaveFrequency ve 1x
        // o dau; muon "vay hoang loan" nhanh hon phai ghi de sau khi no chay xong.
        SetWaveFrequencyMultiplier(Mathf.Max(1f, waveFrequencyMultiplier));
        if (enableBodyTwist)
            PlaySpin();
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
        currentTiltAngle = 0f;
        spinOffsetAngle = 0f;
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
        // Gia tri "an toan" ngay tu dau, phong khi component bi vo hieu hoa vinh
        // vien (cac loai dung script rieng nhu cua/tom/ca_muc) va khong bao gio
        // chay toi Start()/StartNewSwimSegment() de tinh lai theo swimSpeed thuc
        // te - neu khong _WaveFrequency se giu nguyen gia tri cu con sot lai tu
        // lan Play trươc trong file .mat.
        if (bodyMaterial.HasProperty("_WaveFrequency"))
            bodyMaterial.SetFloat("_WaveFrequency", waveFrequencyPerSpeed);
        if (bodyMaterial.HasProperty("_DeformMode"))
            bodyMaterial.SetFloat("_DeformMode", useRadialDeform ? 1f : 0f);
        if (bodyMaterial.HasProperty("_ArmCount"))
            bodyMaterial.SetFloat("_ArmCount", radialArmCount);
        if (bodyMaterial.HasProperty("_BodyAxis"))
            bodyMaterial.SetFloat("_BodyAxis", useVerticalBodyAxis ? 1f : 0f);
        if (bodyMaterial.HasProperty("_WavePhase"))
            bodyMaterial.SetFloat("_WavePhase", motionSeed);
        if (bodyMaterial.HasProperty("_TailWaveStart"))
            bodyMaterial.SetFloat("_TailWaveStart", tailWaveStart);
        if (bodyMaterial.HasProperty("_FinBandCenter"))
            bodyMaterial.SetFloat("_FinBandCenter", finBandCenter);
        if (bodyMaterial.HasProperty("_FinBandWidth"))
            bodyMaterial.SetFloat("_FinBandWidth", finBandWidth);
        if (bodyMaterial.HasProperty("_FinWaveFrequency"))
            bodyMaterial.SetFloat("_FinWaveFrequency", finWaveFrequency);
        if (bodyMaterial.HasProperty("_FinWaveAmplitude"))
            bodyMaterial.SetFloat("_FinWaveAmplitude", finWaveAmplitude);
        if (bodyMaterial.HasProperty("_TwistTravelDuration"))
            bodyMaterial.SetFloat("_TwistTravelDuration", twistTravelDuration);
        if (bodyMaterial.HasProperty("_TwistSpinDuration"))
            bodyMaterial.SetFloat("_TwistSpinDuration", twistSpinDuration);
        if (bodyMaterial.HasProperty("_TwistFlailCycles"))
            bodyMaterial.SetFloat("_TwistFlailCycles", twistFlailCycles);
        if (bodyMaterial.HasProperty("_TwistMaxAngle"))
            bodyMaterial.SetFloat("_TwistMaxAngle", twistMaxAngleDegrees);
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

    // Lich hen gio cho lan lac-minh (PlaySpin) tiep theo. Thay cho hieu ung
    // xoan-quay-quay bang shader (_TwistTriggerTime) truoc day.
    void ScheduleNextTwist()
    {
        twistScheduleTween?.Kill();
        if (!enableBodyTwist)
            return;

        float restDelay = Random.Range(twistRestIntervalRange.x, twistRestIntervalRange.y);
        float nextDelay = twistTravelDuration + twistSpinDuration + restDelay;
        twistScheduleTween = DOVirtual.DelayedCall(nextDelay, PlaySpin);
    }

    // Khong dung Update()/Start() - nen loai dung script chuyen dong rieng
    // (cua/tom/ca_ngua/sua, component nay bi disable) van goi duoc de co
    // chung "chuc nang xoay" thay vi chi cac loai boi thuong DOTweenFishAnim.
    public void EnsureAmbientTwistScheduled()
    {
        if (twistScheduleTween == null || !twistScheduleTween.IsActive())
            ScheduleNextTwist();
    }

    // Xoay tron 1 vong quanh Z roi tu tat, kieu "pingpong" cua Cua. Dung
    // chung cho moi loai qua SpinOffsetAngle: UpdateFishTilt (ca boi thuong)
    // cong truc tiep, Seahorse/Jellyfish tu doc va cong vao goc cua chinh ho.
    public void PlaySpin()
    {
        if (isDying)
            return;

        spinTween?.Kill();
        float spinDirection = Random.value < 0.5f ? 1f : -1f;
        spinOffsetAngle = 0f;
        spinTween = DOTween.To(
                () => spinOffsetAngle,
                value => spinOffsetAngle = value,
                360f * spinDirection,
                Mathf.Max(0.1f, twistSpinDuration))
            .SetEase(Ease.OutBack)
            .OnComplete(() => spinOffsetAngle = 0f);

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

        if (dropBubbleCoroutine != null)
            StopCoroutine(dropBubbleCoroutine);

        Sequence dropSequence = DOTween.Sequence();
        dropSequence.Append(
            transform.DOMove(waterEntryPosition, airDuration)
                .SetEase(Ease.InQuad));
        dropSequence.Join(
            transform.DORotate(new Vector3(0f, 0f, entryTilt), airDuration)
                .SetEase(Ease.InSine));
        dropSequence.AppendCallback(() => EmitBubbleBurst(waterEntryPosition, waterEntryBubbleCount, 1f, 1.2f));
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
            if (dropBubbleCoroutine != null)
            {
                StopCoroutine(dropBubbleCoroutine);
                dropBubbleCoroutine = null;
            }

            currentPathTween = null;
            currentTiltAngle = 0f;
            transform.rotation = Quaternion.identity;
            StartNewSwimSegment(false);
        });

        dropBubbleCoroutine = StartCoroutine(EmitBubbleTrailDuringDrop(safeDuration, waterEntryPosition, dropLandingPosition));
        currentPathTween = dropSequence;
    }

    IEnumerator EmitBubbleTrailDuringDrop(float totalDuration, Vector3 startPosition, Vector3 endPosition)
    {
        if (totalDuration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            float t = Mathf.Clamp01(elapsed / totalDuration);
            Vector3 bubblePosition = Vector3.Lerp(startPosition, endPosition, t);
            bubblePosition.x += UnityEngine.Random.Range(-0.12f, 0.12f);
            bubblePosition.y += UnityEngine.Random.Range(-0.08f, 0.15f);

            int burstCount = Mathf.Clamp(Mathf.RoundToInt(waterEntryBubbleCount * 0.35f), 1, 5);
            EmitBubbleBurst(bubblePosition, burstCount, 0.6f, 0.8f);

            float delay = Mathf.Lerp(0.14f, 0.32f, Mathf.Clamp01(t));
            elapsed += delay;
            yield return new WaitForSeconds(delay);
        }

        dropBubbleCoroutine = null;
    }

    // Dung chung OceanBubbleSystem cua ca be (touch/hold bubbles) thay vi tu dung
    // 1 ParticleSystem rieng moi lan - vua dong nhat hinh anh bong bong trong toan
    // bo scene, vua tan dung pool san co thay vi Instantiate/Destroy GameObject.
    void EmitBubbleBurst(Vector3 position, int count, float radiusMul, float energyMul)
    {
        if (count <= 0)
            return;

        OceanBubbleSystem bubbleSystem = UnityEngine.Object.FindFirstObjectByType<OceanBubbleSystem>();
        bubbleSystem?.Burst(position, count, radiusMul, energyMul);
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
        currentSwimSpeed = swimSpeed * (1f + speedNoise * speedVariation) * startledSpeedMultiplier;
        startledSpeedMultiplier = 1f; // chi ap dung cho dung 1 doan vua kich hoat
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
            float responseSpeed = movement.y < 0f ? tiltSmoothSpeed * 1.5f : tiltSmoothSpeed;
            float smoothing = 1f - Mathf.Exp(-responseSpeed * Time.deltaTime);
            currentTiltAngle = Mathf.LerpAngle(currentTiltAngle, angle, smoothing);
            transform.rotation = Quaternion.Euler(0f, 0f, currentTiltAngle + spinOffsetAngle);

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
        spinTween?.Kill();

        // Roi tom xuong duoi day vung boi (khuat khoi khung hinh) thay vi mo
        // dan tai cho - giong ca that mat suc noi va chim xuong khi "bien mat".
        float targetY = bounds.min.y - sinkBelowBoundsMargin;
        Vector3 sinkTarget = new Vector3(transform.position.x, targetY, transform.position.z);
        float fallDuration = Mathf.Max(0.3f, fadeOutDuration);
        float tumbleSign = Random.value < 0.5f ? 1f : -1f;

        // Vai bong bong thoat ra ngay luc bat dau chim - nhu hoi thu cuoi truoc khi
        // "bien mat" khoi tam nhin, thay vi lang le mo dan khong dau hieu gi.
        EmitBubbleBurst(transform.position, despawnBubbleCount, 0.8f, 0.9f);

        Sequence dieSequence = DOTween.Sequence();
        dieSequence.Append(transform.DOMove(sinkTarget, fallDuration).SetEase(Ease.InQuad));
        dieSequence.Join(
            transform.DORotate(new Vector3(0f, 0f, 55f * tumbleSign), fallDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.InSine));

        if (bodyMaterial != null && bodyMaterial.HasProperty("_BaseColor"))
        {
            Color startColor = bodyMaterial.GetColor("_BaseColor");
            dieSequence.Join(
                DOTween.To(
                        () => startColor.a,
                        alpha =>
                        {
                            Color color = bodyMaterial.GetColor("_BaseColor");
                            color.a = alpha;
                            bodyMaterial.SetColor("_BaseColor", color);
                        },
                        0f,
                        fallDuration * 0.5f)
                    .SetDelay(fallDuration * 0.5f));
        }

        dieSequence.OnComplete(() => Destroy(gameObject));
        fadeOutTween = dieSequence;
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
        if (dropBubbleCoroutine != null)
        {
            StopCoroutine(dropBubbleCoroutine);
            dropBubbleCoroutine = null;
        }

        transform.DOKill();
        fadeOutTween?.Kill();
        twistScheduleTween?.Kill();
        spinTween?.Kill();
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
