using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cầu nối giữa vị trí khách tham quan và OceanBubbleSystem.
///
/// Osc: MỖI khách đang track (qua 1 hoặc nhiều OscPersonTracker) là 1 nguồn phát bong bóng
/// RIÊNG (OceanBubbleSystem.SetEmitPoint(id, point)) - hỗ trợ nhiều khách cùng lúc (tới ~100
/// người), mỗi người có cụm bong bóng bám theo đúng vị trí của mình thay vì gộp chung về 1 điểm.
/// id của track được giữ ổn định qua các frame (xem OscPersonTracker) nên burst/rate riêng của
/// từng nguồn phát không bị "giật" khi có nhiều người cùng di chuyển.
///
/// Mouse: chỉ 1 con trỏ nên vẫn dùng emitter id 0 (SetEmitPoint(point) cũ) để test trong Editor.
/// </summary>
public class TouchOceanManager : MonoBehaviour
{
    public enum InteractionSource { Osc, Mouse }

    [Tooltip("Osc = lay vi tri khach tu OscPersonTracker (san xuat thuc te). " +
             "Mouse = dung chuot/cham de test trong Editor khi chua co nguon OSC.")]
    public InteractionSource interactionSource = InteractionSource.Osc;

    [Tooltip("Cac nguon vi tri khach qua OSC (co the gan nhieu neu co nhieu cam bien/vung track " +
             "rieng biet). Bat buoc it nhat 1 phan tu khi interactionSource = Osc.")]
    public OscPersonTracker[] oscTrackers;

    [Tooltip("Hệ thống bong bóng - mỗi khách OSC sẽ là 1 nguồn phát riêng trên hệ thống này.")]
    public OceanBubbleSystem bubbleSystem;

    [Tooltip("Camera dùng để quy đổi toạ độ màn hình sang world. Để trống = tự lấy Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Độ sâu (world Z) của mặt phẳng tương tác - nơi bong bóng được quy chiếu từ toạ độ " +
             "màn hình. Nên đặt gần mặt kính/khu vực cá bơi để cảm giác chạm đúng vị trí trong bể.")]
    public float interactionDepth = -5f;

    readonly HashSet<int> _activeOscEmitterIds = new HashSet<int>();

    void Update()
    {
        if (bubbleSystem == null)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        if (interactionSource == InteractionSource.Osc)
            UpdateOscEmitters(cam);
        else
            UpdateMouseEmitter(cam);
    }

    void UpdateMouseEmitter(Camera cam)
    {
        bool hasPointer = TryReadHeldPointer(out Vector2 screenPosition);
        bubbleSystem.SetEmitPoint(hasPointer ? ScreenToWorld(cam, screenPosition) : (Vector3?)null);
    }

    void UpdateOscEmitters(Camera cam)
    {
        _activeOscEmitterIds.Clear();

        if (oscTrackers != null)
        {
            for (int ti = 0; ti < oscTrackers.Length; ti++)
            {
                var tracker = oscTrackers[ti];
                if (tracker == null) continue;

                var tracks = tracker.ActiveTracks;
                for (int i = 0; i < tracks.Count; i++)
                {
                    int emitterId = GlobalEmitterId(ti, tracks[i].id);
                    _activeOscEmitterIds.Add(emitterId);
                    bubbleSystem.SetEmitPoint(emitterId, ScreenToWorld(cam, tracks[i].screenPos));
                }
            }
        }

        // Tat nguon phat cua nhung khach da roi (khong con trong danh sach frame nay).
        bubbleSystem.StopEmittersExcept(_activeOscEmitterIds);
    }

    // Ghep (chi so tracker, id track cuc bo) thanh 1 id toan cuc, tranh dung id giua nhieu
    // OscPersonTracker (moi tracker tu danh so id rieng tu 0).
    static int GlobalEmitterId(int trackerIndex, int localTrackId) => trackerIndex * 100000 + localTrackId;

    Vector3 ScreenToWorld(Camera cam, Vector2 screenPosition)
    {
        float distanceFromCamera = interactionDepth - cam.transform.position.z;
        return cam.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera));
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
        bubbleSystem?.StopAllEmitters();
    }
}
