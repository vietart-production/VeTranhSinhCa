using UnityEngine;

public class StickerAnim : MonoBehaviour
{
    [Header("Cấu hình Lắc lư (Wobble)")]
    [Tooltip("Tốc độ lắc lư qua lại trên trục Z")]
    public float wobbleSpeed = 2f;
    [Tooltip("Góc lắc lư tối đa (độ)")]
    public float wobbleAngle = 10f;

    [Header("Cấu hình Bơi lên xuống (Bobbing)")]
    [Tooltip("Tốc độ nhấp nhô lên xuống trên trục Y")]
    public float bobbingSpeed = 1.5f;
    [Tooltip("Biên độ nhấp nhô (đơn vị Unity)")]
    public float bobbingHeight = 0.3f;

    [Header("Cấu hình Di chuyển (Swimming)")]
    [Tooltip("Tốc độ bơi ngang trên trục X")]
    public float swimSpeed = 1f;
    [Tooltip("Hướng bơi: -1 = sang trái, 1 = sang phải")]
    public float swimDirection = -1f;

    [Header("Giới hạn màn hình (Orthographic)")]
    [Tooltip("Tự động tính giới hạn màn hình dựa vào camera Orthographic")]
    public bool autoScreenBounds = true;
    [Tooltip("Tự hủy khi bơi ra ngoài màn hình")]
    public bool destroyOffScreen = true;
    [Tooltip("Khoảng đệm thêm ngoài rìa màn hình trước khi hủy (đơn vị Unity)")]
    public float offScreenPadding = 3f;

    private float startY;
    private float timeOffset;
    private float screenMinX;
    private float screenMaxX;

    void Start()
    {
        startY = transform.position.y;

        // Random timeOffset để các con cá không bơi đều đều giống hệt nhau
        timeOffset = Random.Range(0f, 10f);

        // Tính giới hạn trái phải của màn hình dựa vào camera Orthographic
        if (autoScreenBounds && Camera.main != null && Camera.main.orthographic)
        {
            float orthoHeight = Camera.main.orthographicSize;
            float orthoWidth = orthoHeight * Camera.main.aspect;
            float camX = Camera.main.transform.position.x;

            screenMinX = camX - orthoWidth - offScreenPadding;
            screenMaxX = camX + orthoWidth + offScreenPadding;
        }
        else
        {
            // Fallback nếu không có camera Orthographic
            screenMinX = -20f;
            screenMaxX = 20f;
        }
    }

    void Update()
    {
        // 1. Hiệu ứng lắc lư (Wobble xoay qua lại trên trục Z - mặt phẳng XY)
        float currentAngle = Mathf.Sin((Time.time + timeOffset) * wobbleSpeed) * wobbleAngle;
        transform.rotation = Quaternion.Euler(0, 0, currentAngle);

        // 2. Hiệu ứng bơi nhấp nhô lên xuống (Bobbing trên trục Y)
        float currentY = startY + Mathf.Sin((Time.time + timeOffset) * bobbingSpeed) * bobbingHeight;

        // 3. Hiệu ứng bơi ngang (Swimming trên trục X)
        float currentX = transform.position.x + (swimSpeed * swimDirection * Time.deltaTime);

        // Cập nhật vị trí (Z giữ nguyên, chỉ di chuyển trên XY)
        transform.position = new Vector3(currentX, currentY, transform.position.z);

        // Tự hủy khi bơi ra ngoài màn hình (dựa vào giới hạn Orthographic)
        if (destroyOffScreen)
        {
            if (transform.position.x < screenMinX || transform.position.x > screenMaxX)
            {
                Destroy(gameObject);
            }
        }
    }
}
