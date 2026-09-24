using System.Collections;
using DG.Tweening;
using UnityEngine;

// Shader setup plus the one-shot water-entry animation. Continuous movement,
// facing, tilt and ambient spin remain intentionally absent.
public class DOTweenFishAnim : MonoBehaviour
{
    [Header("Huong dau prefab")]
    public bool spriteFacesRight = true;

    [Header("Vay duoi (shader FishBody)")]
    [Min(2)] public int bodyMeshSegments = 12;
    public bool useRadialDeform;
    [Min(1f)] public float radialArmCount = 5f;
    public bool useVerticalBodyAxis;
    [Min(0f)] public float waveAmplitude = 0.3f;
    [Min(0f)] public float waveFrequencyPerSpeed = 1.4f;
    public float waveLength = 2.5f;
    [Range(0f, 2f)] public float turnAmplitudeBoost = 0.6f;
    [Range(0f, 0.95f)] public float tailWaveStart = 0.72f;

    [Header("Vay nguc (shader FishBody)")]
    [Range(0f, 1f)] public float finBandCenter = 0.18f;
    [Range(0.02f, 0.5f)] public float finBandWidth = 0.12f;
    [Min(0f)] public float finWaveFrequency = 6f;
    [Min(0f)] public float finWaveAmplitude;

    [Header("Thong so shader tuong thich prefab cu")]
    [Min(0.05f)] public float twistTravelDuration = 0.5f;
    [Min(0.05f)] public float twistSpinDuration = 0.35f;
    [Min(0.5f)] public float twistFlailCycles = 2.5f;
    [Range(5f, 90f)] public float twistMaxAngleDegrees = 35f;

    static Mesh sharedBodyMesh;
    const float SharkWaveAmplitude = 0.7f;
    const float SharkWaveFrequency = 1.7f;
    const float StandardWaveAmplitude = 0.35f;
    const float StandardWaveFrequency = 1f;
    const float StandardTurnAmplitudeBoost = 0.6f;
    const float StandardTailWaveStart = 0.72f;
    Material bodyMaterial;
    float motionSeed;
    Tween turnBendTween;
    float turnBendWeight;
    bool dropPrepared;
    Vector3 dropLandingPosition;
    float dropWaterSurfaceY;
    float dropDuration = 1.2f;
    float dropAirPhaseRatio = 0.28f;
    float dropSettlePhaseRatio = 0.2f;
    float dropSinkOvershoot = 0.22f;
    float dropImpactTilt = 9f;
    Sequence dropSequence;
    Coroutine dropBubbleTrailCoroutine;
    [Min(0)] public int waterEntryBubbleCount = 18;

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

    void Awake()
    {
        ApplyBodyMeshAndShaderSettings();
    }

    void Start()
    {
        if (dropPrepared)
            PlayWaterDrop();
    }

