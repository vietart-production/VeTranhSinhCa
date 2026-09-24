using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Theo doi vi tri khach tham quan qua OSC, dung giao thuc giong TouchDesigner person-tracker:
/// /person/count (int) roi lan luot /person/x, /person/y (float, 0..1) cho tung nguoi trong frame.
/// Quy doi UV sang toa do man hinh (screen space) de dung truc tiep cho cac he thong tuong tac
/// hien co (TouchOceanManager, FishClickInteraction) - vi cac he thong nay von da nhan
/// Vector2 screenPosition tu chuot/cham, nen khong can quy dinh mot khong gian the gioi rieng.
///
/// Moi diem phat hien duoc gop nhom thanh "track" on dinh qua cac frame (khop theo khoang cach
/// gan nhat) thay vi dung thang thu tu tin OSC lam danh tinh - giup chong nhay khi lidar mat dau
/// tam thoi hoac tach nham 1 nguoi thanh nhieu diem.
/// </summary>
[RequireComponent(typeof(MiniOscReceiver))]
public class OscPersonTracker : MonoBehaviour
{
    public enum CornerOrigin { BottomLeft, TopLeft, TopRight, BottomRight }

    class Track
    {
        public int id;
        public Vector2 screenPos;
        public float lastSeen;
        public bool claimedThisBatch;
    }

    [Header("Chong nhay / gop nguoi")]
    [Tooltip("Sau bao lau khong nhan duoc du lieu moi thi coi track da roi vung theo doi. " +
             "Day la 'khoang tho' chong nhay: lidar mat dau 1-2 frame van giu nguyen track " +
             "thay vi xoa roi tao lai (gay giat/nhay o phia tieu thu).")]
    public float signalTimeout = 0.3f;

    [Tooltip("Khoang cach toi da (px man hinh) de mot diem phat hien moi duoc coi la CUNG track " +
             "voi mot track dang co, thay vi tao track moi. Tang len neu lidar hay lam vi tri " +
             "nhay qua nhieu giua cac frame khien track bi coi la nguoi moi lien tuc.")]
    public float trackMatchRadius = 150f;

    [Tooltip("Khoang cach toi da (px man hinh) de gop 2 diem phat hien trong CUNG 1 frame thanh " +
             "1 nguoi duy nhat - xu ly truong hop lidar tach nham 1 nguoi thanh 2 diem gan nhau.")]
    public float mergePersonDistance = 80f;

    [Header("Khong gian tuong tac")]
    [Tooltip("RectTransform danh dau vung tuong tac (vd. mot rect stretch-full duoi Canvas). " +
             "UV (0..1,0..1) tu OSC duoc quy doi qua 4 goc cua rect nay. Bo trong = quy doi " +
             "truc tiep theo toan man hinh (Screen.width/height).")]
    public RectTransform spaceRect;

    [Tooltip("Goc coi la UV (0,0) cua du lieu OSC. Doi gia tri nay khi cam bien tracking lap " +
             "nguoc/xoay so voi man hinh, thay vi phai sua code.")]
    public CornerOrigin courtOrigin = CornerOrigin.BottomLeft;

    [Header("Debug marker (hien thi vi tri tung khach dang track)")]
    [Tooltip("Prefab danh dau vi tri khach - dung de kiem tra/hieu chinh khi lap dat thuc te. " +
             "Bo trong = khong hien thi marker nao.")]
    public GameObject debugMarkerPrefab;
    [Tooltip("Camera dung de quy doi screen -> world cho marker. Bo trong = Camera.main.")]
    public Camera debugMarkerCamera;
    [Tooltip("Do sau (world Z) dat marker.")]
    public float debugMarkerDepth = -3f;

    [Header("Debug HUD / Gizmos")]
    [Tooltip("Bat/tat toan bo debug (OnGUI HUD + Gizmos + marker 3D). Bam phim L de doi nhanh " +
             "luc Play - tat het truoc khi chay show that.")]
    public bool showDebugVisuals = true;

