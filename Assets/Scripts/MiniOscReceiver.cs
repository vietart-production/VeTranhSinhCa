using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Receiver OSC/UDP toi gian, tu viet (khong dung plugin ngoai nhu OSCMaster cua Augmenta):
/// doc dia chi + tham so i/f/s/d/h/T/F/N/I (bo qua b/t), co ho tro OSC bundle (#bundle) long nhau.
/// Nhan tren thread nen, day ve main thread trong Update() roi moi ban event OnOscMessage.
///
/// DefaultExecutionOrder(-200): chay TRUOC OscPersonTracker (-100) va moi script tieu thu
/// (TouchOceanManager, BackgroundFishSpawner) de du lieu khach trong frame nay dung ngay
/// trong chinh frame nay, khong tre 1 frame.
/// </summary>
[DefaultExecutionOrder(-200)]
public class MiniOscReceiver : MonoBehaviour
{
    [Tooltip("Cong UDP lang nghe OSC (phai trung voi cong OSC Out ben TouchDesigner).")]
    public int port = 12000;

    [Tooltip("Bat Application.runInBackground luc OnEnable. Exhibit that phai bat: neu cua so " +
             "Unity mat focus ma khong chay nen thi Update() dung, goi tin OSC don lai va toan bo " +
             "tuong tac bi dung.")]
    public bool forceRunInBackground = true;

    [Tooltip("Tran so goi tin cho xu ly. Neu main thread bi khung (load scene, GC...) thi bo goi " +
             "cu nhat thay vi de hang doi phinh vo han roi phat lai du lieu cu.")]
    [Min(64)] public int maxQueuedMessages = 4096;

    public event Action<string, object[]> OnOscMessage;

    public bool PortBound { get; private set; }
    public float SecondsSinceLastMessage => _lastMessageTime < 0f ? -1f : Time.time - _lastMessageTime;
    public int DroppedMessageCount => _dropped;

    UdpClient _client;
    Thread _thread;
    volatile bool _running;
    volatile int _dropped;
    readonly ConcurrentQueue<(string address, object[] args)> _queue = new();
    float _lastMessageTime = -1f;

