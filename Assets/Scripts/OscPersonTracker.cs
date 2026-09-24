using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Theo doi vi tri khach tham quan qua OSC, dung giao thuc giong TouchDesigner person-tracker:
/// /person/count (int hoac float) roi /person/x, /person/y (float, 0..1) cho tung nguoi trong frame.
/// Thu tu x/y co the xen ke (x0 y0 x1 y1) hoac gom (x0 x1 .. y0 y1 ..) - dung 2 con tro rieng.
///
/// Moi khach duoc gan 1 ID ON DINH (khop gan nhat theo UV giua cac frame, giong TDReceiver mau),
/// de script tieu thu giu duoc trang thai rieng tung nguoi (nguon bong bong, cooldown...) thay vi
/// bi doi nguoi khi TD sap xep lai thu tu. ID la duy nhat tren TOAN BO cac tracker trong scene.
///
/// Vi tri duoc quy doi sang screen space (pixel) de dung truc tiep cho cac he thong tuong tac
/// von nhan Vector2 screenPosition tu chuot/cham (TouchOceanManager, FishClickInteraction).
/// Script tieu thu KHONG can gan tay tracker: de mang trong thi tu dung OscPersonTracker.All.
///
/// Chong nhay lidar: (1) diem moi phai ton tai du confirmTime/confirmMinHits moi thanh khach
/// (loc diem ma), (2) khach mat dau duoi signalTimeout van giu vi tri + ID (loc rot frame),
/// (3) vi tri duoc lam muot bang positionSmoothing (loc rung).
/// Gop khach: diem tho / khach dang track gan nhau duoi mergeRadius duoc gop lam 1.
/// HUD + marker debug chi hien khi bat DebugOverlay (phim L), mac dinh TAT.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(MiniOscReceiver))]
public class OscPersonTracker : MonoBehaviour
{
    public enum CornerOrigin { BottomLeft, TopLeft, TopRight, BottomRight }

    /// <summary>1 khach dang duoc track.</summary>
    public readonly struct Person
    {
        public readonly int Id;
        public readonly Vector2 Uv;
        public readonly Vector2 ScreenPosition;
        public readonly float FirstSeenTime;

        public Person(int id, Vector2 uv, Vector2 screenPosition, float firstSeenTime)
        {
            Id = id; Uv = uv; ScreenPosition = screenPosition; FirstSeenTime = firstSeenTime;
        }
    }

    [Tooltip("⭐ CHONG NHAY (mat dau): khach bi lidar mat dau bao lau van duoc GIU O VI TRI CU " +
             "truoc khi bi coi la da roi vung. Lidar hay rot diem 1-5 frame khi nguoi quay nguoi / " +
             "bi che - qua ngan thi hieu ung tat-bat lien tuc. Goi y: 0.3-0.6s.")]
    public float signalTimeout = 0.4f;

    [Tooltip("RectTransform danh dau vung tuong tac (vd. mot rect stretch-full duoi Canvas). " +
             "UV (0..1,0..1) tu OSC duoc quy doi qua 4 goc cua rect nay. Bo trong = quy doi " +
             "truc tiep theo toan man hinh (Screen.width/height).")]
    public RectTransform spaceRect;

    [Tooltip("Goc coi la UV (0,0) cua du lieu OSC. Doi gia tri nay khi cam bien tracking lap " +
             "nguoc/xoay so voi man hinh, thay vi phai sua code.")]
    public CornerOrigin courtOrigin = CornerOrigin.BottomLeft;

    [Header("Tracking")]
    [Tooltip("Ban kinh (theo UV 0..1) de coi diem moi la CUNG 1 khach voi frame truoc. " +
             "Qua nho = khach di nhanh bi coi la nguoi moi (nguon bong bong bi ngat/burst lai). " +
             "Qua lon = 2 khach dung gan nhau bi nhap ID. Goi y: 0.05-0.12.")]
    [Range(0.01f, 0.5f)] public float trackMatchRadius = 0.08f;

