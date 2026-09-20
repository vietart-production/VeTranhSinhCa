using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BackgroundFishSpawner : MonoBehaviour
{
    [Header("Khu vực xuất hiện")]
    public BoxCollider2D spawnBounds;

    [Header("Đàn cá")]
    public List<GameObject> fishPrefabs;
    [Min(0)] public int numberOfFishToSpawn = 40;

    [Header("Kich thuoc ca 2D")]
    [Tooltip("He so kich thuoc tong cho tat ca ca 2D. 1 = kich thuoc goc, 1.3 = lon hon 30%.")]
    [Range(0.25f, 2.5f)] public float fishSizeMultiplier = 0.65f;

    [Header("Texture cá mặc định")]
    [Tooltip("Chỉ prefab có ít nhất một ảnh khớp tên trong thư mục này mới được spawn.")]
    public string defaultTextureFolderPath = "Assets/default_fish";
    public bool requireDefaultTexture = true;

    [Header("Do sang cua ca")]
    [Tooltip("0 = tat emission. Nen bat dau trong khoang 0.2 - 0.5.")]
    [Range(0f, 2f)] public float fishEmissionIntensity = 0.3f;
    [Tooltip("Mau nhan emission. Mau trang giu nguyen mau texture goc.")]
    [ColorUsage(false, true)] public Color fishEmissionTint = Color.white;

    [Header("Ca con mac dinh")]
    [Tooltip("Texture ca_con hien tai la line-art trang/den. Mau nay phu len phan than trang.")]
    public Color caConLineArtTint = new Color(0.78f, 0.86f, 0.95f, 1f);

    [Header("Sua duong noi UV ca voi")]
    [Tooltip("Mirror ngan texture lay mau tu mep doi dien khi UV ca voi vuot bien.")]
    public TextureWrapMode whaleTextureWrapMode = TextureWrapMode.Mirror;
    [Tooltip("Thu nhe vung UV vao trong de tranh lay mau ngay sat bien texture.")]
    [Range(0f, 0.03f)] public float whaleUvInset = 0.004f;

    [Header("Cá con đi theo đàn")]
    [Range(0f, 0.75f)] public float schoolingFishRatio = 0.3f;
    [Min(2)] public int schoolMinSize = 4;
    [Min(2)] public int schoolMaxSize = 6;
    public Vector2 schoolFishWidthRange = new Vector2(0.55f, 0.8f);
    [Min(0.1f)] public float schoolMemberSpacing = 0.75f;

    [Header("Bố cục chuyển động")]
    [Min(3)] public int swimLaneCount = 8;
    [Range(0.05f, 0.4f)] public float swimLaneHeight = 0.16f;
    [Min(0f)] public float minimumVisualSpacing = 1.25f;
    public Vector2 targetFishWidthRange = new Vector2(1.35f, 2.15f);
    public Vector2 calmSpeedRange = new Vector2(1.05f, 1.55f);
    public Vector2 calmWaveHeightRange = new Vector2(0.45f, 0.9f);

    [Header("Sao bien")]
    [Tooltip("Chieu rong rieng cua sao bien; khong dung kich thuoc ca lon.")]
    public Vector2 starfishWidthRange = new Vector2(0.55f, 0.85f);
    [Tooltip("Khoang do cao gan day, tinh theo 0..1 cua Spawn Bounds.")]
    public Vector2 starfishBottomHeightRange = new Vector2(0.06f, 0.28f);
    [Range(0.1f, 1f)] public float starfishSpeedMultiplier = 0.4f;
    [Range(0.02f, 0.4f)] public float starfishWaveHeight = 0.14f;

    [Header("Sua & Ca muc (boi lon song len xuong khap ho, khong gioi han lan boi)")]
    [Tooltip("Wave height lon de ca lac len xuong gan het chieu cao ho thay vi bo hep trong 1 lan boi.")]
    [Min(1f)] public float fullRangeRoamWaveHeight = 5f;

    [Header("Cua (bo ngang duoi day, khong boi)")]
    [Tooltip("Khoang do cao gan day, tinh theo 0..1 cua Spawn Bounds.")]
    public Vector2 crabBottomHeightRange = new Vector2(0.14f, 0.38f);
    [Tooltip("Cua chi bo qua lai trong 1 vung gan diem spawn, khong di het be.")]
    [Min(0.5f)] public float crabPatrolHalfWidth = 1.8f;

    [Header("Ca ngua (dung thang, boi ngang be nhu ca thuong)")]
    [Tooltip("Bien do troi len xuong quanh do cao hien tai khi boi ngang.")]
    [Min(0.3f)] public float seahorsePatrolRadius = 1.0f;

    [Header("Co nha tai tro")]
    public bool sponsorFlagEnabled = true;
    public Sprite sponsorFlagSprite;
    [Min(0f)] public float sponsorFlagInitialDelay = 6f;
    public Vector2 sponsorFlagVisibleDurationRange = new Vector2(10f, 16f);
    public Vector2 sponsorFlagIntervalRange = new Vector2(18f, 32f);
    [Range(0.3f, 1.2f)] public float sponsorFlagWidthRatio = 0.68f;
    [Tooltip("Offset theo ti le rong/cao cua ca.")]
    public Vector2 sponsorFlagAnchorOffset = new Vector2(-0.08f, -0.03f);
    [Tooltip("Do sau chan cot co cam vao lung ca, theo ti le chieu cao ca.")]
    [Range(0f, 0.5f)] public float sponsorFlagBackInset = 0.22f;
    [Range(0.2f, 2f)] public float sponsorFlagRaiseDuration = 0.7f;

    [Header("Tuong tac click vao ca")]
    [Tooltip("Material bong bong URP dung cho vu no va dai bong bong.")]
    public Material clickBubbleMaterial;
    [Tooltip("Ca tang toc gap bao nhieu lan khi quay dau ve lai mep vua xuat phat - can du manh de ro rang la dang CHAY chu khong phai boi thong thuong.")]
    [Range(1f, 5f)] public float clickStartleSpeedMultiplier = 3f;
    [Tooltip("Dai bong bong con lai bao lau sau khi bi cham, cung la thoi gian cooldown truoc khi co the click lai.")]
    [Range(0.5f, 3f)] public float clickBubbleTrailDuration = 1.2f;
    [Range(1f, 4f)] public float clickStartleAnimationSpeed = 2.2f;
    [Range(4, 30)] public int clickBubbleBurstCount = 16;
    [Tooltip("Bat/tat dai bong bong de lai phia sau ca khi bo chay.")]
    public bool clickEscapeBubbleTrailEnabled = true;
    [Range(2f, 40f)] public float clickBubbleTrailRate = 20f;
    [Range(0.5f, 2f)] public float clickBubbleTrailSizeMultiplier = 1.15f;
    [Range(0.02f, 0.3f)] public float clickBubbleTrailSpread = 0.1f;
    public Vector2 clickBubbleSizeRange = new Vector2(0.1f, 0.22f);
    public Color clickBubbleColor = new Color(0.72f, 0.94f, 1f, 0.78f);

    [Header("Chiều sâu")]
    [Tooltip("Khoảng Z của đàn cá. Z nhỏ hơn nằm gần camera hơn trong scene hiện tại.")]
    public Vector2 depthRange = new Vector2(-8f, -2f);
    public Transform background;
    [Min(0.05f)] public float backgroundSafetyDistance = 0.5f;
    public Vector2 depthScaleRange = new Vector2(1.15f, 0.72f);
    public Vector2 depthSpeedRange = new Vector2(1.1f, 0.7f);

    private readonly List<Vector2> occupiedScreenPositions = new List<Vector2>();
    private readonly List<GameObject> sponsorFlagCandidates = new List<GameObject>();
    private sealed class TexturedFishSource
    {
        public GameObject prefab;
        public string texturePath;
        public string fishId;
    }

    void Update()
    {
        if (TryReadPrimaryPointerPress(out Vector2 pressedPosition))
        {
            FishClickInteraction.TryTriggerAtScreenPosition(Camera.main, pressedPosition);
            return;
        }

        if (TryReadPrimaryPointerHold(out Vector2 heldPosition))
            FishClickInteraction.TryTriggerAtScreenPosition(Camera.main, heldPosition);
    }

    static bool TryReadPrimaryPointerPress(out Vector2 screenPosition)
    {
        screenPosition = default;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        return false;
    }

    static bool TryReadPrimaryPointerHold(out Vector2 screenPosition)
    {
        screenPosition = default;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        return false;
    }

    void Start()
    {
        if (spawnBounds == null)
        {
            Debug.LogError("[BackgroundFishSpawner] Chưa gán Spawn Bounds.");
            return;
        }

        if (fishPrefabs == null || fishPrefabs.Count == 0)
        {
            Debug.LogError("[BackgroundFishSpawner] Cần ít nhất một prefab cá.");
            return;
        }

        if (background == null)
        {
            GameObject backgroundObject = GameObject.Find("Background");
            if (backgroundObject != null)
                background = backgroundObject.transform;
        }

        List<TexturedFishSource> texturedSources = BuildTexturedFishSources();
        if (requireDefaultTexture && texturedSources.Count == 0)
        {
            Debug.LogError("[BackgroundFishSpawner] Không tìm thấy cặp prefab/texture hợp lệ trong " +
                           ResolveFolderPath(defaultTextureFolderPath));
            return;
        }

        SpawnSchoolOfFish(texturedSources);
    }

    void SpawnSchoolOfFish(List<TexturedFishSource> texturedSources)
    {
        Bounds bounds = spawnBounds.bounds;
        ResolveDepthRange(out float nearZ, out float farZ);
        occupiedScreenPositions.Clear();
        sponsorFlagCandidates.Clear();

        TexturedFishSource smallFishSource = FindSmallFishSource(texturedSources);
        int schoolingFishCount = smallFishSource == null
            ? 0
            : Mathf.RoundToInt(numberOfFishToSpawn * schoolingFishRatio);

        if (schoolingFishCount > 0 && schoolingFishCount < schoolMinSize)
            schoolingFishCount = Mathf.Min(schoolMinSize, numberOfFishToSpawn);

        int spawnedCount = SpawnSmallFishSchools(
            smallFishSource, schoolingFishCount, bounds, nearZ, farZ);

        List<TexturedFishSource> individualSources = GetIndividualSources(texturedSources, smallFishSource);
        Shuffle(individualSources);
        int sourceIndex = 0;
        while (spawnedCount < numberOfFishToSpawn && individualSources.Count > 0)
        {
            int laneIndex = spawnedCount % Mathf.Max(3, swimLaneCount);
            Vector3 position = FindComfortablePosition(bounds, nearZ, farZ, laneIndex);
            // Đi tuần tự qua danh sách đã xáo trộn để mọi ảnh màu đều xuất hiện
            // ít nhất một lần trước khi dùng lại một biến thể.
            TexturedFishSource source = individualSources[sourceIndex % individualSources.Count];
            if (source.fishId == "sao_bien")
            {
                float bottom01 = Random.Range(
                    Mathf.Min(starfishBottomHeightRange.x, starfishBottomHeightRange.y),
                    Mathf.Max(starfishBottomHeightRange.x, starfishBottomHeightRange.y));
                position.y = Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(bottom01));
            }
            else if (source.fishId == "cua")
            {
                float bottom01 = Random.Range(
                    Mathf.Min(crabBottomHeightRange.x, crabBottomHeightRange.y),
                    Mathf.Max(crabBottomHeightRange.x, crabBottomHeightRange.y));
                position.y = Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(bottom01));
            }
            SpawnIndividualFish(source, position, laneIndex, nearZ, farZ, spawnedCount);
            sourceIndex++;
            spawnedCount++;
        }

        if (sponsorFlagEnabled && sponsorFlagSprite != null && sponsorFlagCandidates.Count > 0)
            StartCoroutine(SponsorFlagRoutine());

        Debug.Log($"[BackgroundFishSpawner] Đã tạo {spawnedCount}/{numberOfFishToSpawn} cá, " +
                  $"gồm {schoolingFishCount} ca_con đi theo đàn.");
    }

    int SpawnSmallFishSchools(
        TexturedFishSource source,
        int requestedFishCount,
        Bounds bounds,
        float nearZ,
        float farZ)
    {
        if (source == null || source.prefab == null || requestedFishCount <= 0)
            return 0;

        int spawned = 0;
        int schoolIndex = 0;
        int safeMin = Mathf.Max(2, Mathf.Min(schoolMinSize, schoolMaxSize));
        int safeMax = Mathf.Max(safeMin, schoolMaxSize);

        while (spawned < requestedFishCount)
        {
            int remaining = requestedFishCount - spawned;
            int groupSize = Mathf.Min(Random.Range(safeMin, safeMax + 1), remaining);

            if (remaining - groupSize == 1 && groupSize > safeMin)
                groupSize--;

            int laneIndex = (schoolIndex * 3 + 1) % Mathf.Max(3, swimLaneCount);
            Vector3 schoolPosition = FindComfortablePosition(bounds, nearZ, farZ, laneIndex);
            CreateSmallFishSchool(source, groupSize, schoolPosition, laneIndex, schoolIndex, nearZ, farZ);

            spawned += groupSize;
            schoolIndex++;
        }

        return spawned;
    }

    void CreateSmallFishSchool(
        TexturedFishSource source,
        int groupSize,
        Vector3 position,
        int laneIndex,
        int schoolIndex,
        float nearZ,
        float farZ)
    {
        GameObject schoolRoot = new GameObject($"CaConSchool_{schoolIndex}");
        schoolRoot.transform.SetParent(transform);
        schoolRoot.transform.position = position;

        DOTweenFishAnim prefabAnimation = source.prefab.GetComponent<DOTweenFishAnim>();
        DOTweenFishAnim schoolAnimation = schoolRoot.AddComponent<DOTweenFishAnim>();
        ConfigureCalmAnimation(schoolAnimation, laneIndex, position.z, nearZ, farZ, schoolIndex);
        bool schoolFacesRight = prefabAnimation != null && prefabAnimation.spriteFacesRight;
        schoolAnimation.spriteFacesRight = schoolFacesRight;
        schoolAnimation.waveHeight = Mathf.Min(schoolAnimation.waveHeight, 0.55f);
        schoolAnimation.swimLaneHeight01 = Mathf.Min(swimLaneHeight, 0.12f);

        for (int i = 0; i < groupSize; i++)
        {
            GameObject member = Instantiate(source.prefab, schoolRoot.transform);
            member.name = $"ca_con_{schoolIndex}_{i}_{Path.GetFileNameWithoutExtension(source.texturePath)}";

            ApplyDefaultTexture(member, source);

            DOTweenFishAnim memberAnimation = member.GetComponent<DOTweenFishAnim>();
            if (memberAnimation != null)
                memberAnimation.enabled = false;

            member.transform.localPosition = GetSchoolFormationOffset(i, schoolFacesRight);
            member.transform.localRotation = Quaternion.identity;
            NormalizeVisualWidth(
                member,
                Random.Range(schoolFishWidthRange.x, schoolFishWidthRange.y) * fishSizeMultiplier);
            ConfigureClickInteraction(member);
        }
    }

    Vector3 GetSchoolFormationOffset(int index, bool modelFacesRight)
    {
        if (index == 0)
            return Vector3.zero;

        int rank = (index + 1) / 2;
        float side = index % 2 == 1 ? 1f : -1f;
        // X cục bộ luôn nằm phía sau đầu cá gốc. Khi root lật hướng,
        // cả đội hình cũng tự lật và vẫn theo sau con dẫn đầu.
        float trailDirection = modelFacesRight ? -1f : 1f;
        float x = trailDirection * rank * schoolMemberSpacing;
        float y = side * rank * schoolMemberSpacing * 0.42f;
        float z = rank * 0.015f;
        return new Vector3(x, y, z);
    }

    void SpawnIndividualFish(
        TexturedFishSource source,
        Vector3 position,
        int laneIndex,
        float nearZ,
        float farZ,
        int fishIndex)
    {
        GameObject fish = Instantiate(source.prefab, position, Quaternion.identity, transform);
        fish.name = $"BackgroundFish_{fishIndex}_{source.prefab.name}_" +
                    Path.GetFileNameWithoutExtension(source.texturePath);
        ApplyDefaultTexture(fish, source);

        float depth01 = Mathf.InverseLerp(nearZ, farZ, position.z);
        float depthSize = Mathf.Lerp(depthScaleRange.x, depthScaleRange.y, depth01);
        bool isStarfish = source.fishId == "sao_bien";
        bool isJellyfish = source.fishId == "sua";
        // Ca_muc van boi binh thuong (dung DOTweenFishAnim), chi can khong
        // gian rong de khong dong ken khi so luong ca nhieu.
        bool isWideRoamer = source.fishId == "ca_muc";
        bool isCrab = source.fishId == "cua";
        bool isShrimp = source.fishId == "tom";
        bool isSeahorse = source.fishId == "ca_ngua";
        // Nhung loai khong co dang boi mo nuoc thong thuong: tu dieu khien
        // transform bang script rieng, DOTweenFishAnim chi con tac dung nap
        // thong so shader (mesh/material) trong Awake().
        bool usesBespokeMotion = isJellyfish || isCrab || isShrimp || isSeahorse;
        Vector2 widthRange = isStarfish ? starfishWidthRange : targetFishWidthRange;
        float targetWidth = Random.Range(
            Mathf.Min(widthRange.x, widthRange.y),
            Mathf.Max(widthRange.x, widthRange.y)) * depthSize * fishSizeMultiplier;
        NormalizeVisualWidth(fish, targetWidth);

        DOTweenFishAnim animation = fish.GetComponent<DOTweenFishAnim>();
        if (animation != null)
        {
            if (usesBespokeMotion)
            {
                animation.enabled = false;
            }
            else
            {
                ConfigureCalmAnimation(animation, laneIndex, position.z, nearZ, farZ, fishIndex);
            }
            if (isStarfish)
            {
                Bounds bounds = spawnBounds.bounds;
                animation.swimLaneCenter01 = Mathf.InverseLerp(
                    bounds.min.y, bounds.max.y, position.y);
                animation.swimLaneHeight01 = Mathf.Min(swimLaneHeight, 0.08f);
                animation.swimSpeed *= starfishSpeedMultiplier;
                animation.waveHeight = starfishWaveHeight;
                animation.maxTiltAngle = Mathf.Min(animation.maxTiltAngle, 6f);
            }
            if (isWideRoamer)
            {
                // Ca_muc boi lan rong khap chieu cao ho thay vi bi bo hep
                // trong 1 lan boi ngang nhu ca thuong, de khong dong ken.
                animation.useSwimLane = false;
                animation.waveHeight = fullRangeRoamWaveHeight;
            }
        }

        if (isJellyfish)
        {
            JellyfishDriftAnim driftAnimation = fish.GetComponent<JellyfishDriftAnim>();
            if (driftAnimation == null)
                driftAnimation = fish.AddComponent<JellyfishDriftAnim>();
            driftAnimation.Configure(spawnBounds);
        }
        else if (isCrab)
        {
            CrabScuttleAnim scuttleAnimation = fish.GetComponent<CrabScuttleAnim>();
            if (scuttleAnimation == null)
                scuttleAnimation = fish.AddComponent<CrabScuttleAnim>();
            scuttleAnimation.Configure(spawnBounds, crabBottomHeightRange, crabPatrolHalfWidth);
        }
        else if (isShrimp)
        {
            ShrimpFlickAnim flickAnimation = fish.GetComponent<ShrimpFlickAnim>();
            if (flickAnimation == null)
                flickAnimation = fish.AddComponent<ShrimpFlickAnim>();
            flickAnimation.Configure(spawnBounds);
        }
        else if (isSeahorse)
        {
            SeahorseHoverAnim hoverAnimation = fish.GetComponent<SeahorseHoverAnim>();
            if (hoverAnimation == null)
                hoverAnimation = fish.AddComponent<SeahorseHoverAnim>();
            hoverAnimation.Configure(spawnBounds, seahorsePatrolRadius);
        }
        else if (source.fishId == "rua")
        {
            // Mai rua cung, khong uon than: thay song lien tuc bang nhip
            // "vo manh roi luot" xen ke, lop len tren duong boi thong thuong.
            TwoBeatWaveEnvelope waveEnvelope = fish.GetComponent<TwoBeatWaveEnvelope>();
            if (waveEnvelope == null)
                fish.AddComponent<TwoBeatWaveEnvelope>();
        }
        else if (source.fishId == "ca_den_long")
        {
            // Ca gan nhu dung yen; "su song" den tu den nhap nhay tren dau.
            AnglerLurePulse lurePulse = fish.GetComponent<AnglerLurePulse>();
            if (lurePulse == null)
                fish.AddComponent<AnglerLurePulse>();
        }

        ConfigureClickInteraction(fish);

        if (!isStarfish && !usesBespokeMotion)
            sponsorFlagCandidates.Add(fish);
    }

    IEnumerator SponsorFlagRoutine()
    {
        if (sponsorFlagInitialDelay > 0f)
            yield return new WaitForSeconds(sponsorFlagInitialDelay);

        while (sponsorFlagEnabled)
        {
            sponsorFlagCandidates.RemoveAll(candidate => candidate == null);
            if (sponsorFlagSprite == null || sponsorFlagCandidates.Count == 0)
                yield break;

            GameObject fish = sponsorFlagCandidates[Random.Range(0, sponsorFlagCandidates.Count)];
            GameObject flag = CreateSponsorFlag(fish);
            float visibleDuration = Random.Range(
                Mathf.Min(sponsorFlagVisibleDurationRange.x, sponsorFlagVisibleDurationRange.y),
                Mathf.Max(sponsorFlagVisibleDurationRange.x, sponsorFlagVisibleDurationRange.y));

            yield return new WaitForSeconds(Mathf.Max(0.5f, visibleDuration));
            if (flag != null)
                Destroy(flag);

            float interval = Random.Range(
                Mathf.Min(sponsorFlagIntervalRange.x, sponsorFlagIntervalRange.y),
                Mathf.Max(sponsorFlagIntervalRange.x, sponsorFlagIntervalRange.y));
            yield return new WaitForSeconds(Mathf.Max(0.5f, interval));
        }
    }

    GameObject CreateSponsorFlag(GameObject fish)
    {
        if (fish == null || sponsorFlagSprite == null)
            return null;

        Renderer[] renderers = fish.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || sponsorFlagSprite.bounds.size.x <= 0.0001f)
            return null;

        Bounds fishBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            fishBounds.Encapsulate(renderers[i].bounds);

        GameObject flag = new GameObject("SponsorFlag");
        flag.layer = fish.layer;
        flag.transform.SetParent(fish.transform, true);

        SpriteRenderer flagRenderer = flag.AddComponent<SpriteRenderer>();
        flagRenderer.sprite = sponsorFlagSprite;
        flagRenderer.color = Color.white;
        flagRenderer.sortingOrder = -20;

        float targetWidth = Mathf.Max(0.1f, fishBounds.size.x * sponsorFlagWidthRatio);
        float worldScale = targetWidth / sponsorFlagSprite.bounds.size.x;
        Vector3 parentScale = fish.transform.lossyScale;
        Vector3 localScale = new Vector3(
            worldScale / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
            worldScale / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
            1f);
        flag.transform.localScale = localScale;

        // Sprite pivot is placed at the bottom of the flag pole, so this world
        // point is the exact contact point between pole and fish back.
        Vector3 anchor = new Vector3(
            fishBounds.center.x + fishBounds.size.x * sponsorFlagAnchorOffset.x,
            fishBounds.max.y + fishBounds.size.y * sponsorFlagAnchorOffset.y,
            fishBounds.center.z);
        if (Camera.main != null)
            anchor += Camera.main.transform.forward * 0.06f;
        flag.transform.position = anchor;
        flag.transform.rotation = Quaternion.identity;

        SponsorFlagDisplay display = flag.AddComponent<SponsorFlagDisplay>();
        display.Configure(
            localScale,
            sponsorFlagRaiseDuration,
            sponsorFlagAnchorOffset,
            sponsorFlagBackInset);
        return flag;
    }

    public void ConfigureClickInteraction(GameObject fish)
    {
        FishClickInteraction interaction = fish.GetComponent<FishClickInteraction>();
        if (interaction == null)
            interaction = fish.AddComponent<FishClickInteraction>();

        interaction.Configure(
            spawnBounds,
            clickBubbleMaterial,
            clickStartleSpeedMultiplier,
            clickBubbleTrailDuration,
            clickStartleAnimationSpeed,
            clickBubbleBurstCount,
            clickEscapeBubbleTrailEnabled,
            clickBubbleTrailRate,
            clickBubbleTrailSizeMultiplier,
            clickBubbleTrailSpread,
            clickBubbleSizeRange,
            clickBubbleColor);
    }

    void ConfigureCalmAnimation(
        DOTweenFishAnim animation,
        int laneIndex,
        float z,
        float nearZ,
        float farZ,
        int directionSeed)
    {
        float depth01 = Mathf.InverseLerp(nearZ, farZ, z);
        int laneTotal = Mathf.Max(3, swimLaneCount);

        // Nhan voi gia tri rieng cua prefab thay vi ghi de, de moi loai giu duoc
        // ca tinh bam sinh (toc do, do lac) da tinh chinh rieng thay vi bi keo
        // ve chung 1 dai gia tri nhu truoc day.
        animation.swimBounds = spawnBounds;
        animation.lifetime = 999999f;
        animation.useSwimLane = true;
        animation.swimLaneCenter01 = (laneIndex + 0.5f) / laneTotal;
        animation.swimLaneHeight01 = swimLaneHeight;
        animation.waveHeight *= Random.Range(calmWaveHeightRange.x, calmWaveHeightRange.y);
        animation.swimSpeed *= Random.Range(calmSpeedRange.x, calmSpeedRange.y) *
                               Mathf.Lerp(depthSpeedRange.x, depthSpeedRange.y, depth01);
        animation.initialDirectionOverride = directionSeed % 2 == 0 ? 1 : -1;
    }

    Vector3 FindComfortablePosition(Bounds bounds, float nearZ, float farZ, int laneIndex)
    {
        int laneTotal = Mathf.Max(3, swimLaneCount);
        float paddingX = Mathf.Min(1.25f, bounds.extents.x * 0.2f);
        float paddingY = Mathf.Min(0.75f, bounds.extents.y * 0.15f);
        float lane01 = (laneIndex + 0.5f) / laneTotal;
        float laneY = Mathf.Lerp(bounds.min.y + paddingY, bounds.max.y - paddingY, lane01);
        float laneStep = (bounds.size.y - paddingY * 2f) / laneTotal;

        Vector2 best = new Vector2(bounds.center.x, laneY);
        float bestDistance = -1f;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(bounds.min.x + paddingX, bounds.max.x - paddingX),
                laneY + Random.Range(-laneStep * 0.22f, laneStep * 0.22f));

            float nearest = DistanceToNearestOccupied(candidate);
            if (nearest > bestDistance)
            {
                best = candidate;
                bestDistance = nearest;
            }

            if (nearest >= minimumVisualSpacing)
                break;
        }

        occupiedScreenPositions.Add(best);
        return new Vector3(best.x, best.y, Random.Range(nearZ, farZ));
    }

    float DistanceToNearestOccupied(Vector2 candidate)
    {
        if (occupiedScreenPositions.Count == 0)
            return float.PositiveInfinity;

        float nearest = float.PositiveInfinity;
        for (int i = 0; i < occupiedScreenPositions.Count; i++)
            nearest = Mathf.Min(nearest, Vector2.Distance(candidate, occupiedScreenPositions[i]));

        return nearest;
    }

    void NormalizeVisualWidth(GameObject fish, float targetWidth)
    {
        Renderer[] renderers = fish.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visualBounds.Encapsulate(renderers[i].bounds);

        if (visualBounds.size.x <= 0.001f)
            return;

        float scaleFactor = Mathf.Clamp(targetWidth / visualBounds.size.x, 0.15f, 4f);
        fish.transform.localScale *= scaleFactor;
    }

    List<TexturedFishSource> BuildTexturedFishSources()
    {
        var sources = new List<TexturedFishSource>();
        string folder = ResolveFolderPath(defaultTextureFolderPath);
        if (!Directory.Exists(folder))
        {
            Debug.LogWarning("[BackgroundFishSpawner] Không tìm thấy thư mục texture: " + folder);
            return sources;
        }

        var prefabById = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fishPrefabs.Count; i++)
        {
            GameObject prefab = fishPrefabs[i];
            if (prefab == null)
                continue;

            string id = CanonicalFishId(prefab.name);
            if (!string.IsNullOrEmpty(id) && !prefabById.ContainsKey(id))
                prefabById.Add(id, prefab);
        }

        string[] files = Directory.GetFiles(folder);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            string extension = Path.GetExtension(files[i]);
            if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                continue;

            string fileId = CanonicalFishId(Path.GetFileNameWithoutExtension(files[i]));
            string bestId = null;
            foreach (string candidate in prefabById.Keys)
            {
                if (!IsFishIdPrefix(fileId, candidate) ||
                    (bestId != null && candidate.Length <= bestId.Length))
                    continue;

                bestId = candidate;
            }

            if (bestId == null)
            {
                Debug.LogWarning($"[BackgroundFishSpawner] Bỏ qua texture không khớp prefab: {Path.GetFileName(files[i])}");
                continue;
            }

            sources.Add(new TexturedFishSource
            {
                prefab = prefabById[bestId],
                texturePath = files[i],
                fishId = bestId
            });
        }

        var texturedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sources.Count; i++)
            texturedIds.Add(sources[i].fishId);

        foreach (KeyValuePair<string, GameObject> pair in prefabById)
        {
            if (!texturedIds.Contains(pair.Key))
                Debug.Log($"[BackgroundFishSpawner] Không spawn '{pair.Value.name}' vì chưa có texture màu tương ứng.");
        }

        Debug.Log($"[BackgroundFishSpawner] Đã nạp {sources.Count} texture màu cho {texturedIds.Count} loại cá.");
        return sources;
    }

    TexturedFishSource FindSmallFishSource(List<TexturedFishSource> sources)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i].fishId == "ca_con")
                return sources[i];
        }

        return null;
    }

    List<TexturedFishSource> GetIndividualSources(
        List<TexturedFishSource> sources,
        TexturedFishSource smallFishSource)
    {
        var result = new List<TexturedFishSource>();
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i].fishId != "ca_con")
                result.Add(sources[i]);
        }

        // Nếu hiện chỉ có texture ca_con, vẫn cho phép dùng nó để đạt đủ số lượng yêu cầu.
        if (result.Count == 0 && smallFishSource != null)
            result.Add(smallFishSource);

        return result;
    }

    // Dung lai dung thuat toan flood-fill silhouette cua luong QR
    // (QRFolderScanner.CalculateFilledSilhouetteAlpha) de lap day alpha ben
    // trong net ve, bien line-art thuan thanh mot mask dac hinh con ca.
    void FillLineArtAlpha(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        byte[] filledAlpha = QRFolderScanner.CalculateFilledSilhouetteAlpha(
            pixels, texture.width, texture.height);

        for (int i = 0; i < pixels.Length; i++)
            pixels[i].a = filledAlpha[i];

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    void ApplyDefaultTexture(GameObject fish, TexturedFishSource source)
    {
        string texturePath = source.texturePath;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(texturePath), false))
                throw new InvalidOperationException("Unity không đọc được ảnh");

            texture.name = "DefaultFish_" + Path.GetFileNameWithoutExtension(texturePath);
            // Các mesh rig hiện tại có UV vượt khỏi khoảng 0..1 và material gốc
            // cũng dùng Repeat. Clamp sẽ kéo pixel ở mép ảnh thành một dải dài:
            // Ca_heo chỉ còn một đường.
            bool useWhaleEdgeFix = source.fishId == "ca_voi";
            bool needsLineArtFill = source.fishId == "ca_con";

            // Mesh cá giờ chỉ là 1 quad chữ nhật (không còn cắt theo silhouette
            // như rig cũ), nên texture line-art thuần của ca_con (thân và nền
            // ngoài đều alpha ~0, chỉ nét viền có alpha) phải tự lấp đầy alpha
            // bên trong nét vẽ trước, nếu không cả hình chữ nhật sẽ hiện ra.
            if (needsLineArtFill)
                FillLineArtAlpha(texture);

            texture.wrapMode = useWhaleEdgeFix
                ? whaleTextureWrapMode
                : TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color baseTint = needsLineArtFill ? caConLineArtTint : Color.white;
            Color emissionTint = fishEmissionTint * baseTint;
            if (PlayerFishTextureApplicator.Apply(
                    fish,
                    texture,
                    fishEmissionIntensity,
                    emissionTint,
                    useWhaleEdgeFix ? whaleUvInset : 0f,
                    Vector2.one,
                    Vector2.zero,
                    0.1f,
                    baseTint) == null)
                throw new InvalidOperationException("material không nhận _BaseMap/_MainTex");

            texture = null; // Applicator sở hữu và giải phóng texture cùng cá.
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BackgroundFishSpawner] Không thể gán '{Path.GetFileName(texturePath)}' " +
                           $"cho '{fish.name}': {exception.Message}");
            // Không bao giờ để prefab nét viền chưa tô xuất hiện nếu texture lỗi.
            fish.SetActive(false);
            Destroy(fish);
        }
        finally
        {
            if (texture != null)
                Destroy(texture);
        }
    }

    static string CanonicalFishId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        while (normalized.Contains("__"))
            normalized = normalized.Replace("__", "_");

        // Một số prefab có hậu tố phiên bản như Ca_voi_1; texture vẫn mang ID ca_voi.
        int lastSeparator = normalized.LastIndexOf('_');
        if (lastSeparator > 0 && IsDigitsOnly(normalized, lastSeparator + 1))
            normalized = normalized.Substring(0, lastSeparator);

        return normalized.Trim('_');
    }

    static bool IsFishIdPrefix(string fileId, string fishId)
    {
        if (!fileId.StartsWith(fishId, StringComparison.OrdinalIgnoreCase))
            return false;

        return fileId.Length == fishId.Length || fileId[fishId.Length] == '_';
    }

    static bool IsDigitsOnly(string value, int startIndex)
    {
        if (startIndex >= value.Length)
            return false;

        for (int i = startIndex; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
                return false;
        }

        return true;
    }

    static string ResolveFolderPath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            T temporary = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = temporary;
        }
    }

    void ResolveDepthRange(out float nearZ, out float farZ)
    {
        nearZ = Mathf.Min(depthRange.x, depthRange.y);
        farZ = Mathf.Max(depthRange.x, depthRange.y);

        if (background != null)
            farZ = Mathf.Min(farZ, background.position.z - backgroundSafetyDistance);

        if (farZ <= nearZ)
        {
            Debug.LogWarning("[BackgroundFishSpawner] Depth Range không hợp lệ; đang tạo khoảng an toàn trước background.");
            nearZ = farZ - 0.1f;
        }
    }
}