    // Doc tu _positionsCache (khong phai _tracks.Count truc tiep) de luon dong bo tuyet doi voi
    // ActiveScreenPositions/ActiveTracks - tranh IndexOutOfRange khi component khac doc gia tri
    // nay TRUOC luc OscPersonTracker.Update() kip chay lai trong cung 1 frame (thu tu Update()
    // giua cac component la tuy y trong Unity, khong dam bao truoc).
    public int ActivePersonCount => _positionsCache.Count;
    public IReadOnlyList<Vector2> ActiveScreenPositions => _positionsCache;
    /// <summary>Vi tri kem id on dinh cua tung track - dung cho he thong can 1 nguon phat
    /// (vd. bong bong) rieng cho tung khach thay vi gop chung ve 1 diem.</summary>
    public IReadOnlyList<(int id, Vector2 screenPos)> ActiveTracks => _tracksCache;

    MiniOscReceiver _receiver;
    readonly List<Track> _tracks = new List<Track>();
    readonly List<Vector2> _positionsCache = new List<Vector2>();
    readonly List<(int id, Vector2 screenPos)> _tracksCache = new List<(int, Vector2)>();
    readonly List<GameObject> _markers = new List<GameObject>();
    int _nextTrackId;

    // Ho tro toi ~100 khach cung luc (co du du phong) - protocol OSC gui /person/count
    // roi lan luot x/y cho tung nguoi, can bien du lon de khong bi cat bot khi dong.
    const int MaxTrackedPersons = 128;
    readonly float[] _bufX = new float[MaxTrackedPersons];
    readonly Vector2[] _batchBuffer = new Vector2[MaxTrackedPersons];
    readonly Vector3[] _rectCorners = new Vector3[4];
    int _idx;
    int _expectedCount;

    readonly Queue<string> _recentData = new Queue<string>(6);
    readonly Queue<string> _logs = new Queue<string>(8);

    void Awake() => _receiver = GetComponent<MiniOscReceiver>();

    void OnEnable()
    {
        _receiver.OnOscMessage += OnMessage;
        Application.logMessageReceived += OnLog;
    }

    void OnDisable()
    {
        _receiver.OnOscMessage -= OnMessage;
        Application.logMessageReceived -= OnLog;
        foreach (var marker in _markers)
            if (marker != null) Destroy(marker);
        _markers.Clear();
    }

    void Update()
    {
        if (WasDebugToggleKeyPressed())
            showDebugVisuals = !showDebugVisuals;

        // Bo cac track qua signalTimeout khong co du lieu moi - "khoang tho" chong nhay khi
        // lidar mat dau nguoi 1-2 frame roi thay lai, thay vi xoa/tao lai track ngay lap tuc.
        for (int i = _tracks.Count - 1; i >= 0; i--)
            if (Time.time - _tracks[i].lastSeen > signalTimeout)
                _tracks.RemoveAt(i);

        _positionsCache.Clear();
        _tracksCache.Clear();
        for (int i = 0; i < _tracks.Count; i++)
        {
            _positionsCache.Add(_tracks[i].screenPos);
            _tracksCache.Add((_tracks[i].id, _tracks[i].screenPos));
        }

        SyncDebugMarkers();
    }

    static bool WasDebugToggleKeyPressed()
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

    void SyncDebugMarkers()
    {
        if (!showDebugVisuals || debugMarkerPrefab == null)
        {
            foreach (var marker in _markers)
                if (marker != null) marker.SetActive(false);
            return;
        }

        Camera cam = debugMarkerCamera != null ? debugMarkerCamera : Camera.main;
        if (cam == null) return;

        while (_markers.Count < _tracks.Count)
            _markers.Add(Instantiate(debugMarkerPrefab, transform));

        for (int i = 0; i < _markers.Count; i++)
        {
            bool active = i < _tracks.Count;
            _markers[i].SetActive(active);
            if (!active) continue;

            Vector2 sp = _tracks[i].screenPos;
            float distanceFromCamera = debugMarkerDepth - cam.transform.position.z;
            _markers[i].transform.position = cam.ScreenToWorldPoint(
                new Vector3(sp.x, sp.y, distanceFromCamera));
        }
    }