    [Tooltip("Bo qua diem (0,0). TouchDesigner thuong gui 0,0 cho slot trong khi so nguoi giam.")]
    public bool ignoreZeroPositions = true;

    [Header("Chong nhay lidar")]
    [Tooltip("⭐ CHONG NHAY (diem ma): khach MOI phai duoc thay lien tuc it nhat bay nhieu giay moi " +
             "duoc tinh la nguoi that (moi co hieu ung/marker). Loc cac diem nhieu chi xuat hien " +
             "1-2 frame (phan xa, tay vung, tuong). 0 = hien ngay. Goi y: 0.1-0.25s.")]
    [Min(0f)] public float confirmTime = 0.15f;

    [Tooltip("So lan (goi OSC) toi thieu phai nhan duoc trong confirmTime moi xac nhan khach moi.")]
    [Min(1)] public int confirmMinHits = 3;

    [Tooltip("⭐ CHONG RUNG: hang so thoi gian (giay) lam muot vi tri (loc mu). Lidar rung vai cm " +
             "moi frame lam bong bong/marker giat. 0 = khong lam muot. Lon qua = tre so voi nguoi " +
             "that. Goi y: 0.05-0.12s.")]
    [Min(0f)] public float positionSmoothing = 0.08f;

    [Header("Gop khach dung qua gan")]
    [Tooltip("⭐ GOP: 2 diem (cung frame) hoac 2 khach dang track cach nhau DUOI ban kinh nay (UV 0..1) " +
             "bi gop thanh 1 khach (giu ID cua nguoi vao truoc, vi tri = trung binh). Lidar hay tach 1 " +
             "nguoi thanh 2 diem (2 chan, than + tay), hoac 2 nguoi dung sat nhau -> 2 nguon hieu " +
             "ung chong len nhau. 0 = tat gop. Nen nho hon trackMatchRadius. Goi y: 0.03-0.06.")]
    [Range(0f, 0.3f)] public float mergeRadius = 0.04f;

    [Header("Debug marker (hien thi vi tri tung khach dang track)")]
    [Tooltip("Prefab danh dau vi tri khach - dung de kiem tra/hieu chinh khi lap dat thuc te. " +
             "Bo trong = khong hien thi marker nao. Marker CHI de xem, khong kich hoat hieu ung nao.")]
    public GameObject debugMarkerPrefab;
    [Tooltip("Camera dung de quy doi screen -> world cho marker. Bo trong = Camera.main.")]
    public Camera debugMarkerCamera;
    [Tooltip("Do sau (world Z) dat marker.")]
    public float debugMarkerDepth = -3f;

    [Header("Object debug trong scene (an/hien bang phim L, mac dinh TAT)")]
    [Tooltip("Cac object CHI de debug (text huong dan, sprite danh dau, khung vung...). Chi bat/tat " +
             "Renderer / UI Graphic / Canvas ben trong, KHONG SetActive GameObject, nen script va " +
             "RectTransform tren do van chay binh thuong khi dang an.")]
    public GameObject[] debugOnlyObjects;
    [Tooltip("An/hien luon Image/Graphic cua spaceRect (khung vung tuong tac) theo phim L.")]
    public bool spaceRectIsDebugVisual = true;

    [Header("Debug HUD (an/hien bang phim L - DebugOverlay, mac dinh TAT)")]
    [Tooltip("Cho phep tracker nay ve bang trang thai OSC (port, so khach, toa do) khi bat debug " +
             "bang phim L. Tat = khong bao gio ve HUD cua tracker nay.")]
    public bool showDebugHud = true;
    [Tooltip("Goc tren-trai cua bang HUD. Doi khi co nhieu tracker de bang khong chong len nhau.")]
    public Vector2 debugHudOffset = new Vector2(10f, 10f);

    // ── Registry toan cuc ────────────────────────────────────────────────────
    static readonly List<OscPersonTracker> s_all = new List<OscPersonTracker>();
    static int s_nextPersonId = 1;

    /// <summary>Moi tracker dang enable trong scene.</summary>
    public static IReadOnlyList<OscPersonTracker> All => s_all;