/// <summary>
/// Marks a bespoke per-species motion script (in the spirit of the old
/// JellyfishRiseAnim) that fully replaces DOTweenFishAnim's transform control.
/// FishClickInteraction looks this up generically instead of hardcoding one
/// concrete type, so it can startle whichever bespoke script a species uses -
/// by reversing IN PLACE on its own patrol path, never detaching/disabling it
/// (that used to require re-syncing state on resume, which is what caused
/// fish to visibly snap once the flee ended).
/// </summary>
interface IStartleableFishMotion
{
    void Startle();
}


/// <summary>
/// Keeps sponsor artwork upright and readable while its fish turns or tilts.
/// </summary>
sealed class SponsorFlagDisplay : MonoBehaviour
{
    Vector3 baseLocalScale = Vector3.one;
    Vector2 anchorOffset = new Vector2(-0.08f, -0.03f);
    float backInset = 0.22f;
    float raiseDuration = 0.7f;
    float raiseTimer;
    float horizontalStartAngle = 90f;

    public void Configure(
        Vector3 localScale,
        float duration,
        Vector2 normalizedAnchorOffset,
        float normalizedBackInset)
    {
        baseLocalScale = new Vector3(
            Mathf.Abs(localScale.x),
            Mathf.Abs(localScale.y),
            localScale.z);
        raiseDuration = Mathf.Max(0.05f, duration);
        anchorOffset = normalizedAnchorOffset;
        backInset = Mathf.Clamp(normalizedBackInset, 0f, 0.5f);
        raiseTimer = 0f;

        float facingSign = transform.parent != null && transform.parent.lossyScale.x < 0f
            ? -1f
            : 1f;
        horizontalStartAngle = 90f * facingSign;
        transform.rotation = Quaternion.Euler(0f, 0f, horizontalStartAngle);
    }