    void OnEnable()
    {
        if (forceRunInBackground)
            Application.runInBackground = true;

        try
        {
            _client = new UdpClient(port);
            _client.Client.ReceiveBufferSize = 1 << 18; // 256 KB, tranh mat goi khi TD ban day
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = $"MiniOscReceiver:{port}" };
            _thread.Start();
            PortBound = true;
            Debug.Log($"[MiniOscReceiver] Dang lang nghe OSC tren port {port}", this);
        }
        catch (Exception e)
        {
            PortBound = false;
            Debug.LogError($"[MiniOscReceiver] Khong the mo port {port} (da bi app khac chiem?): {e.Message}", this);
        }
    }

    void OnDisable()
    {
        _running = false;
        PortBound = false;
        _client?.Close(); // ngat Receive() dang block trong thread nen
        _thread?.Join(200);
        _client = null;
        _thread = null;
        while (_queue.TryDequeue(out _)) { }
    }

    void ReceiveLoop()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        var parsed = new List<(string, object[])>(8);

        while (_running)
        {
            byte[] data;
            try
            {
                data = _client.Receive(ref remote);
            }
            catch (ObjectDisposedException)
            {
                break; // socket bi dong trong OnDisable
            }
            catch (SocketException)
            {
                // Windows co the nem ConnectionReset (ICMP port unreachable) cho socket UDP:
                // khong phai loi chet, tiep tuc nghe thay vi thoat thread vinh vien.
                if (!_running) break;
                continue;
            }

            parsed.Clear();
            TryParsePacket(data, 0, data.Length, parsed);
            foreach (var msg in parsed)
            {
                _queue.Enqueue(msg);
                while (_queue.Count > maxQueuedMessages && _queue.TryDequeue(out _))
                    _dropped++;
            }
        }
    }

    void Update()
    {
        // Chuyen du lieu tu thread nen sang main thread truoc khi ban ra ngoai
        while (_queue.TryDequeue(out var msg))
        {
            _lastMessageTime = Time.time;
            try
            {
                OnOscMessage?.Invoke(msg.address, msg.args);
            }
            catch (Exception e)
            {
                // 1 handler loi khong duoc lam ket ca hang doi (cac goi sau van phai duoc xu ly)
                Debug.LogException(e, this);
            }
        }
    }

    // ── Chuyen kieu an toan ──────────────────────────────────────────────────
    // TouchDesigner hay gui so dem (count) duoi dang float, hoac toa do 0/1 duoi dang int.
    // Ep kieu truc tiep (int)args[0] / (float)args[0] se nem InvalidCastException.

    public static bool TryGetFloat(object[] args, int index, out float value)
    {
        value = 0f;
        if (args == null || index < 0 || index >= args.Length) return false;
        switch (args[index])
        {
            case float f: value = f; return true;
            case int i: value = i; return true;
            case double d: value = (float)d; return true;
            case long l: value = l; return true;
            case bool b: value = b ? 1f : 0f; return true;
            case string s:
                return float.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value);
            default: return false;
        }
    }

    public static bool TryGetInt(object[] args, int index, out int value)
    {
        value = 0;
        if (!TryGetFloat(args, index, out float f)) return false;
        value = Mathf.RoundToInt(f);
        return true;
    }

    // ── Parser ───────────────────────────────────────────────────────────────

    /// <summary>Parse 1 goi UDP: co the la message don hoac bundle (long nhau).</summary>
    internal static void TryParsePacket(byte[] data, int start, int end, List<(string, object[])> results)
    {
        if (end - start < 4) return;

        if (data[start] == (byte)'#')
        {
            int offset = start;
            if (!TryReadOscString(data, ref offset, end, out string tag) || tag != "#bundle") return;
            offset += 8; // bo qua OSC timetag - xu ly ngay, khong lap lich
            while (offset + 4 <= end)
            {
                int size = ReadInt32BE(data, offset);
                offset += 4;
                if (size <= 0 || offset + size > end) return;
                TryParsePacket(data, offset, offset + size, results);
                offset += size;
            }
            return;
        }

        if (TryParseMessage(data, start, end, out string address, out object[] args))
            results.Add((address, args));
    }

    internal static bool TryParseMessage(byte[] data, out string address, out object[] args) =>
        TryParseMessage(data, 0, data.Length, out address, out args);

    internal static bool TryParseMessage(byte[] data, int start, int end, out string address, out object[] args)
    {
        address = null;
        args = null;
        int offset = start;

        if (!TryReadOscString(data, ref offset, end, out address)) return false;
        if (address.Length == 0 || address[0] != '/') return false;

        // Goi tin khong co type tag (OSC 1.0 cu) - coi nhu khong co tham so
        if (offset >= end)
        {
            args = Array.Empty<object>();
            return true;
        }

        if (!TryReadOscString(data, ref offset, end, out string typeTags) ||
            typeTags.Length == 0 || typeTags[0] != ',')
            return false;

        var result = new List<object>(typeTags.Length - 1);
        for (int i = 1; i < typeTags.Length; i++)
        {
            switch (typeTags[i])
            {
                case 'i':
                    if (offset + 4 > end) return false;
                    result.Add(ReadInt32BE(data, offset));
                    offset += 4;
                    break;
                case 'f':
                    if (offset + 4 > end) return false;
                    result.Add(ReadFloatBE(data, offset));
                    offset += 4;
                    break;
                case 'h':
                    if (offset + 8 > end) return false;
                    result.Add(ReadInt64BE(data, offset));
                    offset += 8;
                    break;
                case 'd':
                    if (offset + 8 > end) return false;
                    result.Add(BitConverter.Int64BitsToDouble(ReadInt64BE(data, offset)));
                    offset += 8;
                    break;
                case 's':
                case 'S':
                    if (!TryReadOscString(data, ref offset, end, out string s)) return false;
                    result.Add(s);
                    break;
                case 'b':
                    if (offset + 4 > end) return false;
                    int blobSize = ReadInt32BE(data, offset);
                    offset += 4 + ((blobSize + 3) & ~3);
                    if (blobSize < 0 || offset > end) return false;
                    result.Add(null); // blob khong dung toi, giu cho de index tham so khong lech
                    break;
                case 't':
                    if (offset + 8 > end) return false;
                    offset += 8;
                    result.Add(null);
                    break;
                case 'c':
                case 'r':
                case 'm':
                    if (offset + 4 > end) return false;
                    result.Add(ReadInt32BE(data, offset));
                    offset += 4;
                    break;
                case 'T': result.Add(true); break;
                case 'F': result.Add(false); break;
                case 'N':
                case 'I': result.Add(null); break;
                default:
                    return false; // kieu chua ho tro (mang [ ] ...), khong doan kich thuoc
            }
        }

        args = result.ToArray();
        return true;
    }

    static bool TryReadOscString(byte[] data, ref int offset, int end, out string value)
    {
        value = null;
        int start = offset;
        while (offset < end && data[offset] != 0) offset++;
        if (offset >= end) return false;
        value = Encoding.ASCII.GetString(data, start, offset - start);
        offset++; // bo qua byte 0 ket thuc chuoi
        offset = start + ((offset - start + 3) & ~3); // can le ve boi so cua 4 theo dung OSC spec
        return true;
    }

    static int ReadInt32BE(byte[] d, int o) =>
        (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];

    static long ReadInt64BE(byte[] d, int o) =>
        ((long)(uint)ReadInt32BE(d, o) << 32) | (uint)ReadInt32BE(d, o + 4);

    static float ReadFloatBE(byte[] d, int o) =>
        BitConverter.Int32BitsToSingle(ReadInt32BE(d, o));