    /// <summary>
    /// Gom khach tu cac tracker duoc gan tay; neu mang rong/toan null thi lay tu moi tracker
    /// trong scene. Ket qua ghi vao <paramref name="results"/> (duoc Clear truoc).
    /// </summary>
    public static void CollectPeople(OscPersonTracker[] trackers, List<Person> results)
    {
        results.Clear();
        bool anyAssigned = false;
        if (trackers != null)
        {
            foreach (var tracker in trackers)
            {
                if (tracker == null || !tracker.isActiveAndEnabled) continue;
                anyAssigned = true;
                results.AddRange(tracker._people);
            }
        }
        if (anyAssigned) return;

        foreach (var tracker in s_all)
            results.AddRange(tracker._people);
    }

    // ── API tung tracker ─────────────────────────────────────────────────────
    public IReadOnlyList<Person> People => _people;
    public int ActivePersonCount => _people.Count;
    public IReadOnlyList<Vector2> ActiveScreenPositions => _screenPositions; // giu cho code cu

    class Track
    {
        public int id;
        public Vector2 uv;          // vi tri da lam muot - cai ma script tieu thu thay
        public Vector2 targetUv;    // trung binh cac diem tho gop vao track trong frame OSC gan nhat
        Vector2 _frameSum;
        int _frameCount;
        public float lastSeen;
        public float firstSeen;
        public int hits;
        public bool confirmed;
        public GameObject marker;

        public void BeginFrame(Vector2 raw)
        {
            _frameSum = raw;
            _frameCount = 1;
            targetUv = raw;
        }

        public void MergeInFrame(Vector2 raw)
        {
            _frameSum += raw;
            _frameCount++;
            targetUv = _frameSum / _frameCount;
        }
    }

    MiniOscReceiver _receiver;
    readonly List<Track> _tracks = new List<Track>();
    readonly List<Person> _people = new List<Person>();
    readonly List<Vector2> _screenPositions = new List<Vector2>();
    readonly HashSet<int> _claimed = new HashSet<int>();

    const int MaxPeoplePerFrame = 64;
    readonly float[] _bufX = new float[MaxPeoplePerFrame];
    int _xIdx, _yIdx, _expectedCount;
    float _lastCountTime = float.NegativeInfinity;

    readonly Vector3[] _rectCorners = new Vector3[4];
    GUIStyle _hudHeader, _hudBody;

    void Awake()
    {
        _receiver = GetComponent<MiniOscReceiver>();
        DebugOverlay.VisibilityChanged += ApplyDebugObjectVisibility;
        ApplyDebugObjectVisibility(DebugOverlay.Visible);
    }

    void OnDestroy() => DebugOverlay.VisibilityChanged -= ApplyDebugObjectVisibility;

    void ApplyDebugObjectVisibility(bool visible)
    {
        if (debugOnlyObjects != null)
            foreach (var go in debugOnlyObjects)
                if (go != null) SetVisualsEnabled(go, visible);

        if (spaceRectIsDebugVisual && spaceRect != null)
            SetVisualsEnabled(spaceRect.gameObject, visible);
    }

    static void SetVisualsEnabled(GameObject root, bool visible)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        foreach (var g in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.enabled = visible;
        // Canvas: chi tat canvas CON (khong tat root canvas cua ca UI that neu root duoc gan nham)
        foreach (var c in root.GetComponentsInChildren<Canvas>(true))
            if (!c.isRootCanvas || c.gameObject == root) c.enabled = visible;
    }

    // Doi so an toan ngay trong tracker (khong phu thuoc phien ban MiniOscReceiver):
    // TD hay gui count dang float / toa do dang int -> (int)args[0] se nem InvalidCastException.
    static bool TryGetFloat(object[] args, int index, out float value)
    {
        value = 0f;
        if (args == null || index < 0 || index >= args.Length) return false;
        switch (args[index])
        {
            case float f: value = f; return true;
            case int i: value = i; return true;
            case double d: value = (float)d; return true;
            case long l: value = l; return true;
            case bool bo: value = bo ? 1f : 0f; return true;
            case string str:
                return float.TryParse(str, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value);
            default: return false;
        }
    }