    void LateUpdate()
    {
        if (transform.parent == null)
            return;

        Renderer[] renderers = transform.parent.GetComponentsInChildren<Renderer>(true);
        Bounds fishBounds = default;
        bool foundFishRenderer = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer visual = renderers[i];
            if (visual == null || visual is ParticleSystemRenderer ||
                visual.transform == transform || visual.transform.IsChildOf(transform))
                continue;

            if (!foundFishRenderer)
            {
                fishBounds = visual.bounds;
                foundFishRenderer = true;
            }
            else
            {
                fishBounds.Encapsulate(visual.bounds);
            }
        }

        if (foundFishRenderer)
        {
            Vector3 contactPoint = new Vector3(
                fishBounds.center.x + fishBounds.size.x * anchorOffset.x,
                fishBounds.max.y + fishBounds.size.y * (anchorOffset.y - backInset),
                fishBounds.center.z);
            if (Camera.main != null)
                contactPoint += Camera.main.transform.forward * 0.06f;
            transform.position = contactPoint;
        }

        Vector3 parentScale = transform.parent.lossyScale;
        transform.localScale = new Vector3(
            baseLocalScale.x * (parentScale.x < 0f ? -1f : 1f),
            baseLocalScale.y * (parentScale.y < 0f ? -1f : 1f),
            baseLocalScale.z);

