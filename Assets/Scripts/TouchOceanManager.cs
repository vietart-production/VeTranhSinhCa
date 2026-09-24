using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Cầu nối giữa vị trí khách tham quan và OceanBubbleSystem: MỖI khách (qua OSC) và/hoặc
/// chuột/chạm (test trong Editor) là 1 nguồn phát riêng theo id. Vị trí màn hình được quy
/// chiếu ra world (tại interactionDepth) và gọi OceanBubbleSystem.SetEmitPoint(id, point)
/// mỗi frame; khách rời vùng / thả tay thì nguồn đó tự tắt (kèm release burst).
///
/// Đồng thời DỌA CÁ tại vị trí từng khách OSC (giống click chuột vào cá của
/// BackgroundFishSpawner) — gom 1 chỗ để không phải sửa BackgroundFishSpawner.
///
/// oscTrackers để TRỐNG = tự dùng mọi OscPersonTracker trong scene (OscPersonTracker.All),
/// tránh lỗi quên gán trong Inspector làm bong bóng không bao giờ phát.
/// </summary>
public class TouchOceanManager : MonoBehaviour
{
    // Thêm giá trị MỚI ở cuối để không làm lệch giá trị đã lưu trong scene
    public enum InteractionSource { Osc, Mouse, OscAndMouse }

    [Tooltip("Osc = lay vi tri khach tu OscPersonTracker (san xuat thuc te). " +
             "Mouse = dung chuot/cham de test trong Editor khi chua co nguon OSC. " +
             "OscAndMouse = ca hai cung luc (vua chay OSC vua test bang chuot).")]
    public InteractionSource interactionSource = InteractionSource.OscAndMouse;

    [Tooltip("Cac nguon vi tri khach qua OSC. De TRONG = tu dung moi OscPersonTracker dang " +
             "bat trong scene. Chi gan tay khi muon gioi han rieng vai tracker.")]
    public OscPersonTracker[] oscTrackers;

    [Tooltip("So khach toi da duoc phat bong bong cung luc (uu tien khach vao vung truoc). " +
             "Van an toan FPS khi dong nguoi; maxAlive cua OceanBubbleSystem van la tran cuoi.")]
    [Min(1)] public int maxSimultaneousPeople = 8;

    [Header("Doa ca bang OSC")]
    [Tooltip("Khach OSC doa ca nhu click chuot: ca boi vao vi tri khach se giat minh bo chay. " +
             "Chuot/cham da duoc BackgroundFishSpawner xu ly san nen o day chi lo OSC.")]
    public bool scareFishWithOsc = true;
    [Tooltip("Chu ky (giay) kiem tra khach-ca. 10 lan/giay la du voi nguoi di bo, re hon nhieu " +
             "so voi moi frame x moi khach x moi con ca.")]
    [Range(0f, 0.5f)] public float fishScareCheckInterval = 0.1f;

    [Header("Bong bong")]
    [Tooltip("Hệ thống bong bóng sẽ nhận điểm phát mỗi frame.")]
    public OceanBubbleSystem bubbleSystem;

    [Tooltip("Camera dùng để quy đổi toạ độ màn hình sang world. Để trống = tự lấy Camera.main.")]
    public Camera targetCamera;

    [Tooltip("Độ sâu (world Z) của mặt phẳng tương tác - nơi bong bóng được quy chiếu từ toạ độ " +
             "màn hình. Nên đặt gần mặt kính/khu vực cá bơi để cảm giác chạm đúng vị trí trong bể.")]
    public float interactionDepth = -5f;

    // id am cho chuot, khong trung voi id khach OSC (luon duong, bat dau tu 1)
    const int MouseEmitterId = -1;

    readonly List<OscPersonTracker.Person> _people = new List<OscPersonTracker.Person>();
    readonly HashSet<int> _activeIds = new HashSet<int>();
    bool _warnedNoTracker;
    float _nextFishScareTime;

    void Update()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        bool useOsc = interactionSource != InteractionSource.Mouse;
        if (useOsc)
        {
            OscPersonTracker.CollectPeople(oscTrackers, _people);
            WarnIfNoTracker();
            if (scareFishWithOsc)
                ScareFishAtPeople(cam);
        }

        if (bubbleSystem == null)
            return;

        _activeIds.Clear();

        if (useOsc)
            EmitFromOsc(cam);

        if (interactionSource != InteractionSource.Osc && TryReadHeldPointer(out Vector2 mousePosition))
        {
            bubbleSystem.SetEmitPoint(MouseEmitterId, ScreenToWorld(cam, mousePosition));
            _activeIds.Add(MouseEmitterId);
        }

        // Khach da roi vung / tha tay -> tat dung nguon cua ho (co release burst)
        bubbleSystem.StopEmittersExcept(_activeIds);
    }

    void WarnIfNoTracker()
    {
        if (_warnedNoTracker || OscPersonTracker.All.Count > 0) return;
        _warnedNoTracker = true;
        Debug.LogWarning("[TouchOceanManager] Dang o che do OSC nhung scene khong co OscPersonTracker " +
                         "nao dang bat - bong bong/doa ca chi chay bang chuot (neu bat OscAndMouse).", this);
    }

    void ScareFishAtPeople(Camera cam)
    {
        if (Time.time < _nextFishScareTime) return;
        _nextFishScareTime = Time.time + fishScareCheckInterval;

        // Khach dung yen = giong giu chuot: ca nao boi vao vung khach thi giat minh.
        // FishClickInteraction tu bo qua ca dang bo chay nen khong bi kich lien tuc.
        for (int i = 0; i < _people.Count; i++)
            FishClickInteraction.TryTriggerAtScreenPosition(cam, _people[i].ScreenPosition);
    }

    void EmitFromOsc(Camera cam)
    {
        // Uu tien khach vao truoc (FirstSeenTime nho) khi vuot maxSimultaneousPeople
        if (_people.Count > maxSimultaneousPeople)
            _people.Sort((a, b) => a.FirstSeenTime.CompareTo(b.FirstSeenTime));

        int count = Mathf.Min(_people.Count, maxSimultaneousPeople);
        for (int i = 0; i < count; i++)
        {
            var person = _people[i];
            bubbleSystem.SetEmitPoint(person.Id, ScreenToWorld(cam, person.ScreenPosition));
            _activeIds.Add(person.Id);
        }
    }

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
        if (bubbleSystem != null)
            bubbleSystem.StopAllEmitters();
    }
}