    static bool TryGetInt(object[] args, int index, out int value)
    {
        value = 0;
        if (!TryGetFloat(args, index, out float f)) return false;
        value = Mathf.RoundToInt(f);
        return true;
    }

    void OnEnable()
    {
        _receiver.OnOscMessage += OnMessage;
        if (!s_all.Contains(this)) s_all.Add(this);
    }

    void OnDisable()
    {
        _receiver.OnOscMessage -= OnMessage;
        s_all.Remove(this);
        foreach (var track in _tracks)
            if (track.marker != null) Destroy(track.marker);
        _tracks.Clear();
        _people.Clear();
        _screenPositions.Clear();
    }

    void Update()
    {
        // Nguon gui KHONG co /person/count: coi moi frame Unity la 1 frame du lieu moi,
        // neu khong 2 con tro x/y tang mai va ngung nhan sau MaxPeoplePerFrame diem.
        if (Time.time - _lastCountTime > 1f)
        {
            _xIdx = _yIdx = 0;
            _claimed.Clear();
        }

        float now = Time.time;

        for (int i = _tracks.Count - 1; i >= 0; i--)
        {
            Track track = _tracks[i];
            float sinceSeen = now - track.lastSeen;

            // Diem ma chua kip xac nhan ma da mat -> bo ngay, khong cho grace
            bool expired = track.confirmed
                ? sinceSeen > signalTimeout
                : sinceSeen > Mathf.Max(confirmTime, 0.1f);
            if (expired)
            {
                RemoveTrackAt(i);
                continue;
            }

            if (!track.confirmed && now - track.firstSeen >= confirmTime && track.hits >= confirmMinHits)
                track.confirmed = true;

            // Lam muot: chi tien ve targetUv khi con dang thay (trong grace thi dung yen tai cho cu)
            track.uv = positionSmoothing > 0f
                ? Vector2.Lerp(track.uv, track.targetUv, 1f - Mathf.Exp(-Time.deltaTime / positionSmoothing))
                : track.targetUv;
        }

        MergeCloseTracks();

        // Quy doi UV -> screen MOI FRAME (khong chi luc nhan goi) de doi do phan giai /
        // di chuyen spaceRect van dung vi tri ngay. Chi khach DA XAC NHAN moi lo ra ngoai.
        GetScreenCorners(out Vector2 bl, out Vector2 tl, out Vector2 tr, out Vector2 br);
        _people.Clear();
        _screenPositions.Clear();
        foreach (var track in _tracks)
        {
            if (!track.confirmed) continue;
            Vector2 screen = UvToScreen(track.uv, bl, tl, tr, br);
            _people.Add(new Person(track.id, track.uv, screen, track.firstSeen));
            _screenPositions.Add(screen);
        }

        SyncDebugMarkers();
    }