        raiseTimer += Time.deltaTime;
        float raise01 = Mathf.Clamp01(raiseTimer / raiseDuration);
        float easedRaise = raise01 * raise01 * (3f - 2f * raise01);
        float angle = Mathf.Lerp(horizontalStartAngle, 0f, easedRaise);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}

/// <summary>
/// Gives every spawned fish a click target, an alarmed escape motion and two
/// lightweight particle effects: an initial bubble burst and a moving trail.
/// </summary>
sealed class FishClickInteraction : MonoBehaviour
{
    static readonly List<FishClickInteraction> activeInteractions =
        new List<FishClickInteraction>();

    BoxCollider2D swimBounds;
    Material bubbleMaterial;
    float startleSpeedMultiplier = 1.8f;
    float trailDuration = 1.2f;
    float startleAnimationSpeed = 1.35f;
    int burstCount = 16;
    bool bubbleTrailEnabled = true;
    float trailRate = 20f;
    float trailSizeMultiplier = 1.15f;
    float trailSpread = 0.1f;
    Vector2 bubbleSizeRange = new Vector2(0.1f, 0.22f);
    Color bubbleColor = new Color(0.72f, 0.94f, 1f, 0.78f);
    bool isEscaping;

    void OnEnable()
    {
        if (!activeInteractions.Contains(this))
            activeInteractions.Add(this);
    }

