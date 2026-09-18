using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    [Range(0.5f, 2.5f)] public float fishSizeMultiplier = 1.3f;

    [Header("Texture cá mặc định")]
    [Tooltip("Chỉ prefab có ít nhất một ảnh khớp tên trong thư mục này mới được spawn.")]
    public string defaultTextureFolderPath = "Assets/default_fish";
    public bool requireDefaultTexture = true;

    [Header("Do sang cua ca")]
    [Tooltip("0 = tat emission. Nen bat dau trong khoang 0.2 - 0.5.")]
    [Range(0f, 2f)] public float fishEmissionIntensity = 0.3f;
    [Tooltip("Mau nhan emission. Mau trang giu nguyen mau texture goc.")]
    [ColorUsage(false, true)] public Color fishEmissionTint = Color.white;

    [Header("Ca he mac dinh")]
    [Tooltip("Texture ca_he hien tai la line-art trang/den. Mau nay phu len phan than trang.")]
    public Color clownfishDefaultTint = new Color(1f, 0.42f, 0.08f, 1f);

    [Header("Sua duong noi UV ca voi")]
    [Tooltip("Mirror ngan texture lay mau tu mep doi dien khi UV ca voi vuot bien.")]
    public TextureWrapMode whaleTextureWrapMode = TextureWrapMode.Mirror;
    [Tooltip("Thu nhe vung UV vao trong de tranh lay mau ngay sat bien texture.")]
    [Range(0f, 0.03f)] public float whaleUvInset = 0.004f;

    [Header("Can chinh UV ca con")]
    [Tooltip("Scale dua texture ca_con ve dung khung UV cua mesh goc.")]
    public Vector2 smallFishUvScale = new Vector2(1.267f, 1.08f);
    [Tooltip("Offset dua dau, mat va vay ca_con ve dung vi tri.")]
    public Vector2 smallFishUvOffset = new Vector2(-0.085f, -0.059f);

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

    [Header("Ca muc day len nhu sua")]
    [Tooltip("Khoang do cao spawn ca_muc gan day, tinh theo 0..1 cua Spawn Bounds.")]
    public Vector2 octopusBottomHeightRange = new Vector2(0.03f, 0.12f);
    public Vector2 octopusRiseSpeedRange = new Vector2(0.85f, 1.35f);
    [Range(0.1f, 1.5f)] public float octopusBurstDuration = 0.55f;
    [Range(0.1f, 2f)] public float octopusGlideDuration = 0.8f;
    [Range(0f, 1.5f)] public float octopusHorizontalDrift = 0.45f;

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
    [Min(1f)] public float clickEscapeSpeed = 2.6f;
    [Range(0.5f, 3f)] public float clickEscapeDuration = 1.4f;
    [Range(0f, 3f)] public float clickEscapeAcceleration = 0.2f;
    [Range(1f, 3f)] public float clickEscapeAnimationSpeed = 1.35f;
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
        if (TryReadPrimaryPointerPress(out Vector2 screenPosition))
            FishClickInteraction.TryTriggerAtScreenPosition(Camera.main, screenPosition);
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
            else if (source.fishId == "ca_muc")
            {
                float bottom01 = Random.Range(
                    Mathf.Min(octopusBottomHeightRange.x, octopusBottomHeightRange.y),
                    Mathf.Max(octopusBottomHeightRange.x, octopusBottomHeightRange.y));
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
        bool isOctopus = source.fishId == "ca_muc";
        Vector2 widthRange = isStarfish ? starfishWidthRange : targetFishWidthRange;
        float targetWidth = Random.Range(
            Mathf.Min(widthRange.x, widthRange.y),
            Mathf.Max(widthRange.x, widthRange.y)) * depthSize * fishSizeMultiplier;
        NormalizeVisualWidth(fish, targetWidth);

        DOTweenFishAnim animation = fish.GetComponent<DOTweenFishAnim>();
        if (animation != null)
        {
            if (isOctopus)
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
        }

        if (isOctopus)
        {
            JellyfishRiseAnim riseAnimation = fish.GetComponent<JellyfishRiseAnim>();
            if (riseAnimation == null)
                riseAnimation = fish.AddComponent<JellyfishRiseAnim>();
            riseAnimation.Configure(
                spawnBounds,
                octopusRiseSpeedRange,
                octopusBurstDuration,
                octopusGlideDuration,
                octopusHorizontalDrift);
        }

        ConfigureClickInteraction(fish);

        if (!isStarfish && !isOctopus)
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
            clickEscapeSpeed,
            clickEscapeDuration,
            clickEscapeAcceleration,
            clickEscapeAnimationSpeed,
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

        animation.swimBounds = spawnBounds;
        animation.lifetime = 999999f;
        animation.useSwimLane = true;
        animation.swimLaneCenter01 = (laneIndex + 0.5f) / laneTotal;
        animation.swimLaneHeight01 = swimLaneHeight;
        animation.waveHeight = Random.Range(calmWaveHeightRange.x, calmWaveHeightRange.y);
        animation.swimSpeed = Random.Range(calmSpeedRange.x, calmSpeedRange.y) *
                              Mathf.Lerp(depthSpeedRange.x, depthSpeedRange.y, depth01);
        animation.waypointsPerSegment = 5;
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
    void FillClownfishLineArtAlpha(Texture2D texture)
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
            // Ca_heo chỉ còn một đường, còn màu ca_con bị đặt sai vùng.
            bool useWhaleEdgeFix = source.fishId == "ca_voi";
            bool useSmallFishAlignment = source.fishId == "ca_con";
            bool useClownfishLineArt = source.fishId == "ca_he";

            // Mesh cá giờ chỉ là 1 quad chữ nhật (không còn cắt theo silhouette
            // như rig cũ), nên texture line-art thuần của ca_he (thân và nền
            // ngoài đều alpha ~0, chỉ nét viền có alpha) phải tự lấp đầy alpha
            // bên trong nét vẽ trước, nếu không cả hình chữ nhật sẽ hiện ra.
            if (useClownfishLineArt)
                FillClownfishLineArtAlpha(texture);

            texture.wrapMode = useWhaleEdgeFix
                ? whaleTextureWrapMode
                : TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            Color baseTint = useClownfishLineArt ? clownfishDefaultTint : Color.white;
            Color emissionTint = fishEmissionTint * baseTint;
            if (PlayerFishTextureApplicator.Apply(
                    fish,
                    texture,
                    fishEmissionIntensity,
                    emissionTint,
                    useWhaleEdgeFix ? whaleUvInset : 0f,
                    useSmallFishAlignment ? smallFishUvScale : Vector2.one,
                    useSmallFishAlignment ? smallFishUvOffset : Vector2.zero,
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
/// Vertical pulse movement used by ca_muc. Kept beside the spawner because this
/// component is created at runtime and belongs exclusively to background fish.
/// </summary>
sealed class JellyfishRiseAnim : MonoBehaviour
{
    public BoxCollider2D swimBounds;
    public Vector2 riseSpeedRange = new Vector2(0.85f, 1.35f);
    public float burstDuration = 0.55f;
    public float glideDuration = 0.8f;
    public float glideSpeedMultiplier = 0.18f;
    public float horizontalDrift = 0.45f;
    public float driftFrequency = 0.65f;
    public float maxSwayAngle = 5f;
    public float pulseScale = 0.08f;

    float riseSpeed;
    float phaseTimer;
    float driftPhase;
    float anchorX;
    bool isBursting = true;
    Vector3 baseScale;

    public void Configure(
        BoxCollider2D bounds,
        Vector2 speedRange,
        float jetDuration,
        float restDuration,
        float drift)
    {
        swimBounds = bounds;
        riseSpeedRange = speedRange;
        burstDuration = Mathf.Max(0.05f, jetDuration);
        glideDuration = Mathf.Max(0.05f, restDuration);
        horizontalDrift = Mathf.Max(0f, drift);
        baseScale = transform.localScale;
        anchorX = transform.position.x;
        RandomizeMotion();
        phaseTimer = Random.Range(0f, burstDuration * 0.35f);
        isBursting = true;
    }

    public void ResumeFromCurrentPosition()
    {
        baseScale = transform.localScale;
        anchorX = transform.position.x;
        if (swimBounds != null)
        {
            Bounds bounds = swimBounds.bounds;
            anchorX = Mathf.Clamp(anchorX, bounds.min.x, bounds.max.x);
            Vector3 position = transform.position;
            position.x = anchorX;
            position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            transform.position = position;
        }

        // Start the sine drift at zero so enabling the component cannot snap X.
        driftPhase = -Time.time * driftFrequency;
        phaseTimer = 0f;
        isBursting = true;
        transform.rotation = Quaternion.identity;
        enabled = true;
    }

    void Awake()
    {
        baseScale = transform.localScale;
        anchorX = transform.position.x;
        RandomizeMotion();
    }

    void Update()
    {
        if (swimBounds == null)
            return;

        phaseTimer += Time.deltaTime;
        float phaseDuration = isBursting ? burstDuration : glideDuration;
        if (phaseTimer >= phaseDuration)
        {
            phaseTimer -= phaseDuration;
            isBursting = !isBursting;
            phaseDuration = isBursting ? burstDuration : glideDuration;
        }

        float phase01 = Mathf.Clamp01(phaseTimer / Mathf.Max(phaseDuration, 0.001f));
        float jetStrength = isBursting
            ? Mathf.Sin(phase01 * Mathf.PI)
            : glideSpeedMultiplier;
        float verticalSpeed = isBursting
            ? Mathf.Lerp(riseSpeed * 0.45f, riseSpeed, jetStrength)
            : riseSpeed * glideSpeedMultiplier;

        Vector3 position = transform.position;
        position.y += verticalSpeed * Time.deltaTime;
        float driftTime = Time.time * driftFrequency + driftPhase;
        position.x = anchorX + Mathf.Sin(driftTime) * horizontalDrift;
        transform.position = position;

        float pulse = isBursting ? jetStrength : 0f;
        transform.localScale = new Vector3(
            baseScale.x * (1f + pulse * pulseScale),
            baseScale.y * (1f - pulse * pulseScale),
            baseScale.z);
        transform.rotation = Quaternion.Euler(
            0f, 0f, Mathf.Sin(driftTime) * maxSwayAngle);

        Bounds bounds = swimBounds.bounds;
        if (position.y <= bounds.max.y + 0.75f)
            return;

        float sidePadding = Mathf.Min(0.75f, bounds.extents.x * 0.15f);
        anchorX = Random.Range(bounds.min.x + sidePadding, bounds.max.x - sidePadding);
        position.x = anchorX;
        position.y = bounds.min.y + Random.Range(
            0.05f, Mathf.Max(0.08f, bounds.size.y * 0.12f));
        transform.position = position;
        transform.localScale = baseScale;
        phaseTimer = 0f;
        isBursting = true;
        RandomizeMotion();
    }

    void RandomizeMotion()
    {
        riseSpeed = Random.Range(
            Mathf.Min(riseSpeedRange.x, riseSpeedRange.y),
            Mathf.Max(riseSpeedRange.x, riseSpeedRange.y));
        driftPhase = Random.Range(0f, Mathf.PI * 2f);
    }
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
    float escapeSpeed = 2.6f;
    float escapeDuration = 1.4f;
    float escapeAcceleration = 0.2f;
    float escapeAnimationSpeed = 1.35f;
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
        float speed,
        float duration,
        float acceleration,
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
        escapeSpeed = Mathf.Max(1f, speed);
        escapeDuration = Mathf.Max(0.2f, duration);
        escapeAcceleration = Mathf.Max(0f, acceleration);
        escapeAnimationSpeed = Mathf.Max(1f, animationSpeed);
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

    void TriggerEscape(Vector3 clickWorldPosition)
    {
        if (isEscaping)
            return;

        isEscaping = true;
        Debug.Log($"[FishClick] '{name}' hoang so va bo chay.", this);

        DOTweenFishAnim swimAnimation = GetComponent<DOTweenFishAnim>();
        bool spriteFacesRight = swimAnimation == null || swimAnimation.spriteFacesRight;
        if (swimAnimation != null)
        {
            // A ca_con member normally follows the school root. Copy that calm
            // motion before detaching so it can continue swimming independently.
            if (!swimAnimation.enabled && transform.parent != null)
            {
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
            }

            swimAnimation.StopSwimmingImmediately();
        }

        JellyfishRiseAnim riseAnimation = GetComponent<JellyfishRiseAnim>();
        if (riseAnimation != null)
            riseAnimation.enabled = false;

        Collider2D clickCollider = GetComponent<Collider2D>();
        if (clickCollider != null)
            clickCollider.enabled = false;

        transform.SetParent(null, true);

        Vector2 awayFromClick = (Vector2)(transform.position - clickWorldPosition);
        float horizontalDirection;
        if (swimBounds != null)
        {
            Bounds bounds = swimBounds.bounds;
            float edgeMargin = bounds.size.x * 0.18f;
            if (transform.position.x <= bounds.min.x + edgeMargin)
                horizontalDirection = 1f;
            else if (transform.position.x >= bounds.max.x - edgeMargin)
                horizontalDirection = -1f;
            else if (Mathf.Abs(awayFromClick.x) > 0.15f)
                horizontalDirection = Mathf.Sign(awayFromClick.x);
            else
                horizontalDirection = transform.position.x <= bounds.center.x ? 1f : -1f;
        }
        else
        {
            horizontalDirection = Mathf.Abs(awayFromClick.x) > 0.15f
                ? Mathf.Sign(awayFromClick.x)
                : (Random.value < 0.5f ? -1f : 1f);
        }

        Vector2 escapeDirection;
        if (riseAnimation != null)
        {
            // ca_muc propels itself upward like a jellyfish instead of fleeing
            // horizontally like the regular fish.
            escapeDirection = new Vector2(horizontalDirection * 0.2f, 1f).normalized;
        }
        else
        {
            // Keep regular fish readable: never allow a near-vertical dive/rise.
            float verticalSlope = Mathf.Clamp(
                awayFromClick.y / Mathf.Max(Mathf.Abs(awayFromClick.x), 0.35f),
                -0.28f,
                0.28f);
            verticalSlope = Mathf.Clamp(
                verticalSlope + Random.Range(-0.04f, 0.08f),
                -0.28f,
                0.28f);
            escapeDirection = new Vector2(horizontalDirection, verticalSlope).normalized;
        }

        if (swimAnimation != null)
            swimAnimation.SetWaveFrequencyMultiplier(escapeAnimationSpeed);

        if (riseAnimation == null)
            FaceEscapeDirection(escapeDirection.x, spriteFacesRight);
        CreateBubbleBurst();
        ParticleSystem trail = bubbleTrailEnabled ? CreateBubbleTrail() : null;
        StartCoroutine(EscapeRoutine(
            escapeDirection,
            trail,
            swimAnimation,
            riseAnimation,
            clickCollider));
    }

    void FaceEscapeDirection(float horizontalDirection, bool spriteFacesRight)
    {
        if (Mathf.Abs(horizontalDirection) < 0.01f)
            return;

        Vector3 scale = transform.localScale;
        float absoluteX = Mathf.Abs(scale.x);
        bool moveRight = horizontalDirection > 0f;
        scale.x = moveRight == spriteFacesRight ? absoluteX : -absoluteX;
        transform.localScale = scale;
    }

    IEnumerator EscapeRoutine(
        Vector2 direction,
        ParticleSystem trail,
        DOTweenFishAnim swimAnimation,
        JellyfishRiseAnim riseAnimation,
        Collider2D clickCollider)
    {
        float timer = 0f;
        float targetAngle = 0f;
        if (riseAnimation == null)
        {
            targetAngle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
            targetAngle *= direction.x >= 0f ? 1f : -1f;
        }

        while (timer < escapeDuration)
        {
            float normalizedTime = timer / escapeDuration;
            float frameEnd01 = Mathf.Clamp01(
                (timer + Time.deltaTime) / Mathf.Max(escapeDuration, 0.001f));
            float levelOut01 = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.58f, 1f, frameEnd01));

            float acceleratedSpeed = escapeSpeed *
                                     (1f + normalizedTime * escapeAcceleration);
            float currentSpeed = Mathf.Lerp(
                acceleratedSpeed,
                escapeSpeed * 0.72f,
                levelOut01);
            Vector3 nextPosition = transform.position +
                                   (Vector3)(direction * currentSpeed * Time.deltaTime);
            if (swimBounds != null)
            {
                Bounds bounds = swimBounds.bounds;
                nextPosition.x = Mathf.Clamp(nextPosition.x, bounds.min.x, bounds.max.x);
                nextPosition.y = Mathf.Clamp(nextPosition.y, bounds.min.y, bounds.max.y);
            }
            transform.position = nextPosition;
            float wobble = Mathf.Sin(timer * 18f) * 2.5f *
                           (1f - normalizedTime) * (1f - levelOut01);
            float leveledAngle = Mathf.Lerp(targetAngle, 0f, levelOut01);
            transform.rotation = Quaternion.Euler(0f, 0f, leveledAngle + wobble);

            // Khớp tần số vẫy theo đà giảm tốc thay vì nhảy đột ngột từ tốc độ
            // hoảng sợ về bình thường ở frame cuối.
            if (swimAnimation != null)
            {
                float frequencyMultiplier = Mathf.Lerp(escapeAnimationSpeed, 1f, levelOut01);
                swimAnimation.SetWaveFrequencyMultiplier(frequencyMultiplier);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(trail.gameObject, 1.5f);
        }

        if (swimAnimation != null)
            swimAnimation.SetWaveFrequencyMultiplier(1f);

        transform.rotation = Quaternion.identity;
        if (clickCollider != null)
            clickCollider.enabled = true;

        int resumeDirection = direction.x >= 0f ? 1 : -1;
        if (riseAnimation != null)
        {
            riseAnimation.ResumeFromCurrentPosition();
        }
        else if (swimAnimation != null)
        {
            swimAnimation.swimBounds = swimBounds;
            swimAnimation.ResumeSwimmingFromCurrentPosition(resumeDirection);
        }

        isEscaping = false;
    }

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

    ParticleSystem CreateBubbleTrail()
    {
        ParticleSystem particles = CreateParticleSystem("FishEscapeBubbleTrail", transform.position);
        particles.transform.SetParent(transform, true);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.duration = Mathf.Max(0.5f, escapeDuration);
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
        return particles;
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