    void PlayWaterDrop()
    {
        float settleRatio = Mathf.Clamp(dropSettlePhaseRatio, 0.1f, 0.4f);
        float airDuration = dropDuration * dropAirPhaseRatio;
        float settleDuration = dropDuration * settleRatio;
        float underwaterDuration = Mathf.Max(0.15f, dropDuration - airDuration - settleDuration);
        Vector3 waterEntryPosition = new Vector3(
            dropLandingPosition.x,
            dropWaterSurfaceY,
            dropLandingPosition.z);
        Vector3 submergedPosition = dropLandingPosition;
        submergedPosition.y -= dropSinkOvershoot;
        float entryTilt = dropImpactTilt * (Random.value < 0.5f ? -1f : 1f);

        dropSequence = DOTween.Sequence();
        dropSequence.Append(transform.DOMove(waterEntryPosition, airDuration).SetEase(Ease.InQuad));
        dropSequence.Join(transform.DORotate(
            new Vector3(0f, 0f, entryTilt), airDuration).SetEase(Ease.InSine));
        dropSequence.AppendCallback(() =>
            EmitBubbleBurst(waterEntryPosition, waterEntryBubbleCount, 1f, 1.2f));
        dropSequence.Append(transform.DOMove(submergedPosition, underwaterDuration).SetEase(Ease.OutCubic));
        dropSequence.Join(transform.DORotate(
            new Vector3(0f, 0f, -entryTilt * 0.35f), underwaterDuration).SetEase(Ease.InOutSine));
        dropSequence.Append(transform.DOMove(dropLandingPosition, settleDuration).SetEase(Ease.OutSine));
        dropSequence.Join(transform.DORotate(Vector3.zero, settleDuration).SetEase(Ease.OutSine));
        dropSequence.OnComplete(() =>
        {
            if (dropBubbleTrailCoroutine != null)
            {
                StopCoroutine(dropBubbleTrailCoroutine);
                dropBubbleTrailCoroutine = null;
            }

            transform.position = dropLandingPosition;
            transform.rotation = Quaternion.identity;
            FishRandomMotion randomMotion = GetComponent<FishRandomMotion>();
            randomMotion?.BeginMotion();
            dropSequence = null;
        });
        dropBubbleTrailCoroutine = StartCoroutine(EmitDropBubbleTrail(
            dropDuration, waterEntryPosition, dropLandingPosition));
    }

    IEnumerator EmitDropBubbleTrail(float duration, Vector3 startPosition, Vector3 endPosition)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            Vector3 position = Vector3.Lerp(startPosition, endPosition, progress);
            position.x += Random.Range(-0.12f, 0.12f);
            position.y += Random.Range(-0.08f, 0.15f);
            EmitBubbleBurst(position, Mathf.Clamp(Mathf.RoundToInt(waterEntryBubbleCount * 0.35f), 1, 5), 0.6f, 0.8f);