    void OnDestroy()
    {
        activeInteractions.Remove(this);
    }

    public static bool TryTriggerAtScreenPosition(Camera camera, Vector2 screenPosition)
    {
        if (camera == null)
            return false;

        FishClickInteraction bestCandidate = null;
        float bestDepth = float.PositiveInfinity;
        float bestCenterDistance = float.PositiveInfinity;

        for (int i = activeInteractions.Count - 1; i >= 0; i--)
        {
            FishClickInteraction interaction = activeInteractions[i];
            if (interaction == null)
            {
                activeInteractions.RemoveAt(i);
                continue;
            }

            if (!interaction.isActiveAndEnabled || interaction.isEscaping ||
                !interaction.TryGetScreenRect(camera, out Rect screenRect, out float depth))
                continue;

            if (!screenRect.Contains(screenPosition))
                continue;

            float centerDistance = ((Vector2)screenRect.center - screenPosition).sqrMagnitude;
            if (depth < bestDepth - 0.01f ||
                (Mathf.Abs(depth - bestDepth) <= 0.01f && centerDistance < bestCenterDistance))
            {
                bestCandidate = interaction;
                bestDepth = depth;
                bestCenterDistance = centerDistance;
            }
        }

        if (bestCandidate == null)
            return false;

        bestCandidate.TriggerFromScreenPosition(camera, screenPosition);
        return true;
    }

    bool TryGetScreenRect(Camera camera, out Rect screenRect, out float nearestDepth)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        screenRect = default;
        nearestDepth = float.PositiveInfinity;
        bool foundVisibleCorner = false;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer visual = renderers[rendererIndex];
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = visual.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 projected = camera.WorldToScreenPoint(worldCorner);
                if (projected.z <= 0f)
                    continue;

                foundVisibleCorner = true;
                nearestDepth = Mathf.Min(nearestDepth, projected.z);
                minimum = Vector2.Min(minimum, projected);
                maximum = Vector2.Max(maximum, projected);
            }
        }

        if (!foundVisibleCorner)
            return false;

        const float clickPaddingPixels = 10f;
        minimum -= Vector2.one * clickPaddingPixels;
        maximum += Vector2.one * clickPaddingPixels;
        screenRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        return screenRect.width > 0f && screenRect.height > 0f;
    }

    void TriggerFromScreenPosition(Camera camera, Vector2 screenPosition)
    {
        float depth = Vector3.Dot(
            transform.position - camera.transform.position,
            camera.transform.forward);
        Vector3 clickWorldPosition = camera.ScreenToWorldPoint(new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Max(0.01f, depth)));
        TriggerEscape(clickWorldPosition);
    }

    public void Configure(
        BoxCollider2D bounds,
        Material particleMaterial,
        float speedMultiplier,
        float duration,
        float animationSpeed,
        int bubblesPerBurst,
        bool enableBubbleTrail,
        float bubblesPerSecond,
        float bubbleTrailSizeMultiplier,
        float bubbleTrailSpread,
        Vector2 sizeRange,
        Color color)
    {
        swimBounds = bounds;
        bubbleMaterial = particleMaterial;
        startleSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        trailDuration = Mathf.Max(0.2f, duration);
        startleAnimationSpeed = Mathf.Max(1f, animationSpeed);
        burstCount = Mathf.Max(1, bubblesPerBurst);
        bubbleTrailEnabled = enableBubbleTrail;
        trailRate = Mathf.Max(0f, bubblesPerSecond);
        trailSizeMultiplier = Mathf.Max(0.1f, bubbleTrailSizeMultiplier);
        trailSpread = Mathf.Max(0.01f, bubbleTrailSpread);
        bubbleSizeRange = sizeRange;
        bubbleColor = color;
        CreateClickCollider();
    }

    void CreateClickCollider()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds visualBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visualBounds.Encapsulate(renderers[i].bounds);

        BoxCollider2D clickCollider = GetComponent<BoxCollider2D>();
        if (clickCollider == null)
            clickCollider = gameObject.AddComponent<BoxCollider2D>();

        Vector3 localCenter = transform.InverseTransformPoint(visualBounds.center);
        Vector3 scale = transform.lossyScale;
        clickCollider.offset = new Vector2(localCenter.x, localCenter.y);
        clickCollider.size = new Vector2(
            visualBounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            visualBounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
        clickCollider.isTrigger = true;
    }

    void OnMouseDown()
    {
        if (!isEscaping)
            TriggerEscape(GetPointerWorldPosition());
    }

    Vector3 GetPointerWorldPosition()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return transform.position;

        Vector2 pointerPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            pointerPosition = Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
        pointerPosition = Input.mousePosition;
#endif
        float depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
        return camera.ScreenToWorldPoint(new Vector3(
            pointerPosition.x, pointerPosition.y, Mathf.Max(0.01f, depth)));
    }

    // Khong con roi-quy-dao-roi-ghep-lai (tung gay giat vi trai khi resume).
    // Ca giu nguyen duong boi dang di, chi dao huong+tang toc ngay tren chinh
    // duong do (DOTweenFishAnim.StartleReverse / IStartleableFishMotion.Startle
    // deu tu dao huong noi bo, khong dung transform.SetParent/detach nua).
    void TriggerEscape(Vector3 clickWorldPosition)
    {
        if (isEscaping)
            return;

        isEscaping = true;
        Debug.Log($"[FishClick] '{name}' hoang so va quay dau boi nguoc lai.", this);

        DOTweenFishAnim swimAnimation = GetComponent<DOTweenFishAnim>();
        IStartleableFishMotion bespokeMotion = GetComponent<IStartleableFishMotion>();
        if (bespokeMotion != null)
        {
            // Motion rieng da tu dieu khien transform; khong tao them DOPath
            // cua DOTweenFishAnim tren cung GameObject.
            bespokeMotion.Startle();
        }
        else if (swimAnimation != null)
        {
            if (!swimAnimation.enabled && transform.parent != null)
            {
                // Truong hop duy nhat con can tach: 1 ca_con thanh vien dan,
                // component nay von bi disable tu luc spawn (chi "an theo"
                // goc dan bang parenting) nen chua co quy dao rieng nao de
                // dao nguoc ca. Sao chep cau hinh boi tu goc dan, tach ra va
                // khoi dong nhu 1 con ca doc lap moi (khong co "diem xuat
                // phat" cu de quay ve).
                DOTweenFishAnim schoolAnimation = transform.parent.GetComponent<DOTweenFishAnim>();
                if (schoolAnimation != null)
                {
                    swimAnimation.swimBounds = schoolAnimation.swimBounds;
                    swimAnimation.swimSpeed = schoolAnimation.swimSpeed;
                    swimAnimation.waypointsPerSegment = schoolAnimation.waypointsPerSegment;
                    swimAnimation.waveHeight = schoolAnimation.waveHeight;
                    swimAnimation.useSwimLane = schoolAnimation.useSwimLane;
                    swimAnimation.swimLaneCenter01 = schoolAnimation.swimLaneCenter01;
                    swimAnimation.swimLaneHeight01 = schoolAnimation.swimLaneHeight01;
                    swimAnimation.maxTiltAngle = schoolAnimation.maxTiltAngle;
                    swimAnimation.lifetime = schoolAnimation.lifetime;
                }

                transform.SetParent(null, true);
                float awayX = transform.position.x - clickWorldPosition.x;
                int awayDirection = Mathf.Abs(awayX) > 0.01f
                    ? (int)Mathf.Sign(awayX)
                    : (Random.value < 0.5f ? -1 : 1);
                swimAnimation.ResumeSwimmingFromCurrentPosition(awayDirection);
            }
            else
            {
                swimAnimation.StartleReverse(startleSpeedMultiplier, startleAnimationSpeed);
            }
        }

        // Cac loai co script chuyen dong rieng (sua, ca_muc, cua, tom, ca_ngua...)
        // deu duoc tra ve qua cung 1 interface thay vi liet ke tung class cu the,
        // de moi loai moi them sau nay tu dong duoc "giat minh" dung cach.
        CreateBubbleBurst();
        if (bubbleTrailEnabled)
            CreateBubbleTrail();

        CancelInvoke(nameof(ClearEscaping));
        Invoke(nameof(ClearEscaping), trailDuration);
    }

    void ClearEscaping() => isEscaping = false;

    void CreateBubbleBurst()
    {
        ParticleSystem particles = CreateParticleSystem("FishClickBubbleBurst", transform.position);
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 0.45f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
        main.startSize = BubbleSizeCurve(1f);
        main.maxParticles = Mathf.Max(24, burstCount * 2);
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.burstCount = 1;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)burstCount));

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.13f;

        ConfigureBubbleMotion(particles, 0.28f, 0.16f);
        particles.Play();
    }

    void CreateBubbleTrail()
    {
        ParticleSystem particles = CreateParticleSystem("FishEscapeBubbleTrail", transform.position);
        particles.transform.SetParent(transform, true);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.duration = Mathf.Max(0.5f, trailDuration);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.2f);
        main.startSize = BubbleSizeCurve(trailSizeMultiplier);
        main.maxParticles = Mathf.CeilToInt(trailRate * 2f) + 12;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = trailRate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = trailSpread;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.28f, 1f),
            new Keyframe(0.78f, 0.82f),
            new Keyframe(1f, 0.35f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.68f, 0.92f, 1f), 0f),
                new GradientColorKey(new Color(0.9f, 0.98f, 1f), 0.55f),
                new GradientColorKey(new Color(0.72f, 0.9f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.82f, 0.12f),
                new GradientAlphaKey(0.62f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        ConfigureBubbleMotion(particles, 0.32f, 0.14f);
        particles.Play();

        // Khong con coroutine "cuoi qua trinh chay" de bao khi nao dung - tu
        // huy sau dung trailDuration, khong phu thuoc ca con song hay khong
        // (Destroy(gameObject) an toan ke ca khi ca bi Destroy truoc do vi
        // trail da la GameObject rieng, khong con la con cua ca do nua).
        Transform trailTransform = particles.transform;
        DOVirtual.DelayedCall(trailDuration, () =>
        {
            if (trailTransform == null)
                return;
            trailTransform.SetParent(null, true);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(particles.gameObject, 1.5f);
        });
    }

    ParticleSystem CreateParticleSystem(string objectName, Vector3 position)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.position = position;
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

        // AddComponent starts a ParticleSystem immediately in some Unity versions.
        // Stop it before changing duration/bursts so every module can be configured safely.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            bubbleColor,
            new Color(0.88f, 0.98f, 1f, Mathf.Clamp01(bubbleColor.a * 0.72f)));

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortingOrder = 30;
        if (bubbleMaterial != null)
            particleRenderer.sharedMaterial = bubbleMaterial;

        return particles;
    }

    void ConfigureBubbleMotion(ParticleSystem particles, float upwardSpeed, float noiseStrength)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // All axes intentionally use Constant mode to avoid Unity's
        // "Particle Velocity curves must all be in the same mode" warning.
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(upwardSpeed);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.25f;
        noise.damping = true;
    }

    ParticleSystem.MinMaxCurve BubbleSizeCurve(float multiplier)
    {
        float minimum = Mathf.Min(bubbleSizeRange.x, bubbleSizeRange.y) * multiplier;
        float maximum = Mathf.Max(bubbleSizeRange.x, bubbleSizeRange.y) * multiplier;
        return new ParticleSystem.MinMaxCurve(minimum, maximum);
    }
}