    /// <summary>
    /// 2 khach dang track troi vao sat nhau (&lt; mergeRadius) -> gop lam 1, giu khach vao truoc
    /// (ID cu, uu tien khach da xac nhan) de hieu ung dang chay cua ho khong bi ngat.
    /// </summary>
    void MergeCloseTracks()
    {
        if (mergeRadius <= 0f) return;
        float r2 = mergeRadius * mergeRadius;

        for (int i = _tracks.Count - 1; i >= 1; i--)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                Track a = _tracks[i], b = _tracks[j];
                if ((a.uv - b.uv).sqrMagnitude > r2) continue;

                bool keepB = b.confirmed != a.confirmed ? b.confirmed : b.firstSeen <= a.firstSeen;
                Track keep = keepB ? b : a;
                Track drop = keepB ? a : b;
                keep.targetUv = (keep.targetUv + drop.targetUv) * 0.5f;
                keep.lastSeen = Mathf.Max(keep.lastSeen, drop.lastSeen);
                keep.hits += drop.hits;

                RemoveTrackAt(keepB ? i : j);
                if (!keepB) i--; // j < i da bi xoa -> chi so cua a lui 1
                break;
            }
        }
    }

    void RemoveTrackAt(int index)
    {
        Track track = _tracks[index];
        if (track.marker != null) Destroy(track.marker);
        _claimed.Remove(track.id);
        _tracks.RemoveAt(index);
    }

    // ── OSC ──────────────────────────────────────────────────────────────────

    void OnMessage(string address, object[] args)
    {
        switch (address)
        {
            case "/person/count":
                // Bat dau 1 frame du lieu moi tu TD
                TryGetInt(args, 0, out _expectedCount);
                _lastCountTime = Time.time;
                _xIdx = _yIdx = 0;
                _claimed.Clear();
                break;

            case "/person/x":
                // Ho tro ca 1 gia tri/goi lan nhieu gia tri/goi (mang x0 x1 x2...)
                for (int a = 0; a < args.Length; a++)
                {
                    if (!TryGetFloat(args, a, out float x)) continue;
                    if (_xIdx < _bufX.Length) _bufX[_xIdx] = x;
                    _xIdx++;
                }
                break;

            case "/person/y":
                for (int a = 0; a < args.Length; a++)
                {
                    if (!TryGetFloat(args, a, out float y)) continue;
                    int i = _yIdx++;
                    if (i >= _bufX.Length || i >= _xIdx) continue; // chua co x tuong ung
                    if (_expectedCount > 0 && i >= _expectedCount) continue;

                    var uv = new Vector2(_bufX[i], y);
                    if (ignoreZeroPositions && uv.x == 0f && uv.y == 0f) continue;
                    MatchOrCreateTrack(uv);
                }
                break;
        }
    }

    void MatchOrCreateTrack(Vector2 uv)
    {
        // (1) Diem tho nam sat 1 khach DA nhan diem trong frame OSC nay -> cung 1 nguoi bi lidar
        //     tach doi (2 chan, than + tay) hoac 2 nguoi dung sat nhau: gop, khong tao khach moi.
        if (mergeRadius > 0f)
        {
            float mergeD2 = mergeRadius * mergeRadius;
            foreach (var track in _tracks)
            {
                if (!_claimed.Contains(track.id)) continue;
                if ((track.targetUv - uv).sqrMagnitude > mergeD2) continue;
                track.MergeInFrame(uv);
                return;
            }
        }

        // (2) Khop voi khach gan nhat chua nhan diem trong frame nay (ke ca khach dang trong
        //     grace signalTimeout -> lidar mat dau roi thay lai van giu dung ID, khong nhay).
        Track best = null;
        float bestD2 = trackMatchRadius * trackMatchRadius;
        foreach (var track in _tracks)
        {
            if (_claimed.Contains(track.id)) continue; // moi track chi nhan 1 diem / frame
            float d2 = (track.uv - uv).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = track; }
        }

        // (3) Khach moi - chua lo ra ngoai cho toi khi du confirmTime/confirmMinHits
        if (best == null)
        {
            best = new Track { id = s_nextPersonId++, firstSeen = Time.time, uv = uv };
            _tracks.Add(best);
        }

        best.BeginFrame(uv);
        best.lastSeen = Time.time;
        best.hits++;
        _claimed.Add(best.id);
    }

    // ── Quy doi toa do ───────────────────────────────────────────────────────

    public Vector2 UvToScreen(Vector2 uv)
    {
        GetScreenCorners(out Vector2 bl, out Vector2 tl, out Vector2 tr, out Vector2 br);
        return UvToScreen(uv, bl, tl, tr, br);
    }

    Vector2 UvToScreen(Vector2 uv, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br)
    {
        RemapByCourtOrigin(bl, tl, tr, br, out Vector2 origin, out Vector2 uEnd, out Vector2 vEnd, out Vector2 diagonal);
        Vector2 uEdge = Vector2.Lerp(origin, uEnd, uv.x);
        Vector2 vEdge = Vector2.Lerp(vEnd, diagonal, uv.x);
        return Vector2.Lerp(uEdge, vEdge, uv.y);
    }

    void GetScreenCorners(out Vector2 bl, out Vector2 tl, out Vector2 tr, out Vector2 br)
    {
        if (spaceRect == null)
        {
            bl = new Vector2(0f, 0f);
            tl = new Vector2(0f, Screen.height);
            tr = new Vector2(Screen.width, Screen.height);
            br = new Vector2(Screen.width, 0f);
            return;
        }

        // GetWorldCorners: [0]=BL, [1]=TL, [2]=TR, [3]=BR
        spaceRect.GetWorldCorners(_rectCorners);
        Camera cam = GetCanvasCamera();
        bl = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[0]);
        tl = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[1]);
        tr = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[2]);
        br = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[3]);
    }

    Camera GetCanvasCamera()
    {
        // Canvas Overlay: bat buoc truyen null (ke ca khi worldCamera co gan) - neu khong
        // WorldToScreenPoint se chieu sai. Canvas Camera/World: dung camera cua root canvas.
        Canvas canvas = spaceRect.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    // origin = goc UV(0,0), uEnd = huong tang U, vEnd = huong tang V, diagonal = goc doi dien origin
    void RemapByCourtOrigin(Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br,
        out Vector2 origin, out Vector2 uEnd, out Vector2 vEnd, out Vector2 diagonal)
    {
        switch (courtOrigin)
        {
            case CornerOrigin.BottomLeft: origin = bl; uEnd = br; diagonal = tr; vEnd = tl; break;
            case CornerOrigin.TopLeft: origin = tl; uEnd = tr; diagonal = br; vEnd = bl; break;
            case CornerOrigin.TopRight: origin = tr; uEnd = tl; diagonal = bl; vEnd = br; break;
            default: origin = br; uEnd = bl; diagonal = tl; vEnd = tr; break; // BottomRight
        }
    }

    // ── Debug marker ─────────────────────────────────────────────────────────

    void SyncDebugMarkers()
    {
        Camera cam = debugMarkerCamera != null ? debugMarkerCamera : Camera.main;

        bool show = DebugOverlay.Visible && debugMarkerPrefab != null && cam != null;
        int personIndex = 0;

        for (int i = 0; i < _tracks.Count; i++)
        {
            Track track = _tracks[i];
            if (!track.confirmed) continue;
            Vector2 sp = _people[personIndex++].ScreenPosition;

            if (!show)
            {
                if (track.marker != null && track.marker.activeSelf) track.marker.SetActive(false);
                continue;
            }

            // Marker gan theo ID nen di theo DUNG nguoi do; khong parent vao tracker de
            // khong bi an theo scale/rotation cua GameObject nay.
            if (track.marker == null)
            {
                track.marker = Instantiate(debugMarkerPrefab);
                track.marker.name = $"{name}_Person_{track.id}";
            }
            if (!track.marker.activeSelf) track.marker.SetActive(true);

            float distanceFromCamera = debugMarkerDepth - cam.transform.position.z;
            track.marker.transform.position = cam.ScreenToWorldPoint(
                new Vector3(sp.x, sp.y, distanceFromCamera));
        }
    }

    // ── HUD ──────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!showDebugHud || !DebugOverlay.Visible) return;

        _hudHeader ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _hudBody ??= new GUIStyle(GUI.skin.label) { fontSize = 12 };
        var h = _hudHeader;
        var b = _hudBody;
        float x = debugHudOffset.x, y = debugHudOffset.y;

        h.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 400, 22), $"-- OscPersonTracker ({name}) --", h);
        y += 22;

        bool bound = _receiver != null && _receiver.PortBound;
        float sinceLast = _receiver != null ? _receiver.SecondsSinceLastMessage : -1f;
        bool receiving = sinceLast >= 0f && sinceLast < 1f;

        b.normal.textColor = bound ? Color.green : Color.red;
        GUI.Label(new Rect(x, y, 400, 20),
            $"Port {(_receiver != null ? _receiver.port : 0)}: {(bound ? "bound" : "FAILED")}", b);
        y += 18;

        b.normal.textColor = receiving ? Color.green : Color.yellow;
        string lastMsg = sinceLast >= 0f ? $"{sinceLast:F1}s truoc" : "chua co";
        GUI.Label(new Rect(x, y, 400, 20), $"Nhan du lieu: {(receiving ? "CO" : "khong")} (lan cuoi: {lastMsg})", b);
        y += 18;

        y += 6;

        h.normal.textColor = Color.cyan;
        GUI.Label(new Rect(x, y, 400, 22), $"-- Khach dang track: {_people.Count} --", h);
        y += 22;

        b.normal.textColor = Color.white;
        foreach (var person in _people)
        {
            GUI.Label(new Rect(x, y, 500, 18),
                $"  id={person.Id} uv=({person.Uv.x:F3},{person.Uv.y:F3}) screen=({person.ScreenPosition.x:F0},{person.ScreenPosition.y:F0})", b);
            y += 16;
        }
        y += 8;

        b.normal.textColor = Color.gray;
        int pending = _tracks.Count - _people.Count;
        if (pending > 0)
        {
            GUI.Label(new Rect(x, y, 400, 18), $"  (+{pending} diem dang cho xac nhan)", b);
            y += 18;
        }
        GUI.Label(new Rect(x, y, 400, 18), "Bam L de an/hien debug", b);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (spaceRect == null) return;

        spaceRect.GetWorldCorners(_rectCorners);
        Vector3 bl = _rectCorners[0], tl = _rectCorners[1], tr = _rectCorners[2], br = _rectCorners[3];

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bl, tl);
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);

        Vector3 origin = courtOrigin switch
        {
            CornerOrigin.BottomLeft => bl,
            CornerOrigin.TopLeft => tl,
            CornerOrigin.TopRight => tr,
            _ => br
        };
        float markerRadius = Vector3.Distance(bl, br) * 0.02f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, markerRadius);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(origin + Vector3.up * markerRadius * 2f, $"OSC (0,0) [{courtOrigin}]");

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            foreach (var track in _tracks)
            {
                if (track.marker == null || !track.marker.activeSelf) continue;
                Gizmos.DrawWireSphere(track.marker.transform.position, markerRadius);
                UnityEditor.Handles.Label(track.marker.transform.position + Vector3.up * markerRadius, $"id:{track.id}");
            }
        }
    }