    void OnMessage(string address, object[] args)
    {
        switch (address)
        {
            case "/person/count":
                _expectedCount = Mathf.Clamp((int)args[0], 0, _batchBuffer.Length);
                _idx = 0;
                foreach (var t in _tracks) t.claimedThisBatch = false;
                break;
            case "/person/x":
                if (_idx < _bufX.Length) _bufX[_idx] = (float)args[0];
                break;
            case "/person/y":
                if (_idx < _batchBuffer.Length)
                {
                    Vector2 uv = new Vector2(_bufX[_idx], (float)args[0]);
                    Vector2 screenPos = UvToScreen(uv);
                    _batchBuffer[_idx] = screenPos;
                    LogRecent(_idx, uv, screenPos);
                }
                _idx++;
                if (_idx >= _expectedCount) ProcessBatch(_expectedCount);
                break;
        }
    }

    // Gop cac diem qua gan trong CUNG 1 frame (1 nguoi bi lidar tach nham thanh nhieu diem),
    // roi khop tung diem da gop voi track gan nhat dang co de giu danh tinh on dinh qua cac frame.
    void ProcessBatch(int n)
    {
        // ponytail: gop theo kieu greedy (moi diem gop voi tat ca diem con lai trong ban kinh),
        // khong phai clustering chuan - du dung neu moi nguoi chi tach thanh toi da vai diem gan
        // nhau; nang cap len union-find/k-means neu lidar nhieu qua muc nay.
        var used = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (used[i]) continue;
            used[i] = true;
            Vector2 sum = _batchBuffer[i];
            int count = 1;
            for (int j = i + 1; j < n; j++)
            {
                if (used[j]) continue;
                if (Vector2.Distance(_batchBuffer[i], _batchBuffer[j]) <= mergePersonDistance)
                {
                    sum += _batchBuffer[j];
                    count++;
                    used[j] = true;
                }
            }
            MatchOrCreateTrack(sum / count);
        }
    }

    void MatchOrCreateTrack(Vector2 pos)
    {
        Track best = null;
        float bestDist = trackMatchRadius;
        foreach (var t in _tracks)
        {
            if (t.claimedThisBatch) continue;
            float d = Vector2.Distance(t.screenPos, pos);
            if (d <= bestDist) { bestDist = d; best = t; }
        }

        if (best == null)
        {
            best = new Track { id = _nextTrackId++ };
            _tracks.Add(best);
        }

        best.screenPos = pos;
        best.lastSeen = Time.time;
        best.claimedThisBatch = true;
    }

    void LogRecent(int index, Vector2 uv, Vector2 screenPos)
    {
        string entry = $"[{Time.time:F1}s] p[{index}] uv=({uv.x:F3},{uv.y:F3}) screen=({screenPos.x:F0},{screenPos.y:F0})";
        if (_recentData.Count >= 6) _recentData.Dequeue();
        _recentData.Enqueue(entry);
    }

    void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        string prefix = type == LogType.Exception ? "<EXC> " : "<ERR> ";
        string line = $"[{Time.time:F1}s] {prefix}{condition}";
        if (_logs.Count >= 8) _logs.Dequeue();
        _logs.Enqueue(line);
    }

    Vector2 UvToScreen(Vector2 uv)
    {
        GetScreenCorners(out Vector2 bl, out Vector2 tl, out Vector2 tr, out Vector2 br);
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
        Camera cam = spaceRect.GetComponentInParent<Canvas>()?.worldCamera;
        bl = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[0]);
        tl = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[1]);
        tr = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[2]);
        br = RectTransformUtility.WorldToScreenPoint(cam, _rectCorners[3]);
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

    void OnGUI()
    {
        if (!showDebugVisuals) return;

        var h = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        var b = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        float x = 10f, y = 10f;

        h.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 400, 22), "-- OscPersonTracker --", h);
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
        y += 24;

        h.normal.textColor = Color.red;
        GUI.Label(new Rect(x, y, 500, 22), $"-- Vung track (origin={courtOrigin}) --", h);
        y += 22;
        b.normal.textColor = Color.white;
        if (spaceRect != null)
        {
            GetScreenCorners(out var bl, out var tl, out var tr, out var br);
            GUI.Label(new Rect(x, y, 500, 18), $"BL=({bl.x:F0},{bl.y:F0})  TL=({tl.x:F0},{tl.y:F0})", b); y += 16;
            GUI.Label(new Rect(x, y, 500, 18), $"BR=({br.x:F0},{br.y:F0})  TR=({tr.x:F0},{tr.y:F0})", b); y += 16;
        }
        else
        {
            b.normal.textColor = Color.gray;
            GUI.Label(new Rect(x, y, 500, 18), $"Chua gan spaceRect - dung toan man hinh {Screen.width}x{Screen.height}", b); y += 16;
        }
        y += 8;

        h.normal.textColor = Color.cyan;
        GUI.Label(new Rect(x, y, 400, 22), $"-- Khach dang track: {ActivePersonCount} --", h);
        y += 22;
        b.normal.textColor = Color.white;
        for (int i = 0; i < _tracks.Count; i++)
        {
            var t = _tracks[i];
            GUI.Label(new Rect(x, y, 600, 18),
                $"  id={t.id} screen=({t.screenPos.x:F0},{t.screenPos.y:F0}) lastSeen={Time.time - t.lastSeen:F2}s truoc", b);
            y += 16;
        }
        y += 8;

        h.normal.textColor = Color.gray;
        GUI.Label(new Rect(x, y, 400, 20), "-- Du lieu gan day --", h);
        y += 20;
        b.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        foreach (var entry in _recentData) { GUI.Label(new Rect(x, y, 700, 18), entry, b); y += 16; }
        y += 8;

        h.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 400, 20), "-- Log loi --", h);
        y += 20;
        foreach (var entry in _logs)
        {
            b.normal.textColor = entry.Contains("<EXC>") ? Color.red : Color.yellow;
            GUI.Label(new Rect(x, y, 700, 18), entry, b);
            y += 16;
        }
        y += 8;

        b.normal.textColor = Color.gray;
        GUI.Label(new Rect(x, y, 500, 18), "Bam L de an/hien toan bo debug (HUD + gizmos + marker)", b);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugVisuals || spaceRect == null) return;

        spaceRect.GetWorldCorners(_rectCorners);
        Vector3 bl = _rectCorners[0], tl = _rectCorners[1], tr = _rectCorners[2], br = _rectCorners[3];

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bl, tl);
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);

        float markerRadius = Vector3.Distance(bl, br) * 0.02f;
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(bl, "BL");
        UnityEditor.Handles.Label(tl, "TL");
        UnityEditor.Handles.Label(tr, "TR");
        UnityEditor.Handles.Label(br, "BR");

        Vector3 origin = courtOrigin switch
        {
            CornerOrigin.BottomLeft => bl,
            CornerOrigin.TopLeft => tl,
            CornerOrigin.TopRight => tr,
            _ => br
        };
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, markerRadius);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(origin + Vector3.up * markerRadius * 2f, $"OSC (0,0) [{courtOrigin}]");

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            UnityEditor.Handles.color = Color.cyan;
            for (int i = 0; i < _markers.Count && i < _tracks.Count; i++)
            {
                if (_markers[i] == null || !_markers[i].activeSelf) continue;
                Vector3 pos = _markers[i].transform.position;
                Gizmos.DrawWireSphere(pos, markerRadius);
                UnityEditor.Handles.Label(pos + Vector3.up * markerRadius, $"id:{_tracks[i].id}");
            }
        }
    }
#endif
}
