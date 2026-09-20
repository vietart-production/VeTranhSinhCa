using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cầu nối giữa input chạm/chuột của khách tham quan và OceanBubbleSystem: đang giữ
/// tay/chuột ở đâu trên màn hình thì quy chiếu ra world position (tại interactionDepth)
/// và gọi OceanBubbleSystem.SetEmitPoint() mỗi frame; thả tay thì gọi SetEmitPoint(null)
/// để tắt nguồn phát. OceanBubbleSystem tự lo burst lúc chạm/thả (autoBurstOnStartStop).
/// </summary>
public class TouchOceanManager : MonoBehaviour
{
    [Tooltip("Hệ thống bong bóng sẽ nhận điểm phát mỗi frame.")]
    public OceanBubbleSystem bubbleSystem;

    [Tooltip("Camera dùng để quy đổi toạ độ màn hình sang world. Để trống = tự lấy Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Độ sâu (world Z) của mặt phẳng tương tác - nơi bong bóng được quy chiếu từ toạ độ " +
             "màn hình. Nên đặt gần mặt kính/khu vực cá bơi để cảm giác chạm đúng vị trí trong bể.")]
    public float interactionDepth = -5f;

    void Update()
    {
        if (bubbleSystem == null)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        if (TryReadHeldPointer(out Vector2 screenPosition))
        {
            float distanceFromCamera = interactionDepth - cam.transform.position.z;
            Vector3 worldPosition = cam.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera));
            bubbleSystem.SetEmitPoint(worldPosition);
        }
        else
        {
            bubbleSystem.SetEmitPoint(null);
        }
    }

    static bool TryReadHeldPointer(out Vector2 screenPosition)
    {
        screenPosition = default;

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            screenPosition = Mouse.current.position.ReadValue();
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

    void OnDisable()
    {
        bubbleSystem?.SetEmitPoint(null);
    }
}