/// <summary>
/// Sideways scuttle-walk for cua (crab): short hops with pauses, no
/// direction-facing flip (a crab reads the same broadside-on regardless of
/// travel direction), confined to a small patrol patch near the tank floor.
/// </summary>
sealed class CrabScuttleAnim : MonoBehaviour, IStartleableFishMotion
{
    public BoxCollider2D swimBounds;
    public Vector2 hopDistanceRange = new Vector2(0.55f, 0.9f);
    public float hopDuration = 0.25f;
    public Vector2 pauseDurationRange = new Vector2(0.25f, 0.9f);

    Vector3 baseScale;
    float anchorX;
    float patrolHalfWidth = 1.8f;
    float direction = 1f;
    int hopsSinceSpin;
    int nextSpinThreshold;
    Sequence cycleSequence;

    public void Configure(BoxCollider2D bounds, Vector2 bottomHeightRange01, float halfWidth)
    {
        swimBounds = bounds;
        patrolHalfWidth = Mathf.Max(0.5f, halfWidth);
        baseScale = transform.localScale;
        anchorX = transform.position.x;
        direction = Random.value < 0.5f ? -1f : 1f;
        nextSpinThreshold = Random.Range(10, 20);
        // Khong tu dat lai Y o day nua: swimBounds la collider "vung boi" rong
        // hon vung camera thuc su nhin thay (o do sau spawn cua ca nguoi choi),
        // Lerp truc tiep tren no co the day cua ra ngoai man hinh. Do cao thap
        // hon duoc quyet dinh tu luc tha ca (xem QRFolderScanner.CreateFish),
        // dua tren viewport camera - luon nam trong khung hinh.
        GetComponent<DOTweenFishAnim>()?.EnsureAmbientTwistScheduled();
        PlayNextHop();
    }

    void OnDisable() => cycleSequence?.Kill();

    void PlayNextHop()
    {
        cycleSequence?.Kill();
        if (swimBounds == null)
            return;

        Bounds bounds = swimBounds.bounds;
        float minX = Mathf.Max(bounds.min.x, anchorX - patrolHalfWidth);
        float maxX = Mathf.Min(bounds.max.x, anchorX + patrolHalfWidth);
        if (transform.position.x <= minX + 0.05f)
            direction = 1f;
        else if (transform.position.x >= maxX - 0.05f)
            direction = -1f;

        bool doubleStep = Random.value < 0.25f;
        int hopCount = doubleStep ? 2 : 1;

        cycleSequence = DOTween.Sequence();
        for (int i = 0; i < hopCount; i++)
        {
            float hopDistance = Random.Range(hopDistanceRange.x, hopDistanceRange.y) * (doubleStep ? 0.55f : 1f);
            Vector3 target = transform.position + new Vector3(direction * hopDistance, 0f, 0f);
            target.x = Mathf.Clamp(target.x, minX, maxX);
            float rock = direction * Random.Range(3f, 6f);

            cycleSequence.Append(
                transform.DOScale(new Vector3(baseScale.x, baseScale.y * 0.9f, baseScale.z), hopDuration * 0.3f)
                    .SetEase(Ease.OutQuad));
            cycleSequence.Join(transform.DOMove(target, hopDuration).SetEase(Ease.OutQuad));
            cycleSequence.Join(
                transform.DORotate(new Vector3(0f, 0f, rock), hopDuration * 0.5f).SetEase(Ease.OutSine));
            cycleSequence.Append(transform.DOScale(baseScale, hopDuration * 0.25f).SetEase(Ease.OutBack));
            cycleSequence.Join(transform.DORotate(Vector3.zero, hopDuration * 0.5f).SetEase(Ease.InSine));
            if (i < hopCount - 1)
                cycleSequence.AppendInterval(0.08f);
        }

        hopsSinceSpin++;
        if (hopsSinceSpin >= nextSpinThreshold)
        {
            hopsSinceSpin = 0;
            nextSpinThreshold = Random.Range(10, 20);
            cycleSequence.Append(
                transform.DORotate(new Vector3(0f, 0f, 360f), 0.45f, RotateMode.FastBeyond360).SetEase(Ease.OutBack));
            cycleSequence.AppendCallback(() => transform.rotation = Quaternion.identity);
        }

        float pause = Random.Range(pauseDurationRange.x, pauseDurationRange.y);
        cycleSequence.AppendInterval(pause);
        cycleSequence.OnComplete(PlayNextHop);
    }

    public void Startle()
    {
        direction = -direction;
        PlayNextHop();
    }
}

/// <summary>
/// Idle-hover-then-escape-flick movement for tom (shrimp): the real caridoid
/// escape reaction - mostly still, then a single sharp backward tail-flick
/// dash with a tumble, before settling back into idle.
/// </summary>
sealed class ShrimpFlickAnim : MonoBehaviour, IStartleableFishMotion
{
    public BoxCollider2D swimBounds;
    public Vector2 idleIntervalRange = new Vector2(4f, 9f);
    public Vector2 flickDistanceRange = new Vector2(1.5f, 2.6f);
    public float flickDuration = 0.2f;
    public float settleDuration = 0.5f;
    [Tooltip("Toc do boi lang thang ve 2 dau man hinh - can du ro de thay dang di chuyen.")]
    public float wanderSpeed = 0.5f;

    float direction = 1f;
    float flickTimer;
    float nextFlickDelay;
    bool isFlicking;
    bool spriteFacesRight = true;
    DOTweenFishAnim swimAnimation;
    Sequence flickSequence;

    public void Configure(BoxCollider2D bounds)
    {
        swimBounds = bounds;
        // Ban than script nay khong tu suy ra huong quay mat dung - phai doc tu
        // DOTweenFishAnim (van con tren GameObject, chi bi disable) de tuan thu
        // dung calibration rieng cua tung loai, giong moi noi khac trong file nay.
        swimAnimation = GetComponent<DOTweenFishAnim>();
        spriteFacesRight = swimAnimation == null || swimAnimation.spriteFacesRight;
        Vector3 scale = transform.localScale;
        float currentFacing = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Sign(scale.x);
        direction = currentFacing;
        ApplyFacingFromDirection();
        swimAnimation?.EnsureAmbientTwistScheduled();
        ScheduleNextFlick();
    }

    void OnDisable() => flickSequence?.Kill();