#if UNITY_EDITOR
    // Self-test toi thieu cho parser OSC - chay khi Editor load script, khong can framework test.
    [UnityEditor.InitializeOnLoadMethod]
    static void SelfTestParser()
    {
        byte[] message = BuildTestMessage("/person/x", 0.5f);
        bool okMessage = TryParseMessage(message, out string addr, out object[] args)
            && addr == "/person/x" && args.Length == 1 && Mathf.Approximately((float)args[0], 0.5f);
        Debug.Assert(okMessage, "[MiniOscReceiver] Self-test parser OSC message that bai");

        byte[] bundle = BuildTestBundle(BuildTestMessage("/person/x", 0.25f), BuildTestMessage("/person/y", 0.75f));
        var parsed = new List<(string, object[])>();
        TryParsePacket(bundle, 0, bundle.Length, parsed);
        bool okBundle = parsed.Count == 2 && parsed[0].Item1 == "/person/x" && parsed[1].Item1 == "/person/y"
            && TryGetFloat(parsed[1].Item2, 0, out float y) && Mathf.Approximately(y, 0.75f);
        Debug.Assert(okBundle, "[MiniOscReceiver] Self-test parser OSC bundle that bai");
    }

    static byte[] BuildTestMessage(string address, float value)
    {
        using var ms = new System.IO.MemoryStream();
        WriteOscString(ms, address);
        WriteOscString(ms, ",f");
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        ms.Write(bytes, 0, 4);
        return ms.ToArray();
    }

    static byte[] BuildTestBundle(params byte[][] elements)
    {
        using var ms = new System.IO.MemoryStream();
        WriteOscString(ms, "#bundle");
        ms.Write(new byte[8], 0, 8); // timetag
        foreach (var element in elements)
        {
            byte[] size = BitConverter.GetBytes(element.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(size);
            ms.Write(size, 0, 4);
            ms.Write(element, 0, element.Length);
        }
        return ms.ToArray();
    }

    static void WriteOscString(System.IO.MemoryStream ms, string s)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(s);
        ms.Write(bytes, 0, bytes.Length);
        int pad = 4 - (bytes.Length % 4);
        for (int i = 0; i < pad; i++) ms.WriteByte(0);
    }
#endif
}
