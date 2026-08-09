using UnityEngine;
using DG.Tweening;

public class DOTweenFishAnim : MonoBehaviour
{
    [Header("Vùng bơi (Kéo thả GameObject có BoxCollider2D vào đây)")]
    public BoxCollider2D swimBounds;

    [Header("Cài đặt Spawn")]
    public float spawnDuration = 0.5f;

    [Header("Cài đặt Bơi")]
    public float swimSpeed = 3f;
    public int waypointsPerSegment = 5;

    [Header("Cài đặt Đường bơi")]
    [Tooltip("Biên độ sóng lên xuống tối đa (trục Y)")]
    public float waveHeight = 2f;
    [Tooltip("Giới hạn góc nghiêng khi chúc đầu (độ)")]
    public float maxTiltAngle = 20f;

    [Header("Cài đặt Hướng cá")]
    public bool spriteFacesRight = true;

    [Header("Thời gian tồn tại")]
    public float lifetime = 15f;
    public float fadeOutDuration = 1f;

    private Vector3 lastPos;
    private Bounds bounds;
    private float lifeTimer;
    private bool isDying = false;
    private SpriteRenderer spriteRenderer;
    private Tween currentPathTween;
    private int currentDirection = -1; // -1: bơi sang trái, 1: bơi sang phải

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        lastPos = transform.position;
        lifeTimer = lifetime;

        // Lấy vùng bơi từ BoxCollider2D
        if (swimBounds != null)
        {
            bounds = swimBounds.bounds;
        }
        else
        {
            if (Camera.main != null && Camera.main.orthographic)
            {
                float h = Camera.main.orthographicSize;
                float w = h * Camera.main.aspect;
                Vector3 camPos = Camera.main.transform.position;
                bounds = new Bounds(camPos, new Vector3(w * 2f, h * 2f, 10f));
            }
            else
            {
                bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 10f));
            }
            Debug.LogWarning("[DOTweenFishAnim] Chưa gắn Swim Bounds! Đang dùng camera làm vùng bơi mặc định.");
        }

        ClampPositionToBounds();

        // Chọn hướng bơi ban đầu: bơi về phía nào có nhiều khoảng trống hơn
        float distToLeft = transform.position.x - bounds.min.x;
        float distToRight = bounds.max.x - transform.position.x;
        currentDirection = distToRight >= distToLeft ? 1 : -1;

        // Hiệu ứng Spawn
        Vector3 originalScale = transform.localScale;
        transform.localScale = Vector3.zero;
        transform.DOScale(originalScale, spawnDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => StartNewSwimSegment());
    }

    void Update()
    {
        if (isDying) return;

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Tạo một đoạn đường bơi mới: trải đều theo trục X, chỉ random Y
    /// Khi bơi đến mép bên kia thì đổi hướng quay lại
    /// </summary>
    void StartNewSwimSegment()
    {
        if (isDying) return;

        // Đổi hướng bơi (bơi ngược lại)
        currentDirection *= -1;

        Vector3[] path = GenerateSmoothPath();
        float totalDistance = CalculatePathLength(path);
        float duration = totalDistance / Mathf.Max(swimSpeed, 0.1f);

        if (currentPathTween != null && currentPathTween.IsActive())
        {
            currentPathTween.Kill();
        }

        currentPathTween = transform.DOPath(path, duration, PathType.CatmullRom)
            .SetEase(Ease.Linear)
            .OnUpdate(UpdateFishDirection)
            .OnComplete(() => StartNewSwimSegment());
    }

    /// <summary>
    /// Sinh đường bơi mềm mại:
    /// - Các điểm trải ĐỀU theo trục X (từ trái sang phải hoặc ngược lại)
    /// - Chỉ random nhẹ trục Y → tạo sóng lên xuống mượt mà
    /// - KHÔNG BAO GIỜ lặp lại hoặc đè lên nhau
    /// </summary>
    Vector3[] GenerateSmoothPath()
    {
        Vector3[] path = new Vector3[waypointsPerSegment];
        float padding = 1f;

        float minX = bounds.min.x + padding;
        float maxX = bounds.max.x - padding;
        float minY = bounds.min.y + padding;
        float maxY = bounds.max.y - padding;

        // Giới hạn waveHeight để không vượt bounds
        float clampedWaveHeight = Mathf.Min(waveHeight, (maxY - minY) / 2f);

        // Điểm bắt đầu = vị trí hiện tại của cá
        float startX = transform.position.x;
        // Điểm kết thúc = mép bên kia (theo hướng bơi)
        float endX = currentDirection > 0 ? maxX : minX;

        // Chia đều khoảng cách X cho các waypoint
        float totalDistX = endX - startX;

        for (int i = 0; i < waypointsPerSegment; i++)
        {
            // Tiến đều theo X
            float t = (float)(i + 1) / waypointsPerSegment;
            float x = startX + totalDistX * t;

            // Random Y nhẹ nhàng quanh tâm bounds, giữ trong giới hạn
            float centerY = bounds.center.y;
            float randomY = centerY + Random.Range(-clampedWaveHeight, clampedWaveHeight);
            randomY = Mathf.Clamp(randomY, minY, maxY);

            path[i] = new Vector3(x, randomY, transform.position.z);
        }

        return path;
    }

    float CalculatePathLength(Vector3[] path)
    {
        float length = 0f;
        Vector3 prev = transform.position;
        for (int i = 0; i < path.Length; i++)
        {
            length += Vector3.Distance(prev, path[i]);
            prev = path[i];
        }
        return length;
    }

    void UpdateFishDirection()
    {
        Vector3 moveDir = transform.position - lastPos;

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            // --- Lật ảnh theo hướng bơi ---
            float baseScale = Mathf.Abs(transform.localScale.x);
            float targetScaleX;

            if (moveDir.x < 0)
            {
                targetScaleX = spriteFacesRight ? -baseScale : baseScale;
            }
            else
            {
                targetScaleX = spriteFacesRight ? baseScale : -baseScale;
            }

            transform.localScale = new Vector3(targetScaleX, transform.localScale.y, transform.localScale.z);

            // --- Chúc đầu lên/xuống theo hướng bơi (giới hạn góc nghiêng) ---
            float angle = Mathf.Atan2(moveDir.y, Mathf.Abs(moveDir.x)) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, -maxTiltAngle, maxTiltAngle);

            Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        lastPos = transform.position;
    }

    void Die()
    {
        if (isDying) return;
        isDying = true;

        if (currentPathTween != null && currentPathTween.IsActive())
        {
            currentPathTween.Kill();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.DOFade(0f, fadeOutDuration)
                .OnComplete(() => Destroy(gameObject));
        }
        else
        {
            Destroy(gameObject, fadeOutDuration);
        }
    }

    void ClampPositionToBounds()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, bounds.min.x, bounds.max.x);
        pos.y = Mathf.Clamp(pos.y, bounds.min.y, bounds.max.y);
        transform.position = pos;
    }

    void OnDestroy()
    {
        transform.DOKill();
        if (spriteRenderer != null)
        {
            spriteRenderer.DOKill();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (swimBounds != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawCube(swimBounds.bounds.center, swimBounds.bounds.size);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(swimBounds.bounds.center, swimBounds.bounds.size);
        }
    }
}