    void ApplyFacingFromDirection()
    {
        Vector3 scale = transform.localScale;
        float visualFacing = direction * (spriteFacesRight ? 1f : -1f);
        transform.localScale = new Vector3(
            Mathf.Abs(scale.x) * Mathf.Sign(visualFacing == 0f ? 1f : visualFacing),
            scale.y,
            scale.z);
    }

    void ScheduleNextFlick()
    {
        flickTimer = 0f;
        nextFlickDelay = Random.Range(idleIntervalRange.x, idleIntervalRange.y);
    }

    // Boi lang thang ro rang ve phia 2 dau man hinh (doi chieu khi cham bien),
    // khong dung im giua 2 lan bung nhu truoc.
    void LateUpdate()
    {
        if (swimBounds == null || isFlicking)
            return;

        Bounds bounds = swimBounds.bounds;
        Vector3 position = transform.position;
        position.x += direction * wanderSpeed * Time.deltaTime;
        if (position.x >= bounds.max.x) { position.x = bounds.max.x; direction = -1f; }
        else if (position.x <= bounds.min.x) { position.x = bounds.min.x; direction = 1f; }
        transform.position = position;

        ApplyFacingFromDirection();

        flickTimer += Time.deltaTime;
        if (flickTimer >= nextFlickDelay)
            PlayFlick();
    }

    void PlayFlick()
    {
        if (swimBounds == null)
        {
            ScheduleNextFlick();
            return;
        }

        isFlicking = true;
        Bounds bounds = swimBounds.bounds;
        float flickDistance = Random.Range(flickDistanceRange.x, flickDistanceRange.y);
        // Bung MANH VE PHIA DANG DI TOI (khong phai lui nhu phan xa thoat
        // hiem sinh hoc that) - theo yeu cau rieng cho game nay.
        Vector3 target = transform.position +
                          new Vector3(direction * flickDistance, Random.Range(-0.2f, 0.3f), 0f);
        target.x = Mathf.Clamp(target.x, bounds.min.x, bounds.max.x);
        target.y = Mathf.Clamp(target.y, bounds.min.y, bounds.max.y);

        flickSequence = DOTween.Sequence();
        flickSequence.Append(transform.DOMove(target, flickDuration).SetEase(Ease.OutExpo));
        flickSequence.Join(
            transform.DORotate(new Vector3(0f, 0f, -direction * 20f), flickDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad));
        flickSequence.Append(transform.DORotate(Vector3.zero, settleDuration).SetEase(Ease.OutElastic));
        flickSequence.OnComplete(() =>
        {
            isFlicking = false;
            ScheduleNextFlick();
        });
    }

    // Dao huong roi bung ngay - dung lai chinh phan xa "flick" co san (von la
    // 1 cu bung manh VE PHIA DANG DI TOI), nen dao huong xong la tu nhien lao
    // nguoc ve dung phia vua den.
    public void Startle()
    {
        direction = -direction;
        ApplyFacingFromDirection();
        if (!isFlicking)
            PlayFlick();
    }
}

/// <summary>
/// Ca_ngua (seahorse): swims across the tank like a regular fish (bounce at
/// swimBounds edges, flip localScale.x to face travel direction, honoring the
/// per-prefab spriteFacesRight calibration) plus a gentle vertical bob and an
/// occasional curious head-tilt for character. Kept as a bespoke script
/// instead of re-enabling DOTweenFishAnim because the seahorse should stay
/// upright with no body-wave/tilt-on-turn - just straightforward side-to-side
/// travel.
/// </summary>
sealed class SeahorseHoverAnim : MonoBehaviour, IStartleableFishMotion
{
    public BoxCollider2D swimBounds;
    [Tooltip("Bien do troi len xuong quanh do cao hien tai.")]
    public float patrolRadius = 1f;
    [Tooltip("Toc do boi ngang qua be.")]
    public float wanderSpeed = 0.45f;
    [Tooltip("Toc do lay mau Perlin noise cho troi doc; cang nho troi cang cham/muot.")]
    public float driftNoiseSpeed = 0.12f;
    [Tooltip("Toc do bam theo diem troi doc muc tieu (khong bao gio dung han giua chung).")]
    public float followSharpness = 1.2f;
    [Tooltip("Goc nghieng toi da theo huong di chuyen thuc te (boi cheo len/xuong).")]
    [Range(0f, 45f)] public float maxTiltAngle = 18f;
    [Min(0.1f)] public float tiltSmoothSpeed = 5f;

    const float startleSpeedMultiplier = 3f;
    const float startleBoostDuration = 0.8f;

    float anchorY;
    float noiseSeedY;
    float direction = 1f;
    bool spriteFacesRight = true;
    Vector3 lastPosition;
    float headTiltOffset;
    float startleBoostTimer;
    float currentTiltAngle;
    DOTweenFishAnim swimAnimation;
    Sequence headTiltSequence;

    public void Configure(BoxCollider2D bounds, float radius)
    {
        swimBounds = bounds;
        patrolRadius = Mathf.Max(0.3f, radius);
        anchorY = transform.position.y;
        noiseSeedY = Random.Range(0f, 1000f);
        transform.rotation = Quaternion.identity;
        lastPosition = transform.position;
        headTiltOffset = 0f;

        swimAnimation = GetComponent<DOTweenFishAnim>();
        spriteFacesRight = swimAnimation == null || swimAnimation.spriteFacesRight;
        Vector3 scale = transform.localScale;
        float currentFacing = Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Sign(scale.x);
        direction = currentFacing;
        ApplyFacingFromDirection();
        swimAnimation?.EnsureAmbientTwistScheduled();

        ScheduleHeadTilt();
    }

    void ApplyFacingFromDirection()
    {
        Vector3 scale = transform.localScale;
        float visualFacing = direction * (spriteFacesRight ? 1f : -1f);
        transform.localScale = new Vector3(
            Mathf.Abs(scale.x) * Mathf.Sign(visualFacing == 0f ? 1f : visualFacing),
            scale.y,
            scale.z);
    }

    void OnDisable()
    {
        headTiltSequence?.Kill();
    }

    void LateUpdate()
    {
        if (swimBounds == null)
            return;

        float speedMultiplier = 1f;
        if (startleBoostTimer > 0f)
        {
            speedMultiplier = startleSpeedMultiplier;
            startleBoostTimer -= Time.deltaTime;
        }

        Bounds bounds = swimBounds.bounds;
        Vector3 position = transform.position;
        position.x += direction * wanderSpeed * speedMultiplier * Time.deltaTime;
        if (position.x >= bounds.max.x) { position.x = bounds.max.x; direction = -1f; }
        else if (position.x <= bounds.min.x) { position.x = bounds.min.x; direction = 1f; }

        // Troi len xuong nhe theo Perlin noise quanh do cao hien tai thay vi
        // dung im tren truc Y trong luc boi ngang.
        float driftY = (Mathf.PerlinNoise(noiseSeedY, Time.time * driftNoiseSpeed) * 2f - 1f) * patrolRadius * 0.6f;
        float targetY = Mathf.Clamp(anchorY + driftY, bounds.min.y, bounds.max.y);
        float smoothing = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        position.y = Mathf.Lerp(position.y, targetY, smoothing);
        transform.position = position;

        ApplyFacingFromDirection();

        // Khi troi cheo len/xuong trong luc boi ngang, mat phai nghieng theo
        // huong di chuyen thuc te thay vi giu z=0 - cung cong thuc voi
        // DOTweenFishAnim.UpdateFishTilt de dong nhat giua cac loai.
        Vector3 movement = position - lastPosition;
        if (movement.sqrMagnitude > 0.0000001f)
        {
            float pathAngle = Mathf.Atan2(movement.y, Mathf.Abs(movement.x)) * Mathf.Rad2Deg;
            float visualFacing = direction * (spriteFacesRight ? 1f : -1f);
            float angle = Mathf.Clamp(pathAngle * Mathf.Sign(visualFacing == 0f ? 1f : visualFacing), -maxTiltAngle, maxTiltAngle);
            float rotSmoothing = 1f - Mathf.Exp(-tiltSmoothSpeed * Time.deltaTime);
            // currentTiltAngle lam muot rieng, roi moi cong spin (khong lam
            // muot) vao luc dung Euler - cung ly do nhu DOTweenFishAnim.
            // UpdateFishTilt: Slerp thang tu transform.rotation cu se lam
            // currentTiltAngle "hoc nham" ca phan PlaySpin() da xoay o frame truoc.
            currentTiltAngle = Mathf.LerpAngle(currentTiltAngle, angle, rotSmoothing);
            float spinOffset = swimAnimation != null ? swimAnimation.SpinOffsetAngle : 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, currentTiltAngle + headTiltOffset + spinOffset);
        }
        lastPosition = position;
    }

    // Lac dau nhu mot do lech (headTiltOffset) cong them vao goc nghieng theo
    // huong di chuyen o LateUpdate, thay vi DORotate truc tiep transform - vi
    // rotation cua transform gio da bi LateUpdate ghi de moi frame theo huong
    // boi, 2 nguon cung xoay 1 truc se danh nhau/giat.
    void ScheduleHeadTilt()
    {
        headTiltSequence?.Kill();
        float delay = Random.Range(7f, 12f);
        headTiltSequence = DOTween.Sequence();
        headTiltSequence.AppendInterval(delay);
        headTiltSequence.Append(
            DOTween.To(() => headTiltOffset, value => headTiltOffset = value, -14f, 0.3f).SetEase(Ease.OutQuad));
        headTiltSequence.AppendInterval(0.4f);
        headTiltSequence.Append(
            DOTween.To(() => headTiltOffset, value => headTiltOffset = value, 0f, 0.3f).SetEase(Ease.InQuad));
        headTiltSequence.OnComplete(ScheduleHeadTilt);
    }

    public void Startle()
    {
        direction = -direction;
        ApplyFacingFromDirection();
        startleBoostTimer = startleBoostDuration;
        swimAnimation?.PlaySpin();
    }
}