            float delay = Mathf.Lerp(0.14f, 0.32f, progress);
            elapsed += delay;
            yield return new WaitForSeconds(delay);
        }

        dropBubbleTrailCoroutine = null;
    }

    void EmitBubbleBurst(Vector3 position, int count, float radiusMultiplier, float energyMultiplier)
    {
        if (count <= 0)
            return;

        OceanBubbleSystem bubbleSystem = FindFirstObjectByType<OceanBubbleSystem>();
        bubbleSystem?.Burst(position, count, radiusMultiplier, energyMultiplier);
    }

    void OnDestroy()
    {
        if (dropBubbleTrailCoroutine != null)
            StopCoroutine(dropBubbleTrailCoroutine);
        dropSequence?.Kill();
        turnBendTween?.Kill();
    }

    void ApplyBodyMeshAndShaderSettings()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
            meshFilter.sharedMesh = GetOrCreateBodyMesh(bodyMeshSegments);

        Renderer renderer = GetComponent<Renderer>();
        bodyMaterial = renderer != null ? renderer.sharedMaterial : null;
        motionSeed = Random.Range(0f, 1000f);

        if (bodyMaterial == null)
            return;

        SetShaderFloat("_WaveAmplitude", waveAmplitude);
        SetShaderFloat("_WaveFrequency", waveFrequencyPerSpeed);
        SetShaderFloat("_WaveLength", waveLength);
        SetShaderFloat("_DeformMode", useRadialDeform ? 1f : 0f);
        SetShaderFloat("_ArmCount", radialArmCount);
        SetShaderFloat("_BodyAxis", useVerticalBodyAxis ? 1f : 0f);
        SetShaderFloat("_WavePhase", motionSeed);
        SetShaderFloat("_TailWaveStart", tailWaveStart);
        SetShaderFloat("_FinBandCenter", finBandCenter);
        SetShaderFloat("_FinBandWidth", finBandWidth);
        SetShaderFloat("_FinWaveFrequency", finWaveFrequency);
        SetShaderFloat("_FinWaveAmplitude", finWaveAmplitude);
        SetShaderFloat("_TwistTravelDuration", twistTravelDuration);
        SetShaderFloat("_TwistSpinDuration", twistSpinDuration);
        SetShaderFloat("_TwistFlailCycles", twistFlailCycles);
        SetShaderFloat("_TwistMaxAngle", twistMaxAngleDegrees);
        SetShaderFloat("_TwistTriggerTime", -1000f);
    }

    void SetShaderFloat(string propertyName, float value)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null && material.HasProperty(propertyName))
                    material.SetFloat(propertyName, value);
            }
        }

        if (bodyMaterial != null && bodyMaterial.HasProperty(propertyName))
            bodyMaterial.SetFloat(propertyName, value);
    }

    public void ConfigureTailOnlyMotion(string fishId)
    {
        if (string.IsNullOrWhiteSpace(fishId) ||
            !fishId.Trim().Equals("ca_vuoc", System.StringComparison.OrdinalIgnoreCase))
            return;

        // Ca vuoc chi quay day duoi: giu dau cung, tat song vay nguc va
        // khong boost bien do khi re de tranh than truoc bi quay.
        tailWaveStart = 0.86f;
        finWaveAmplitude = 0f;
        turnAmplitudeBoost = 0f;
        useRadialDeform = false;
        useVerticalBodyAxis = false;

        SetShaderFloat("_DeformMode", 0f);
        SetShaderFloat("_BodyAxis", 0f);
        SetShaderFloat("_TailSide", 0f);
        SetShaderFloat("_TailWaveStart", tailWaveStart);
        SetShaderFloat("_FinWaveAmplitude", finWaveAmplitude);
    }

    public void ConfigureStandardFishMotion(string fishId)
    {
        string normalizedId = fishId != null
            ? fishId.Trim().ToLowerInvariant()
            : string.Empty;
        if (normalizedId == "ca_muc" || normalizedId == "tom" ||
            normalizedId == "sua" || normalizedId == "ca_ngua" ||
            normalizedId == "rua" || normalizedId == "cua" ||
            normalizedId == "sao_bien")
            return;

        // Ca map giu bien do/tan so rieng; cac loai con lai dung profile nhe hon.
        bool isShark = normalizedId == "ca_map";
        useRadialDeform = false;
        useVerticalBodyAxis = false;
        waveAmplitude = isShark ? SharkWaveAmplitude : StandardWaveAmplitude;
        waveFrequencyPerSpeed = isShark ? SharkWaveFrequency : StandardWaveFrequency;
        waveLength = 2.5f;
        turnAmplitudeBoost = StandardTurnAmplitudeBoost;
        tailWaveStart = StandardTailWaveStart;
        finWaveAmplitude = 0f;

        SetShaderFloat("_DeformMode", 0f);
        SetShaderFloat("_BodyAxis", 0f);
        SetShaderFloat("_TailSide", spriteFacesRight ? 1f : 0f);
        SetShaderFloat("_WaveAmplitude", waveAmplitude);
        SetShaderFloat("_WaveFrequency", waveFrequencyPerSpeed);
        SetShaderFloat("_WaveLength", waveLength);
        SetShaderFloat("_TailWaveStart", tailWaveStart);
        SetShaderFloat("_FinWaveAmplitude", finWaveAmplitude);
    }

    public void TriggerTurnBend()
    {
        if (turnAmplitudeBoost <= 0f || bodyMaterial == null)
            return;

        turnBendTween?.Kill();
        turnBendWeight = 0f;
        turnBendTween = DOTween.To(
                () => turnBendWeight,
                value =>
                {
                    turnBendWeight = value;
                    SetShaderFloat(
                        "_WaveAmplitude",
                        waveAmplitude * (1f + turnAmplitudeBoost * value));
                },
                1f,
                0.12f)
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo)
            .OnComplete(() =>
            {
                SetShaderFloat("_WaveAmplitude", waveAmplitude);
                turnBendTween = null;
            });
    }

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
            float positionX = u - 0.5f;
            vertices[x * 2] = new Vector3(positionX, -0.5f, 0f);
            vertices[x * 2 + 1] = new Vector3(positionX, 0.5f, 0f);
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
}