#endif
}

/// <summary>
/// Cong tac debug TOAN CUC: bam L de an/hien moi hien thi debug (HUD OSC, marker vi tri khach,
/// debugOnlyObjects cua OscPersonTracker...). MAC DINH TAT moi lan vao Play / chay build, de
/// exhibit that khong bao gio lo chu/marker debug khi vua bat may.
/// Tu khoi tao (RuntimeInitializeOnLoadMethod) - khong can dat vao scene.
/// Script khac: doc DebugOverlay.Visible, hoac dang ky DebugOverlay.VisibilityChanged.
/// </summary>
public static class DebugOverlay
{
    public static bool Visible { get; private set; }

    /// <summary>Ban ra moi khi bat/tat (tham so = trang thai moi).</summary>
    public static event Action<bool> VisibilityChanged;

    public static void SetVisible(bool visible)
    {
        if (Visible == visible) return;
        Visible = visible;
        Debug.Log($"[DebugOverlay] Hien thi debug: {(visible ? "BAT" : "TAT")} (phim L)");
        VisibilityChanged?.Invoke(visible);
    }

    public static void Toggle() => SetVisible(!Visible);

    // Reset ca khi tat Domain Reload (Enter Play Mode Options) - static khong tu ve mac dinh
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Visible = false;
        VisibilityChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateKeyListener()
    {
        var go = new GameObject("[DebugOverlay]") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<DebugOverlayKeyListener>();
    }

    internal static bool WasToggleKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.L);
#else
        return false;
#endif
    }
}

/// <summary>Chi de nghe phim L moi frame cho DebugOverlay (tao tu dong, an khoi Hierarchy).</summary>
sealed class DebugOverlayKeyListener : MonoBehaviour
{
    void Update()
    {
        if (DebugOverlay.WasToggleKeyPressed())
            DebugOverlay.Toggle();
    }
}