/// <summary>
/// Diagonal drifting wave for sua (jellyfish): unlike a regular fish (long
/// horizontal segment, near-flat Y) or ca_muc (purposeful directional swim),
/// a jellyfish's X and Y both keep changing together continuously - drifting
/// sideways across the whole map while bobbing up-down and breathing via a
/// squash-stretch pulse. Fully replaces DOTweenFishAnim (bespoke motion),
/// same pattern as CrabScuttleAnim/ShrimpFlickAnim/SeahorseHoverAnim.
/// </summary>
sealed class JellyfishDriftAnim : MonoBehaviour, IStartleableFishMotion
{
    public BoxCollider2D swimBounds;
    public float driftSpeed = 0.42f; // giam 30% so voi 0.6 truoc do
    public float bobAmplitude = 2f;
    public float bobFrequencyHz = 0.12f;
    public float pulseScaleAmount = 0.12f;
    public float pulseFrequencyHz = 0.45f;
    [Tooltip("Goc nghieng toi da theo huong troi thuc te (troi cheo len/xuong).")]
    [Range(0f, 45f)] public float maxTiltAngle = 22f;
    [Min(0.1f)] public float tiltSmoothSpeed = 4f;

    const float startleSpeedMultiplier = 3f;
    const float startleBoostDuration = 0.8f;

    float phaseOffset;
    float lastBobOffset;
    Vector3 baseScale;
    float direction = 1f;
    bool spriteFacesRight = true;
    Vector3 lastPosition;
    float startleBoostTimer;
    float currentTiltAngle;
    DOTweenFishAnim swimAnimation;

    public void Configure(BoxCollider2D bounds)
    {
        swimBounds = bounds;
        baseScale = transform.localScale;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        direction = Random.value < 0.5f ? -1f : 1f;
        lastBobOffset = 0f;
        lastPosition = transform.position;

        swimAnimation = GetComponent<DOTweenFishAnim>();
        spriteFacesRight = swimAnimation == null || swimAnimation.spriteFacesRight;
        swimAnimation?.EnsureAmbientTwistScheduled();
    }

    void OnDisable() { }

    void LateUpdate()
    {
        if (swimBounds == null)
            return;

        float speedMultiplier = 1f;
        if (startleBoostTimer > 0f)
        {
            speedMultiplier = startleSpeedMultiplier;
            startleBoostTimer -= Time.deltaTime;
        }

        Bounds bounds = swimBounds.bounds;
        Vector3 position = transform.position;

        // Troi ngang lien tuc khap chieu rong ho (dan trai cac con ra, tranh
        // dong ken khi so luong ca nhieu), doi chieu khi cham bien.
        position.x += direction * driftSpeed * speedMultiplier * Time.deltaTime;
        if (position.x >= bounds.max.x) { position.x = bounds.max.x; direction = -1f; }
        else if (position.x <= bounds.min.x) { position.x = bounds.min.x; direction = 1f; }

        // Bap benh len-xuong CUNG LUC voi troi ngang (khong phai troi xong roi
        // moi bap benh) - ca 2 truc thay doi dong thoi tao thanh 1 duong song
        // cheo, thay vi mot truc thang + mot truc lac doc lap.
        float bobOffset = Mathf.Sin(Time.time * bobFrequencyHz * Mathf.PI * 2f + phaseOffset) * bobAmplitude;
        position.y += bobOffset - lastBobOffset;
        position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
        lastBobOffset = bobOffset;

        transform.position = position;

        float pulse01 = (Mathf.Sin(Time.time * pulseFrequencyHz * Mathf.PI * 2f + phaseOffset) + 1f) * 0.5f;
        float scaleFactor = 1f + (pulse01 - 0.5f) * 2f * pulseScaleAmount;
        float absX = Mathf.Abs(baseScale.x);
        float facingSign = spriteFacesRight ? direction : -direction;
        transform.localScale = new Vector3(
            absX * facingSign * scaleFactor,
            baseScale.y * (2f - scaleFactor),
            baseScale.z);

        // Troi cheo len/xuong thi mat phai nghieng theo huong troi thuc te
        // thay vi giu z=0, cung cong thuc voi DOTweenFishAnim.UpdateFishTilt.
        Vector3 movement = position - lastPosition;
        if (movement.sqrMagnitude > 0.0000001f)
        {
            float pathAngle = Mathf.Atan2(movement.y, Mathf.Abs(movement.x)) * Mathf.Rad2Deg;
            float angle = Mathf.Clamp(pathAngle * facingSign, -maxTiltAngle, maxTiltAngle);
            float smoothing = 1f - Mathf.Exp(-tiltSmoothSpeed * Time.deltaTime);
            currentTiltAngle = Mathf.LerpAngle(currentTiltAngle, angle, smoothing);
            float spinOffset = swimAnimation != null ? swimAnimation.SpinOffsetAngle : 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, currentTiltAngle + spinOffset);
        }
        lastPosition = position;
    }

    // Component nay khong bao gio bi tat khi bi click nua (xem FishClickInteraction),
    // nen LateUpdate chay lien tuc va lastBobOffset luon khop voi Time.time -
    // chi can dao huong troi va tang toc tam thoi, khong can dong bo lai gi ca.
    public void Startle()
    {
        direction = -direction;
        startleBoostTimer = startleBoostDuration;
        swimAnimation?.PlaySpin();
    }
}

/// <summary>
/// Two-beat "stroke then glide" envelope for rua (turtle): a turtle's shell
/// is rigid (no body-wave), so its motion has to come from the flipper band
/// instead. This spikes the shared FishBody fin-wave amplitude (the same
/// "vay vay" band other fish use for a subtle pectoral ripple) briefly on
/// each stroke and lets it decay during the long glide, layered on top of
/// the regular swim path.
/// </summary>
sealed class TwoBeatWaveEnvelope : MonoBehaviour
{
    public DOTweenFishAnim swimAnimation;
    public float cycleDuration = 2.1f;
    public float strokeSharpness = 6f;
    public float glideAmplitudeScale = 0.15f;
    public float strokeAmplitudeScale = 1.6f;

    Material bodyMaterial;
    float baseAmplitude = -1f;

    void Awake()
    {
        if (swimAnimation == null)
            swimAnimation = GetComponent<DOTweenFishAnim>();
        // Lay thang tu gia tri cau hinh tren component, khong doc nguoc lai tu
        // material (co the da bi UpdateFishTilt boost tam thoi luc doc).
        if (swimAnimation != null)
            baseAmplitude = swimAnimation.finWaveAmplitude;
        Renderer rendererComponent = GetComponent<Renderer>();
        bodyMaterial = rendererComponent != null ? rendererComponent.sharedMaterial : null;
    }

    // LateUpdate() vi DOTweenFishAnim.UpdateFishTilt() (chay trong Update(),
    // qua callback OnUpdate cua DOTween) cung ghi _WaveAmplitude moi khi re
    // (turnAmplitudeBoost, mac dinh 0 cho rua nen khong dung cham _FinWaveAmplitude).
    void LateUpdate()
    {
        if (bodyMaterial == null || baseAmplitude < 0f || !bodyMaterial.HasProperty("_FinWaveAmplitude"))
            return;

        float phase01 = Mathf.Repeat(Time.time, cycleDuration) / cycleDuration;
        float pulse01 = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase01 * Mathf.PI * 2f)), strokeSharpness);
        float scale = Mathf.Lerp(glideAmplitudeScale, strokeAmplitudeScale, pulse01);
        bodyMaterial.SetFloat("_FinWaveAmplitude", baseAmplitude * scale);
    }
}

/// <summary>
/// Slow breathing emission pulse for ca_den_long (anglerfish): the fish itself
/// barely moves, so its "life" comes from the glowing lure brightening in a
/// slow rhythm with an occasional brighter "notice" flash.
/// </summary>
sealed class AnglerLurePulse : MonoBehaviour
{
    public float breathCycleDuration = 5f;
    public Vector2 flashIntervalRange = new Vector2(8f, 15f);
    public float flashPeakMultiplier = 1.6f;

    Material bodyMaterial;
    Color baseEmission = Color.black;
    bool hasBaseEmission;
    Tween flashTween;

    void OnEnable()
    {
        Renderer rendererComponent = GetComponent<Renderer>();
        bodyMaterial = rendererComponent != null ? rendererComponent.sharedMaterial : null;
        if (bodyMaterial != null && bodyMaterial.HasProperty("_EmissionColor"))
        {
            baseEmission = bodyMaterial.GetColor("_EmissionColor");
            hasBaseEmission = true;
        }

        ScheduleNextFlash();
    }

    void OnDisable() => flashTween?.Kill();

    void Update()
    {
        if (!hasBaseEmission)
            return;

        float breathPhase01 = Mathf.Repeat(Time.time, breathCycleDuration) / breathCycleDuration;
        float breath = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(breathPhase01 * Mathf.PI * 2f) + 1f) * 0.5f);
        bodyMaterial.SetColor("_EmissionColor", baseEmission * breath);
    }

    void ScheduleNextFlash()
    {
        flashTween?.Kill();
        if (!hasBaseEmission)
            return;

        float delay = Random.Range(flashIntervalRange.x, flashIntervalRange.y);
        flashTween = DOVirtual.DelayedCall(delay, () =>
        {
            bodyMaterial.SetColor("_EmissionColor", baseEmission * flashPeakMultiplier);
            ScheduleNextFlash();
        });
    }
}
